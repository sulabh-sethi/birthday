using Congrats.Worker.Config;
using Congrats.Worker.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Congrats.Tests;

public class IdempotencyTests
{
    [Fact]
    public async Task SentLog_PreventsDuplicateSends()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"sentlog-{Guid.NewGuid():N}.json");
        var options = Options.Create(new AppOptions
        {
            SentLog = new AppOptions.SentLogOptions
            {
                Enabled = true,
                StoragePath = storagePath
            }
        });

        var sentLog = new SentLog(options, NullLogger<SentLog>.Instance);
        var entry = new SentLogEntry("E001", OccasionType.Birthday, new DateOnly(2024, 8, 17), DateTimeOffset.UtcNow);

        (await sentLog.AlreadySentAsync(entry.EmployeeId, entry.OccasionType, entry.Date, CancellationToken.None)).Should().BeFalse();

        await sentLog.MarkSentAsync(entry, CancellationToken.None);

        (await sentLog.AlreadySentAsync(entry.EmployeeId, entry.OccasionType, entry.Date, CancellationToken.None)).Should().BeTrue();
    }
}
