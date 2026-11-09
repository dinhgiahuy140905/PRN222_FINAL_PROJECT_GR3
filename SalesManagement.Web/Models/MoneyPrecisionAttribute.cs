using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models;

public sealed class MoneyPrecisionAttribute : ValidationAttribute
{
    public MoneyPrecisionAttribute() : base("Số tiền chỉ được có tối đa 2 chữ số thập phân.") { }
    public override bool IsValid(object? value) => value is not decimal amount || decimal.Round(amount, 2) == amount;
}
