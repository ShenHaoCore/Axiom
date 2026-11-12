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
        var entity = ReferenceEnum.Create(ReferenceEnumCategories.GeoDivisionKind, "standard", "标准");

        var act = () => entity.Update("  ", null);

        act.Should().Throw<ReferenceEnumValidationException>();
    }

    [Fact]
    public void GeoDivisionKinds_normalize_falls_back_to_standard()
    {
        GeoDivisionKinds.Normalize(null).Should().Be(GeoDivisionKinds.Standard);
        GeoDivisionKinds.Normalize("  MUNICIPALITY ").Should().Be(GeoDivisionKinds.Municipality);
    }
}
