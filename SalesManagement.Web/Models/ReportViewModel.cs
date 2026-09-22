namespace SalesManagement.Web.Models;

public class ReportViewModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal CompletedSales { get; set; }
    public decimal RecordedPayments { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public List<TopProductViewModel> TopProducts { get; set; } = new();
}

public class TopProductViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Sales { get; set; }
}
