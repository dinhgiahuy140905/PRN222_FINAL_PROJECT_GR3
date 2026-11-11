using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace SalesManagement.Web.Services;

public static class MoneyFormatter
{
    public static IHtmlContent Render(decimal value, string currencyCode) => new HtmlString(
        $"<span data-money=\"{value.ToString(CultureInfo.InvariantCulture)}\" data-currency=\"{HtmlEncoder.Default.Encode(currencyCode)}\">{HtmlEncoder.Default.Encode(Format(value, currencyCode))}</span>");
    public static string Label(string currencyCode) => currencyCode == "VND" ? "₫" : currencyCode;

    public static string Format(decimal value, string currencyCode) =>
        value.ToString(currencyCode == "VND" ? "N0" : "N2", CultureInfo.GetCultureInfo("vi-VN"))
        + " " + Label(currencyCode);
}
