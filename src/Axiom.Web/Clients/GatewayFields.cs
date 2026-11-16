// 网关线协议字段名：客户端构造与 Mock 解析共用同一份定义，消除双份魔法字符串。
namespace Axiom.Web.Clients;

internal static class GatewayFields
{
    // 查询串参数
    public const string PAGE = "page";
    public const string PAGE_SIZE = "pageSize";
    public const string LEVEL = "level";
    public const string COUNTRY_CODE = "countryCode";
    public const string SUBDIVISION_CODE = "subdivisionCode";
    public const string KEYWORD = "q";
    public const string STATUS = "status";
    public const string CATEGORY = "category";

    // 请求体字段
    public const string NAME = "name";
    public const string CODE = "code";
    public const string KIND = "kind";
    public const string PARENT_ID = "parentId";
    public const string ASCII_NAME = "asciiName";
    public const string LOCAL_NAME = "localName";
    public const string LATITUDE = "latitude";
    public const string LONGITUDE = "longitude";
    public const string POPULATION = "population";
    public const string SORT_ORDER = "sortOrder";
    public const string TITLE = "title";
    public const string SUMMARY = "summary";
    public const string CONTENT = "content";
    public const string AUTHOR = "author";

    // 路径段
    public const string TREE = "tree";
    public const string CHILDREN = "children";
    public const string TRANSLATIONS = "translations";
}
