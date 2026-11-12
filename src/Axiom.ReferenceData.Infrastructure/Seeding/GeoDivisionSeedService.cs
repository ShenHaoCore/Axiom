// 启动时同步行政区层级：先内置国家，再尝试 GeoNames 增量。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using Axiom.ReferenceData.Infrastructure.EfCore;

namespace Axiom.ReferenceData.Infrastructure.Seeding;

public sealed class GeoDivisionSeedService(
    IServiceProvider serviceProvider,
    GeoNamesDownloader downloader,
    ILogger<GeoDivisionSeedService> logger,
    IConfiguration configuration)
    : BackgroundService
{
    private readonly bool _enabled = configuration.GetValue<bool>("GeoDivisions:EnableSeed", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("行政区种子已关闭（GeoDivisions:EnableSeed=false）。");
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
        // Phase 0: Kind 枚举字典（行政区写入依赖这些 code）
        await EnsureGeoDivisionKindEnumsAsync(dbContext, cancellationToken);

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
            await PatchCnAdmin1KindsAsync(dbContext, cancellationToken);
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

        // Phase 4: 区县占位（正式数据源待接入，仅样例便于联调 Level=4）
        var districtCount = await SeedDistrictPlaceholdersAsync(dbContext, cancellationToken);
        logger.LogInformation("区县占位种子完成：{Count} 条。", districtCount);

        // 最终统计
        var total = await dbContext.GeoDivisions.CountAsync(cancellationToken);
        logger.LogInformation("行政区种子全部完成：数据库共 {Total} 条 GeoDivision。", total);
    }

    private static async Task<Dictionary<string, Guid>> UpsertCountriesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CountrySeed> countries,
        CancellationToken cancellationToken)
    {
        var codeToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var existing = await dbContext.GeoDivisions
            .AsNoTracking()
            .Where(r => r.Level == 1)
            .ToDictionaryAsync(r => r.CountryCode, r => r.Id, cancellationToken);

        var toAdd = new List<GeoDivision>();
        foreach (var country in countries)
        {
            if (existing.TryGetValue(country.Code, out var id))
            {
                codeToId[country.Code] = id;
                continue;
            }

            // 同 CountryCode+Level+Code 唯一键可能有其他条目，也尝试复用
            var dup = await dbContext.GeoDivisions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.CountryCode == country.Code && r.Level == 1 && r.Code == country.Code,
                    cancellationToken);
            if (dup is not null)
            {
                codeToId[country.Code] = dup.Id;
                continue;
            }

            var region = GeoDivision.Create(
                1,
                country.Code,
                country.Name,
                code: country.Code,
                asciiName: country.Name);
            toAdd.Add(region);
            codeToId[country.Code] = region.Id;
        }

        if (toAdd.Count > 0)
        {
            await dbContext.GeoDivisions.AddRangeAsync(toAdd, cancellationToken);
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
        var existing = await dbContext.GeoDivisions
            .AsNoTracking()
            .Where(r => r.Level == 2)
            .Select(r => new { r.CountryCode, r.SubdivisionCode, r.Id })
            .Where(r => r.SubdivisionCode != null)
            .ToDictionaryAsync(
                r => $"{r.CountryCode}.{r.SubdivisionCode}",
                r => r.Id,
                cancellationToken);

        foreach (var row in admin1List)
        {
            keyToId[$"{row.CountryCode}.{row.Admin1Code}"] = existing.TryGetValue($"{row.CountryCode}.{row.Admin1Code}", out var eId)
                ? eId
                : Guid.Empty;
        }

        var toAdd = new List<GeoDivision>();
        foreach (var row in admin1List)
        {
            var key = $"{row.CountryCode}.{row.Admin1Code}";
            if (existing.ContainsKey(key)) continue;
            if (!countryIdByCode.TryGetValue(row.CountryCode, out var countryId)) continue;

            // GeoNameId 唯一键可能冲突（同一条目已存在），跳过。
            var dupByGeoNameId = row.GeoNameId > 0
                ? await dbContext.GeoDivisions.AsNoTracking().FirstOrDefaultAsync(
                    r => r.GeoNameId == row.GeoNameId, cancellationToken)
                : null;
            if (dupByGeoNameId is not null)
            {
                keyToId[key] = dupByGeoNameId.Id;
                continue;
            }

            var region = GeoDivision.Create(
                2,
                row.CountryCode,
                row.Name,
                subdivisionCode: row.Admin1Code,
                code: row.Admin1Code,
                asciiName: row.AsciiName,
                parentId: countryId,
                geoNameId: row.GeoNameId,
                kind: CnAdmin1Kinds.Resolve(row.CountryCode, row.Admin1Code));
            toAdd.Add(region);
            keyToId[key] = region.Id;
        }

        if (toAdd.Count > 0)
        {
            await dbContext.GeoDivisions.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return keyToId;
    }

    /// <summary>幂等写入 geo_division.kind 预设，供行政区 Kind 强校验。</summary>
    private static async Task EnsureGeoDivisionKindEnumsAsync(
        ReferenceDataDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var category = ReferenceEnumCategories.GeoDivisionKind;
        var existing = await dbContext.ReferenceEnums
            .Where(e => e.Category == category)
            .Select(e => e.Code)
            .ToListAsync(cancellationToken);
        var existingCodes = existing.ToHashSet(StringComparer.Ordinal);

        var toAdd = new List<ReferenceEnum>();
        foreach (var (code, name, sortOrder) in GeoDivisionKinds.SeedItems)
        {
            if (existingCodes.Contains(code))
            {
                continue;
            }

            toAdd.Add(ReferenceEnum.Create(category, code, name, sortOrder));
        }

        if (toAdd.Count == 0)
        {
            return;
        }

        await dbContext.ReferenceEnums.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>已存在的 CN Admin1 补写 Kind，避免仅新插入行带类型。</summary>
    private static async Task PatchCnAdmin1KindsAsync(
        ReferenceDataDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var cnAdmin1 = await dbContext.GeoDivisions
            .Where(r => r.Level == 2 && r.CountryCode == "CN")
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var region in cnAdmin1)
        {
            var kind = CnAdmin1Kinds.Resolve(region.CountryCode, region.SubdivisionCode);
            if (region.Kind == kind)
            {
                continue;
            }

            region.Update(
                name: null,
                asciiName: null,
                localName: null,
                code: null,
                latitude: null,
                longitude: null,
                population: null,
                parentId: null,
                kind: kind);
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<int> UpsertCitiesAsync(
        ReferenceDataDbContext dbContext,
        List<GeoNamesDownloader.CityRow> cities,
        Dictionary<string, Guid> countryIdByCode,
        Dictionary<string, Guid> admin1IdByKey,
        CancellationToken cancellationToken)
    {
        var existingGeoIds = await dbContext.GeoDivisions
            .AsNoTracking()
            .Where(r => r.Level == 3 && r.GeoNameId.HasValue)
            .Select(r => r.GeoNameId!.Value)
            .ToHashSetAsync(cancellationToken);

        // 直辖市名称：用于跳过「省+同名市」双记录中的冗余市。
        var municipalityNames = await dbContext.GeoDivisions
            .AsNoTracking()
            .Where(r => r.Kind == GeoDivisionKinds.Municipality)
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        var toAdd = new List<GeoDivision>();
        foreach (var city in cities)
        {
            if (existingGeoIds.Contains(city.GeoNameId)) continue;
            if (!countryIdByCode.TryGetValue(city.CountryCode, out _)) continue;

            Guid? parentId = null;
            if (!string.IsNullOrEmpty(city.Admin1Code))
            {
                if (admin1IdByKey.TryGetValue($"{city.CountryCode}.{city.Admin1Code}", out var admin1Id))
                {
                    parentId = admin1Id;
                }
            }

            // 直辖市下与省同名的市级重复不入库。
            if (parentId is Guid municipalityId
                && municipalityNames.TryGetValue(municipalityId, out var parentName)
                && string.Equals(parentName.Trim(), city.Name.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            var region = GeoDivision.Create(
                3,
                city.CountryCode,
                city.Name,
                subdivisionCode: city.Admin1Code,
                code: city.GeoNameId.ToString(),
                asciiName: city.AsciiName,
                localName: ExtractLocalName(city.AlternateNames),
                latitude: city.Latitude,
                longitude: city.Longitude,
                population: city.Population,
                parentId: parentId,
                geoNameId: city.GeoNameId);
            toAdd.Add(region);
        }

        // 分批插入避免参数爆炸
        const int batchSize = 500;
        for (var offset = 0; offset < toAdd.Count; offset += batchSize)
        {
            var batch = toAdd.Skip(offset).Take(batchSize).ToList();
            await dbContext.GeoDivisions.AddRangeAsync(batch, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }

    /// <summary>
    /// shortcut: 区县正式源未定，仅在已有 CN 城市下挂少量样例；接入国标/GeoNames admin2 后替换本方法。
    /// </summary>
    private async Task<int> SeedDistrictPlaceholdersAsync(
        ReferenceDataDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // 样例：挂到「广州市」下（按名称匹配，城市种子缺失则跳过）。
        var guangzhou = await dbContext.GeoDivisions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.CountryCode == "CN"
                     && r.Level == 3
                     && r.Name == "广州市",
                cancellationToken);
        if (guangzhou is null)
        {
            logger.LogInformation("区县占位：未找到广州市，跳过样例写入。");
            return 0;
        }

        var samples = new (string Code, string Name, string AsciiName)[]
        {
            ("440103", "荔湾区", "Liwan"),
            ("440106", "天河区", "Tianhe")
        };

        var existingCodes = await dbContext.GeoDivisions
            .AsNoTracking()
            .Where(r => r.CountryCode == "CN" && r.Level == 4)
            .Select(r => r.Code)
            .ToHashSetAsync(cancellationToken);

        var toAdd = new List<GeoDivision>();
        foreach (var sample in samples)
        {
            if (existingCodes.Contains(sample.Code))
            {
                continue;
            }

            toAdd.Add(GeoDivision.Create(
                4,
                "CN",
                sample.Name,
                subdivisionCode: guangzhou.SubdivisionCode,
                code: sample.Code,
                asciiName: sample.AsciiName,
                parentId: guangzhou.Id));
        }

        if (toAdd.Count == 0)
        {
            return 0;
        }

        await dbContext.GeoDivisions.AddRangeAsync(toAdd, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
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
