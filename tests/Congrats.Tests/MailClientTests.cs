using Congrats.Worker.Config;
using Congrats.Worker.Data;
using Congrats.Worker.Mail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Congrats.Tests;

public class MailClientTests
{
    [Fact]
    public async Task SendAsync_WritesEmlFile_InDryRunMode()
    {
        var output = Path.Combine(Path.GetTempPath(), $"dry-{Guid.NewGuid():N}");
        var options = Options.Create(new AppOptions
        {
            Mail = new AppOptions.MailOptions
            {
                From = "hr@example.com",
                DisplayName = "HR",
                SmtpHost = "smtp.example.com",
                Username = "user",
                Password = "pass"
            },
            DryRun = new AppOptions.DryRunOptions
            {
                Enabled = true,
                OutputDirectory = output
            }
        });

        var client = new MailClient(options, NullLogger<MailClient>.Instance);
        var request = new MailRequest(
            RecipientEmail: "user@example.com",
            RecipientName: "User",
            Subject: "Hello",
            HtmlBody: "<p>Hello</p>",
            Attachments: Array.Empty<CardAttachment>());

        await client.SendAsync(request, CancellationToken.None);

        Directory.Exists(output).Should().BeTrue();
        Directory.GetFiles(output, "*.eml").Should().HaveCount(1);
    }
}
