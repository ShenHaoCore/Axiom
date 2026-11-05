// 验证 gRPC 可空字段映射后仍保持原外部 REST 响应结构。
using Axiom.ApiGateway;
using Axiom.Contracts.ReferenceData.V1;
using FluentAssertions;

namespace Axiom.IntegrationTests;

public sealed class AirportResponseTests
{
    [Fact]
    public void Grpc_message_maps_present_and_missing_optional_fields()
    {
        var id = Guid.NewGuid();
        var complete = new Airport
        {
            Id = id.ToString(),
            Code = "SHA",
            Name = "上海浦东国际机场",
            CountryCode = "CN",
            City = "上海",
            Latitude = 31.1443,
            Longitude = 121.8083
        };

        var response = AirportResponse.FromMessage(complete);
        var missingOptionals = AirportResponse.FromMessage(new Airport
        {
            Id = id.ToString(),
            Code = "PEK",
            Name = "北京首都国际机场",
            CountryCode = "CN"
        });

        response.Should().BeEquivalentTo(new AirportResponse(
            id,
            "SHA",
            "上海浦东国际机场",
            "CN",
            "上海",
            31.1443,
            121.8083));
        missingOptionals.City.Should().BeNull();
        missingOptionals.Latitude.Should().BeNull();
        missingOptionals.Longitude.Should().BeNull();
    }
}
