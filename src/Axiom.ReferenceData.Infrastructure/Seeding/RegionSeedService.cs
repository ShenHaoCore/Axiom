// 启动时同步行政区层级：先内置国家，再尝试 GeoNames 增量。
// 全部按 (country, code) 复合业务键 upsert，与 geo_division 数据集的键口径一致。
using Axiom.ReferenceData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Seeding;

public sealed class RegionSeedService(
    IServiceProvider serviceProvider,
    GeoNamesDownloader downloader,
    ILogger<RegionSeedService> logger,
    IConfiguration configuration)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue<bool>("Regions:EnableSeed", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("行政区种子已关闭（Regions:EnableSeed=false）。");
            return;
        }

        // 延迟启动：等数据库可用，避免迁移竞争。
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
            await SeedAsync(dbContext, stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "行政区种子执行失败（不阻塞应用启动）。");
        }
    }

    private async Task SeedAsync(ReferenceDataDbContext dbContext, CancellationToken cancellationToken)
    {
        // Phase 1: 内置国家（depth 0，code = ISO 国家代码）
        var countries = await downloader.LoadBuiltInCountriesAsync(cancellationToken);
        var countryCodes = await UpsertCountriesAsync(dbContext, countries, cancellationToken);
        logger.LogInformation("内置国家种子完成：{Count} 个国家。", countryCodes.Count);

        // Phase 2: GeoNames 一级行政区（depth 1）
        List<GeoNamesDownloader.Admin1Row>? admin1List = null;
        try
        {
            admin1List = await downloader.DownloadProvinceCodesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "GeoNames admin1 下载失败，仅使用内置国家数据。");
            admin1List = null;
        }

        if (admin1List is { Count: > 0 })
        {
            var count = await UpsertProvincesAsync(dbContext, admin1List, countryCodes, cancellationToken);
            logger.LogInformation("GeoNames 一级行政区种子完成：{Count} 条。", count);
        }

        // Phase 3: GeoNames 城市（depth 2，人口 ≥ MinPopulation）
        try
        {
            var cities = await downloader.DownloadCitiesAsync(cancellationToken);
            if (cities.Count > 0)
            {
                var cityCount = await UpsertCitiesAsync(dbContext, cities, countryCodes, cancellationToken);
                logger.LogInformation("GeoNames 城市种子完成：{Count} 条。", cityCount);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "GeoNames allCountries 下载失败，跳过城市种子。");
        }

        // 最终统计
        var total = await dbContext.Regions.CountAsync(cancellationToken);
        logger.LogInformation("行政区种子全部完成：数据库共 {Total} 条 Region。", total);
    }

    private static async Task<Dictionary<string, string>> UpsertCountriesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CountrySeed> countries,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.Depth == 0)
            .ToDictionaryAsync(r => r.CountryCode, r => r.Code, cancellationToken);

        var toAdd = new List<Region>();
        foreach (var country in countries)
        {
            // 国家行：code 即 ISO 国家代码，与 (country, code) 复合键中的 country 相同。
            if (existing.TryGetValue(country.Code, out _))
            {
                continue;
            }

            toAdd.Add(Region.Create(
                depth: 0,
                countryCode: country.Code,
                code: country.Code,
                name: country.Name,
                asciiName: country.Name));
        }

        if (toAdd.Count > 0)
        {
            await dbContext.Regions.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return countries.ToDictionary(c => c.Code, c => c.Code);
    }

    private async Task<int> UpsertProvincesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.Admin1Row> admin1List,
        Dictionary<string, string> countryCodes,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.Depth == 1)
            .Select(r => new { r.CountryCode, r.Code })
            .ToDictionaryAsync(r => $"{r.CountryCode}.{r.Code}", cancellationToken);

        var toAdd = new List<Region>();
        foreach (var row in admin1List)
        {
            var key = $"{row.CountryCode}.{row.Code}";
            if (existing.ContainsKey(key)) continue;
            if (!countryCodes.TryGetValue(row.CountryCode, out var countryCode)) continue;

            // GeoNameId 唯一键可能冲突（同一条目已存在），跳过。
            if (row.GeoNameId > 0 && await dbContext.Regions.AsNoTracking().AnyAsync(
                    r => r.GeoNameId == row.GeoNameId, cancellationToken))
            {
                continue;
            }

            toAdd.Add(Region.Create(
                depth: 1,
                countryCode: row.CountryCode,
                code: row.Code,
                name: row.Name,
                parentCode: countryCode,
                asciiName: row.AsciiName,
                geoNameId: row.GeoNameId,
                type: RegionTypeIngestion.DeriveForProvince(row.CountryCode, row.Code)));
        }

        if (toAdd.Count > 0)
        {
            await dbContext.Regions.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }

    private async Task<int> UpsertCitiesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CityRow> cities,
        Dictionary<string, string> countryCodes,
        CancellationToken cancellationToken)
    {
        var existingGeoIds = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.GeoNameId.HasValue)
            .Select(r => r.GeoNameId!.Value)
            .ToHashSetAsync(cancellationToken);

        var toAdd = new List<Region>();
        foreach (var city in cities)
        {
            if (existingGeoIds.Contains(city.GeoNameId)) continue;
            if (!countryCodes.TryGetValue(city.CountryCode, out var countryCode)) continue;

            // 上级一级行政区必须存在（城市挂 depth 1 行）。
            if (string.IsNullOrEmpty(city.ParentCode)) continue;

            toAdd.Add(Region.Create(
                depth: 2,
                countryCode: city.CountryCode,
                code: city.GeoNameId.ToString(),
                name: city.Name,
                parentCode: city.ParentCode,
                asciiName: city.AsciiName,
                localName: ExtractLocalName(city.AlternateNames),
                latitude: city.Latitude,
                longitude: city.Longitude,
                population: city.Population,
                geoNameId: city.GeoNameId,
                type: RegionTypeIngestion.ForGeoNamesCity()));
        }

        // 分批插入避免参数爆炸
        const int batchSize = 500;
        for (var offset = 0; offset < toAdd.Count; offset += batchSize)
        {
            var batch = toAdd.Skip(offset).Take(batchSize).ToList();
            await dbContext.Regions.AddRangeAsync(batch, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }

    private static string? ExtractLocalName(string? alternateNames)
    {
        if (string.IsNullOrWhiteSpace(alternateNames))
        {
            return null;
        }

        // 简化：取第一个逗号分隔项作为本地名（GeoNames 中文 alternateNames 通常是逗号分隔多语言）。
        var commaIndex = alternateNames.IndexOf(',');
        var first = commaIndex > 0 ? alternateNames[..commaIndex] : alternateNames;
        return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
    }
}
