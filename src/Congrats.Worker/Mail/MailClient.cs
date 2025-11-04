using System.Linq;
using System.Text;
using Congrats.Worker.Config;
using Congrats.Worker.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Congrats.Worker.Mail;

public interface IMailClient
{
    Task SendAsync(MailRequest request, CancellationToken cancellationToken);
    Task SendSummaryAsync(string subject, string htmlBody, CancellationToken cancellationToken);
}

public sealed class MailClient : IMailClient
{
    private readonly AppOptions _options;
    private readonly ILogger<MailClient> _logger;

    public MailClient(IOptions<AppOptions> options, ILogger<MailClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendSummaryAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!_options.Notifications.SendSummaryEmail)
        {
            _logger.LogDebug("Summary notifications disabled");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.Notifications.SummaryRecipient))
        {
            _logger.LogWarning("Summary recipient not configured. Skipping summary email.");
            return Task.CompletedTask;
        }

        var request = new MailRequest(
            RecipientEmail: _options.Notifications.SummaryRecipient,
            RecipientName: "HR Team",
            Subject: subject,
            HtmlBody: htmlBody,
            Attachments: Array.Empty<CardAttachment>(),
            IsSummary: true,
            SummaryBody: htmlBody);

        return SendAsync(request, cancellationToken);
    }

    public async Task SendAsync(MailRequest request, CancellationToken cancellationToken)
    {
        if (_options.DryRun.Enabled)
        {
            await SaveDryRunAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var attempts = Math.Max(1, _options.Mail.MaxRetryAttempts);
        Exception? lastException = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var message = BuildMessage(request);
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_options.Mail.SmtpHost, _options.Mail.Port, _options.Mail.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(_options.Mail.Username))
                {
                    await smtp.AuthenticateAsync(_options.Mail.Username, _options.Mail.Password, cancellationToken).ConfigureAwait(false);
                }

                await smtp.SendAsync(message, cancellationToken).ConfigureAwait(false);
                await smtp.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Sent email to {Recipient}", request.RecipientEmail);
                return;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                _logger.LogWarning(ex, "Failed to send email to {Recipient} on attempt {Attempt}/{Attempts}", request.RecipientEmail, attempt, attempts);
                await Task.Delay(_options.Mail.RetryBackoff, cancellationToken).ConfigureAwait(false);
                lastException = ex;
            }
        }

        throw new InvalidOperationException($"Unable to send email to {request.RecipientEmail} after {attempts} attempts", lastException);
    }

    private MimeMessage BuildMessage(MailRequest request)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.Mail.DisplayName, _options.Mail.From));
        message.To.Add(new MailboxAddress(request.RecipientName, request.RecipientEmail));
        message.Subject = request.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = request.HtmlBody
        };

        foreach (var attachment in request.Attachments)
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.MimeType));
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private async Task SaveDryRunAsync(MailRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.DryRun.OutputDirectory);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"{timestamp}_{SanitizeFileName(request.RecipientEmail)}.eml";
        var path = Path.Combine(_options.DryRun.OutputDirectory, fileName);

        var message = BuildMessage(request);
        await using var stream = File.Create(path);
        await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);

        if (_options.Mail.EnableDryRunAttachments && request.Attachments.Count > 0)
        {
            foreach (var attachment in request.Attachments)
            {
                var attachmentPath = Path.Combine(_options.DryRun.OutputDirectory, $"{timestamp}_{attachment.FileName}");
                await File.WriteAllBytesAsync(attachmentPath, attachment.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Dry-run email saved to {Path}", path);
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }
}
