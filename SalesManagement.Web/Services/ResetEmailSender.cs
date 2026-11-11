using System.Net;
using System.Net.Mail;

namespace SalesManagement.Web.Services;

public interface IResetEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string email, Uri resetLink);
}

public sealed class ResetEmailSender(IConfiguration config, IWebHostEnvironment environment) : IResetEmailSender
{
    private bool Pickup => environment.IsDevelopment() && config["Mail:Mode"] != "Smtp";
    public bool IsConfigured => Pickup ||
        (!string.IsNullOrWhiteSpace(config["Mail:Host"]) && !string.IsNullOrWhiteSpace(config["Mail:Username"]) &&
         !string.IsNullOrWhiteSpace(config["Mail:Password"]) && !string.IsNullOrWhiteSpace(config["Mail:From"]));

    public async Task SendAsync(string email, Uri resetLink)
    {
        using var message = new MailMessage(config["Mail:From"] ?? "saleshub@localhost.test", email)
        {
            Subject = "SalesHub - Đặt lại mật khẩu",
            Body = "Liên kết đặt lại mật khẩu (có hiệu lực 30 phút):\r\n" + resetLink.AbsoluteUri
                + "\r\nNếu bạn không yêu cầu, hãy bỏ qua email này.",
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8
        };
        using var client = new SmtpClient();
        if (Pickup)
        {
            var directory = Path.Combine(environment.ContentRootPath, ".local-mail");
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else
        {
            if (!IsConfigured) throw new InvalidOperationException("SMTP is not configured.");
            client.Host = config["Mail:Host"]!;
            client.Port = config.GetValue("Mail:Port", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(config["Mail:Username"], config["Mail:Password"]);
            client.Timeout = 15000;
        }
        await client.SendMailAsync(message);
    }
}
