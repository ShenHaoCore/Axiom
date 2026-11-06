// 行政区层级：国家 / 一级行政区 / 城市，数据来自 GeoNames 种子。
namespace Axiom.ReferenceData.Api.Models;

public enum RegionLevel
{
    Country = 1,
    Admin1 = 2,
    City = 3
}
