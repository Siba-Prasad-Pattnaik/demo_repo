using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Infrastructure.External;

public class EmailService : IEmailService
{
    private readonly EmailConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailConfiguration> config,
        ILogger<EmailService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
    {
        try
        {
            using var client = CreateSmtpClient();
            using var message = new MailMessage();

            message.From = new MailAddress(_config.FromEmail, _config.FromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = isHtml;

            await client.SendMailAsync(message);

            _logger.LogInformation("Email sent successfully to: {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to: {To}", to);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, List<string> attachments, bool isHtml = false)
    {
        try
        {
            using var client = CreateSmtpClient();
            using var message = new MailMessage();

            message.From = new MailAddress(_config.FromEmail, _config.FromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = isHtml;

            // Add attachments
            foreach (var attachmentPath in attachments)
            {
                if (File.Exists(attachmentPath))
                {
                    message.Attachments.Add(new Attachment(attachmentPath));
                }
            }

            await client.SendMailAsync(message);

            _logger.LogInformation("Email with attachments sent successfully to: {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email with attachments to: {To}", to);
            return false;
        }
    }

    public async Task<bool> SendBulkEmailAsync(List<string> recipients, string subject, string body, bool isHtml = false)
    {
        var results = new List<bool>();

        foreach (var recipient in recipients)
        {
            var result = await SendEmailAsync(recipient, subject, body, isHtml);
            results.Add(result);
        }

        var successCount = results.Count(r => r);
        _logger.LogInformation("Bulk email sent: {SuccessCount}/{TotalCount}", successCount, recipients.Count);

        return successCount == recipients.Count;
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_config.SmtpHost, _config.SmtpPort)
        {
            Credentials = new NetworkCredential(_config.Username, _config.Password),
            EnableSsl = _config.EnableSSL,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        return client;
    }
}
