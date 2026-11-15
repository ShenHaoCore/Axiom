// 枚举字典实体与 Kind 规范化的最小自检。
using Axiom.ReferenceData.Domain.Constants;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using FluentAssertions;

namespace Axiom.ReferenceData.Tests;

public sealed class ReferenceEnumTests
{
    [Fact]
    public void Create_normalizes_category_and_code()
    {
        var entity = ReferenceEnum.Create(" Geo_Division.Kind ", " Municipality ", "直辖市", 1);

        entity.Category.Should().Be(ReferenceEnumCategories.GeoDivisionKind);
        entity.Code.Should().Be(GeoDivisionKinds.Municipality);
        entity.Name.Should().Be("直辖市");
        entity.SortOrder.Should().Be(1);
    }

    [Fact]
    public void Update_rejects_empty_name()
    {
        var entity = ReferenceEnum.Create(ReferenceEnumCategories.GeoDivisionKind, "province", "省");

        var act = () => entity.Update("  ", null);

        act.Should().Throw<ReferenceEnumValidationException>();
    }

    [Fact]
    public void GeoDivisionKinds_normalize_falls_back_to_other()
    {
        GeoDivisionKinds.Normalize(null).Should().Be(GeoDivisionKinds.Other);
        GeoDivisionKinds.Normalize("  MUNICIPALITY ").Should().Be(GeoDivisionKinds.Municipality);
    }

    [Fact]
    public void GeoDivisionKinds_seed_items_do_not_contain_standard()
    {
        GeoDivisionKinds.SeedItems.Should().NotContain(item => item.Code == "standard");
        GeoDivisionKinds.SeedItems.Should().Contain(item => item.Code == GeoDivisionKinds.Country);
        GeoDivisionKinds.SeedItems.Should().Contain(item => item.Code == GeoDivisionKinds.State);
        GeoDivisionKinds.SeedItems.Should().Contain(item => item.Code == GeoDivisionKinds.PrefectureCity);
    }

    [Fact]
    public void GeoDivisionKinds_english_names_cover_all_seed_codes()
    {
        var codes = GeoDivisionKinds.SeedItems.Select(item => item.Code).Distinct().ToArray();
        codes.Should().NotBeEmpty();
        foreach (var code in codes)
        {
            GeoDivisionKinds.EnglishNames.Should().ContainKey(code);
        }

        GeoDivisionKinds.EnglishNames[GeoDivisionKinds.Municipality].Should().Be("Municipality");
        GeoDivisionKinds.EnglishNames[GeoDivisionKinds.Country].Should().Be("Country");
    }

    [Fact]
    public void GeoDivisionKinds_derive_default_by_country_and_level()
    {
        GeoDivisionKinds.DeriveDefault("CN", 1, null).Should().Be(GeoDivisionKinds.Country);
        GeoDivisionKinds.DeriveDefault("CN", 2, "11").Should().Be(GeoDivisionKinds.Municipality);
        GeoDivisionKinds.DeriveDefault("CN", 2, "15").Should().Be(GeoDivisionKinds.AutonomousRegion);
        GeoDivisionKinds.DeriveDefault("CN", 2, "81").Should().Be(GeoDivisionKinds.SpecialAdministrativeRegion);
        GeoDivisionKinds.DeriveDefault("CN", 2, "44").Should().Be(GeoDivisionKinds.Province);
        GeoDivisionKinds.DeriveDefault("CN", 3, "44").Should().Be(GeoDivisionKinds.PrefectureCity);
        GeoDivisionKinds.DeriveDefault("CN", 4, "44").Should().Be(GeoDivisionKinds.District);
        GeoDivisionKinds.DeriveDefault("US", 2, "06").Should().Be(GeoDivisionKinds.State);
        GeoDivisionKinds.DeriveDefault("US", 3, "06").Should().Be(GeoDivisionKinds.County);
        GeoDivisionKinds.DeriveDefault("GB", 2, "ENG").Should().Be(GeoDivisionKinds.ConstituentCountry);
        GeoDivisionKinds.DeriveDefault("JP", 2, "13").Should().Be(GeoDivisionKinds.Prefecture);
    }
}
