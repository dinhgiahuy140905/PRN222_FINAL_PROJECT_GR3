using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace SalesManagement.Web.Services;

public static class DisplayFormatter
{
    public static IHtmlContent Date(DateTime value, bool includeTime = true) => new HtmlString(
        $"<span data-date=\"{value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\" data-time=\"{includeTime.ToString().ToLowerInvariant()}\">{value.ToString(includeTime ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy", CultureInfo.InvariantCulture)}</span>");

    public static IHtmlContent Number(int value) => new HtmlString(
        $"<span data-number=\"{value}\">{HtmlEncoder.Default.Encode(value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")))}</span>");
}
