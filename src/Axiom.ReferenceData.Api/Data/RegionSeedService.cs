// 启动时同步行政区层级：先内置国家，再尝试 GeoNames 增量。
using Axiom.ReferenceData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Api.Data;

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
        // Phase 1: 内置国家
        var countries = await downloader.LoadBuiltInCountriesAsync(cancellationToken);
        var countryIdByCode = await UpsertCountriesAsync(dbContext, countries, cancellationToken);
        logger.LogInformation("内置国家种子完成：{Count} 个国家。", countryIdByCode.Count);

        // Phase 2: GeoNames 一级行政区
        List<GeoNamesDownloader.Admin1Row>? admin1List = null;
        try
        {
            admin1List = await downloader.DownloadAdmin1CodesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "GeoNames admin1 下载失败，仅使用内置国家数据。");
            admin1List = null;
        }

        var admin1IdByKey = new Dictionary<string, Guid>();
        if (admin1List is { Count: > 0 })
        {
            admin1IdByKey = await UpsertAdmin1Async(dbContext, admin1List, countryIdByCode, cancellationToken);
            logger.LogInformation("GeoNames admin1 种子完成：{Count} 条。", admin1IdByKey.Count);
        }

        // Phase 3: GeoNames 城市（人口 ≥ MinPopulation）
        try
        {
            var cities = await downloader.DownloadCitiesAsync(cancellationToken);
            if (cities.Count > 0)
            {
                var cityCount = await UpsertCitiesAsync(dbContext, cities, countryIdByCode, admin1IdByKey, cancellationToken);
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

    private static async Task<Dictionary<string, Guid>> UpsertCountriesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CountrySeed> countries,
        CancellationToken cancellationToken)
    {
        var codeToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var existing = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.Level == RegionLevel.Country)
            .ToDictionaryAsync(r => r.CountryCode, r => r.Id, cancellationToken);

        var toAdd = new List<Region>();
        foreach (var country in countries)
        {
            if (existing.TryGetValue(country.Code, out var id))
            {
                codeToId[country.Code] = id;
                continue;
            }

            // 同 CountryCode+Level+Code 唯一键可能有其他条目，也尝试复用
            var dup = await dbContext.Regions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.CountryCode == country.Code && r.Level == RegionLevel.Country && r.Code == country.Code,
                    cancellationToken);
            if (dup is not null)
            {
                codeToId[country.Code] = dup.Id;
                continue;
            }

            var region = new Region
            {
                Id = Guid.NewGuid(),
                ParentId = null,
                Level = RegionLevel.Country,
                CountryCode = country.Code,
                Admin1Code = null,
                Code = country.Code,
                Name = country.Name,
                AsciiName = country.Name,
                GeoNameId = null
            };
            toAdd.Add(region);
            codeToId[country.Code] = region.Id;
        }

        if (toAdd.Count > 0)
        {
            await dbContext.Regions.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return codeToId;
    }

    private static async Task<Dictionary<string, Guid>> UpsertAdmin1Async(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.Admin1Row> admin1List,
        Dictionary<string, Guid> countryIdByCode,
        CancellationToken cancellationToken)
    {
        var keyToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var existing = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.Level == RegionLevel.Admin1)
            .Select(r => new { r.CountryCode, r.Admin1Code, r.Id })
            .Where(r => r.Admin1Code != null)
            .ToDictionaryAsync(
                r => $"{r.CountryCode}.{r.Admin1Code}",
                r => r.Id,
                cancellationToken);

        foreach (var row in admin1List)
        {
            keyToId[$"{row.CountryCode}.{row.Admin1Code}"] = existing.TryGetValue($"{row.CountryCode}.{row.Admin1Code}", out var eId)
                ? eId
                : Guid.Empty;
        }

        var toAdd = new List<Region>();
        foreach (var row in admin1List)
        {
            var key = $"{row.CountryCode}.{row.Admin1Code}";
            if (existing.ContainsKey(key)) continue;
            if (!countryIdByCode.TryGetValue(row.CountryCode, out var countryId)) continue;

            // GeoNameId 唯一键可能冲突（同一条目已存在），跳过。
            var dupByGeoNameId = row.GeoNameId > 0
                ? await dbContext.Regions.AsNoTracking().FirstOrDefaultAsync(
                    r => r.GeoNameId == row.GeoNameId, cancellationToken)
                : null;
            if (dupByGeoNameId is not null)
            {
                keyToId[key] = dupByGeoNameId.Id;
                continue;
            }

            var region = new Region
            {
                Id = Guid.NewGuid(),
                ParentId = countryId,
                Level = RegionLevel.Admin1,
                CountryCode = row.CountryCode,
                Admin1Code = row.Admin1Code,
                Code = row.Admin1Code,
                Name = row.Name,
                AsciiName = row.AsciiName,
                GeoNameId = row.GeoNameId
            };
            toAdd.Add(region);
            keyToId[key] = region.Id;
        }

        if (toAdd.Count > 0)
        {
            await dbContext.Regions.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return keyToId;
    }

    private static async Task<int> UpsertCitiesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CityRow> cities,
        Dictionary<string, Guid> countryIdByCode,
        Dictionary<string, Guid> admin1IdByKey,
        CancellationToken cancellationToken)
    {
        var existingGeoIds = await dbContext.Regions
            .AsNoTracking()
            .Where(r => r.Level == RegionLevel.City && r.GeoNameId.HasValue)
            .Select(r => r.GeoNameId!.Value)
            .ToHashSetAsync(cancellationToken);

        var toAdd = new List<Region>();
        foreach (var city in cities)
        {
            if (existingGeoIds.Contains(city.GeoNameId)) continue;
            if (!countryIdByCode.TryGetValue(city.CountryCode, out var countryId)) continue;

            Guid? parentId = null;
            if (!string.IsNullOrEmpty(city.Admin1Code))
            {
                if (admin1IdByKey.TryGetValue($"{city.CountryCode}.{city.Admin1Code}", out var admin1Id))
                {
                    parentId = admin1Id;
                }
            }

            var region = new Region
            {
                Id = Guid.NewGuid(),
                ParentId = parentId,
                Level = RegionLevel.City,
                CountryCode = city.CountryCode,
                Admin1Code = city.Admin1Code,
                Code = city.GeoNameId.ToString(),
                Name = city.Name,
                AsciiName = city.AsciiName,
                LocalName = ExtractLocalName(city.AlternateNames),
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                Population = city.Population,
                GeoNameId = city.GeoNameId
            };
            toAdd.Add(region);
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
        if (string.IsNullOrWhiteSpace(alternateNames)) return null;
        // 简化：取第一个逗号分隔项作为本地名（GeoNames 中文 alternateNames 通常是逗号分隔多语言）。
        var commaIndex = alternateNames.IndexOf(',');
        var first = commaIndex > 0 ? alternateNames[..commaIndex] : alternateNames;
        return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
    }
}
