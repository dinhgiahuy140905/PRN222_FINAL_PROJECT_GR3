namespace SalesManagement.Web.Models;

public sealed class StoreNotificationsViewModel
{
    public bool HasStore { get; set; }
    public List<StoreNotificationItem> Items { get; } = new();
    public int TotalCount => Items.Sum(item => item.Count);
}

public sealed record StoreNotificationItem(string Text, int Count, string Controller, string Action,
    string? Status = null, string? PaymentStatus = null);
