// Kind 下拉选项：国家行只允许 country，其余层级不允许选 country。
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Axiom.Web.Models;

public static class GeoDivisionKindOptions
{
    public static List<SelectListItem> ToSelectList(IEnumerable<ReferenceEnumView> items, int level) =>
        (level == 1
                ? items.Where(item => item.Code == "country")
                : items.Where(item => item.Code != "country"))
            .Select(item => new SelectListItem(item.Name, item.Code))
            .ToList();
}
