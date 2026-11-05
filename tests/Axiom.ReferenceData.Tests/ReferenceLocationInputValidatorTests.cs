// 验证三类基础档案在 API 边界执行的输入规则。
using Axiom.ReferenceData.Api.Models;
using FluentAssertions;

namespace Axiom.ReferenceData.Tests;

public sealed class ReferenceLocationInputValidatorTests
{
    [Theory]
    [InlineData(ReferenceLocationType.Airport, "sha")]
    [InlineData(ReferenceLocationType.Seaport, "CNSHA")]
    [InlineData(ReferenceLocationType.RailStation, "BJP-01")]
    public void Validate_with_valid_input_returns_no_errors(ReferenceLocationType type, string code)
    {
        var request = CreateRequest(code);

        var errors = ReferenceLocationInputValidator.Validate(type, request);

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ReferenceLocationType.Airport, "CNSHA")]
    [InlineData(ReferenceLocationType.Seaport, "SHA")]
    [InlineData(ReferenceLocationType.RailStation, "INVALID CODE")]
    public void Validate_with_invalid_code_returns_code_error(ReferenceLocationType type, string code)
    {
        var errors = ReferenceLocationInputValidator.Validate(type, CreateRequest(code));

        errors.Should().ContainKey("code");
    }

    [Fact]
    public void Validate_with_overlong_name_and_invalid_coordinates_returns_all_errors()
    {
        var request = CreateRequest("SHA") with
        {
            Name = new string('a', 201),
            Latitude = 91,
            Longitude = -181
        };

        var errors = ReferenceLocationInputValidator.Validate(ReferenceLocationType.Airport, request);

        errors.Keys.Should().Contain(["name", "latitude", "longitude"]);
    }

    private static UpsertReferenceLocationRequest CreateRequest(string code) => new(
        code,
        "测试地点",
        "cn",
        "上海",
        31.1,
        121.8);
}
