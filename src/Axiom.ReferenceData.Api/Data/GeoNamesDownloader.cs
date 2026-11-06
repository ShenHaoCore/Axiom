// 从 GeoNames 下载并解析行政区层级数据。
using System.IO.Compression;
using System.Text.Json;

namespace Axiom.ReferenceData.Api.Data;

public sealed class GeoNamesDownloader(HttpClient httpClient, ILogger<GeoNamesDownloader> logger, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly char[] Separators = ['\t'];

    private readonly Uri _admin1Url = new(
        configuration["GeoNames:Admin1CodesUrl"] ?? "http://download.geonames.org/export/dump/admin1CodesASCII.txt");
    private readonly Uri _allCountriesUrl = new(
        configuration["GeoNames:AllCountriesUrl"] ?? "http://download.geonames.org/export/dump/allCountries.zip");
    private readonly long _minPopulation = configuration.GetValue<long>("GeoNames:MinPopulation", 500_000);

    // 一级行政区关键字段：admin1Code (CC.ADM1), name, geonameid, countryCode
    public sealed record Admin1Row(
        string CountryCode,
        string Admin1Code,
        string Name,
        string? AsciiName,
        int GeoNameId);

    // 城市关键字段：geonameid, countryCode, admin1Code, name, population, lat/lng
    public sealed record CityRow(
        int GeoNameId,
        string CountryCode,
        string? Admin1Code,
        string Name,
        string? AsciiName,
        string? AlternateNames,
        long Population,
        double Latitude,
        double Longitude);

    // 内置国家数据
    public sealed record CountrySeed(string Code, string Name);

    public async Task<List<CountrySeed>> LoadBuiltInCountriesAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(GeoNamesDownloader).Assembly;
        var resourceName = "Axiom.ReferenceData.Api.Data.countries.json";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入资源 {resourceName} 未找到。");
        var countries = await JsonSerializer.DeserializeAsync<List<CountrySeed>>(stream, JsonOptions, cancellationToken);
        return countries ?? new List<CountrySeed>();
    }

    public async Task<List<Admin1Row>> DownloadAdmin1CodesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("下载 GeoNames admin1CodesASCII.txt ...");
        var content = await httpClient.GetStringAsync(_admin1Url, cancellationToken);
        var result = new List<Admin1Row>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith('#')) continue;
            var parts = line.Split(Separators);
            if (parts.Length < 4) continue;
            // admin1 code 格式: CC.ADM1
            var code = parts[0];
            var dotIndex = code.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= code.Length - 1) continue;
            var countryCode = code[..dotIndex];
            var admin1Code = code[(dotIndex + 1)..];
            if (int.TryParse(parts[3], out var geoNameId))
            {
                result.Add(new Admin1Row(countryCode, admin1Code, parts[1], parts[2], geoNameId));
            }
        }
        logger.LogInformation("GeoNames admin1Codes 解析完成：{Count} 条。", result.Count);
        return result;
    }

    public async Task<List<CityRow>> DownloadCitiesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("下载 GeoNames allCountries.zip ...");
        await using var zipStream = await httpClient.GetStreamAsync(_allCountriesUrl, cancellationToken);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".txt"));
        if (entry is null)
        {
            logger.LogWarning("allCountries.zip 中未找到 .txt 条目。");
            return new List<CityRow>();
        }

        // GeoNames 城市/特征代码：国家首都、首都、一级/二级行政区城市、人口城市
        var featureCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "PPLC", "PPLA", "PPLA2", "PPLA3", "PPLA4", "PPL"
        };

        var result = new List<CityRow>();
        using (var entryStream = entry.Open())
        using (var reader = new StreamReader(entryStream))
        {
            string? line;
            var skipped = 0;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                var parts = line.Split(Separators);
                // 列: geonameid(0) name(1) asciiname(2) alternatenames(3) lat(4) lng(5) fclass(6) fcode(7)
                // country(8) cc2(9) admin1(10) admin2(11) admin3(12) admin4(13) pop(14) elev(15) dem(16) tz(17) mod(18)
                if (parts.Length < 15) { skipped++; continue; }

                var fcode = parts[7];
                if (!featureCodes.Contains(fcode)) { skipped++; continue; }

                if (!long.TryParse(parts[14], out var population) || population < _minPopulation)
                {
                    skipped++;
                    continue;
                }

                if (!int.TryParse(parts[0], out var geoNameId)) { skipped++; continue; }
                var countryCode = parts[8];
                if (string.IsNullOrWhiteSpace(countryCode)) { skipped++; continue; }

                if (!double.TryParse(parts[4], out var lat) || !double.TryParse(parts[5], out var lng))
                {
                    skipped++;
                    continue;
                }

                result.Add(new CityRow(
                    GeoNameId: geoNameId,
                    CountryCode: countryCode,
                    Admin1Code: string.IsNullOrWhiteSpace(parts[10]) ? null : parts[10],
                    Name: parts[1],
                    AsciiName: string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2],
                    AlternateNames: string.IsNullOrWhiteSpace(parts[3]) ? null : parts[3],
                    Population: population,
                    Latitude: lat,
                    Longitude: lng));
            }
            logger.LogInformation(
                "GeoNames allCountries 解析完成：城市 {Count} 条（跳过 {Skipped} 条不符合筛选）。",
                result.Count, skipped);
        }
        return result;
    }
}
