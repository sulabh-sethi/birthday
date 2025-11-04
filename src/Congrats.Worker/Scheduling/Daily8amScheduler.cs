using Congrats.Worker.Config;
using Congrats.Worker.Data;
using Congrats.Worker.Mail;
using Congrats.Worker.Rendering;
using Congrats.Worker.Templating;
using Congrats.Worker.Utils;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Scheduling;

public sealed class Daily8amScheduler : BackgroundService
{
    private readonly ILogger<Daily8amScheduler> _logger;
    private readonly AppOptions _options;
    private readonly IDateTimeProvider _clock;
    private readonly IExcelReader _excelReader;
    private readonly OccasionMatcher _matcher;
    private readonly ITemplateEngine _templateEngine;
    private readonly ICardRenderer _cardRenderer;
    private readonly IMailClient _mailClient;
    private readonly SentLog _sentLog;
    private readonly TimeZoneInfo _timeZone;
    private readonly CronExpression _cronExpression;

    public Daily8amScheduler(
        ILogger<Daily8amScheduler> logger,
        IOptions<AppOptions> options,
        IDateTimeProvider clock,
        IExcelReader excelReader,
        OccasionMatcher matcher,
        ITemplateEngine templateEngine,
        ICardRenderer cardRenderer,
        IMailClient mailClient,
        SentLog sentLog)
    {
        _logger = logger;
        _options = options.Value;
        _clock = clock;
        _excelReader = excelReader;
        _matcher = matcher;
        _templateEngine = templateEngine;
        _cardRenderer = cardRenderer;
        _mailClient = mailClient;
        _sentLog = sentLog;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.Scheduler.TimeZone);
        _cronExpression = CronExpression.Parse(_options.Scheduler.Cron, CronFormat.IncludeSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Congratulatory service started. Scheduling enabled: {Enabled}", _options.Scheduler.Enabled);

        if (!_options.Scheduler.Enabled)
        {
            _logger.LogWarning("Scheduler disabled. The service will not send emails automatically.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = _clock.UtcNow;
            var nextOccurrence = _cronExpression.GetNextOccurrence(utcNow, _timeZone);
            if (nextOccurrence is null)
            {
                _logger.LogWarning("Cron expression returned no further occurrences. Stopping scheduler.");
                break;
            }

            var delay = nextOccurrence.Value - utcNow;
            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next run scheduled at {NextRun} ({TimeZone})", nextOccurrence, _timeZone.DisplayName);
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            await RunDailyAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunDailyAsync(CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        _logger.LogInformation("Starting daily congratulation run {RunId}", runId);

        var readResult = await _excelReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (readResult.HasErrors)
        {
            foreach (var error in readResult.Errors)
            {
                _logger.LogWarning("Excel row {Row} error: {Message}", error.RowNumber, error.Message);
            }
        }

        var today = DateHelpers.ConvertToTimeZone(_clock.UtcNow, _timeZone);
        var matches = _matcher.MatchOccasions(readResult.People, today);
        _logger.LogInformation("Found {Count} matches for {Date}", matches.Count, today);

        var sent = new List<OccasionMatch>();
        var skipped = new List<OccasionMatch>();

        foreach (var match in matches)
        {
            if (await _sentLog.AlreadySentAsync(match.Person.EmployeeId, match.OccasionType, today, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Skipping {Employee} for {Occasion} as already sent", match.Person.EmployeeId, match.OccasionType);
                skipped.Add(match);
                continue;
            }

            try
            {
                var html = await _templateEngine.RenderAsync(match.TemplateKey, match.Tokens, cancellationToken).ConfigureAwait(false);
                var attachments = await _cardRenderer.RenderAsync(match, html, cancellationToken).ConfigureAwait(false);

                var request = new MailRequest(
                    RecipientEmail: match.Person.Email,
                    RecipientName: match.Person.FullName,
                    Subject: match.Subject,
                    HtmlBody: html,
                    Attachments: attachments);

                await _mailClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                await _sentLog.MarkSentAsync(new SentLogEntry(match.Person.EmployeeId, match.OccasionType, today, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
                sent.Add(match);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {Employee} for {Occasion}", match.Person.EmployeeId, match.OccasionType);
            }
        }

        await SendSummaryAsync(sent, skipped, today, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Run {RunId} completed. Sent {SentCount} notifications, skipped {SkippedCount}", runId, sent.Count, skipped.Count);
    }

    private async Task SendSummaryAsync(IReadOnlyCollection<OccasionMatch> sent, IReadOnlyCollection<OccasionMatch> skipped, DateOnly today, CancellationToken cancellationToken)
    {
        if (!_options.Notifications.SendSummaryEmail)
        {
            return;
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"Daily Congratulation Report — {today:yyyy-MM-dd}");
        builder.AppendLine($"Sent: {sent.Count}");
        builder.AppendLine($"Skipped: {skipped.Count}");
        builder.AppendLine();

        foreach (var match in sent)
        {
            builder.AppendLine($"✔ {match.Person.FullName} — {match.OccasionType}");
        }

        foreach (var match in skipped)
        {
            builder.AppendLine($"⚠ {match.Person.FullName} — {match.OccasionType} (duplicate)");
        }

        await _mailClient.SendSummaryAsync(
            subject: $"Congratulation report for {today:yyyy-MM-dd}",
            htmlBody: builder.ToString().Replace("\n", "<br />"),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
