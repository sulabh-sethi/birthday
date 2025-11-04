using System.ComponentModel.DataAnnotations;

namespace Congrats.Worker.Config;

public sealed class AppOptions
{
    public const string SectionName = "CongratulatoryService";

    public ExcelOptions Excel { get; init; } = new();
    public MailOptions Mail { get; init; } = new();
    public TemplateOptions Templates { get; init; } = new();
    public SchedulerOptions Scheduler { get; init; } = new();
    public RenderingOptions Rendering { get; init; } = new();
    public DryRunOptions DryRun { get; init; } = new();
    public SentLogOptions SentLog { get; init; } = new();
    public NotificationsOptions Notifications { get; init; } = new();
    public OccasionOptions Occasions { get; init; } = new();

    public sealed class ExcelOptions
    {
        public string FilePath { get; init; } = "samples/people.xlsx";
        public string SheetName { get; init; } = "People";
        public bool Optional { get; init; }
        public bool WatchForChanges { get; init; } = true;
    }

    public sealed class MailOptions
    {
        [EmailAddress]
        public string From { get; init; } = string.Empty;
        public string DisplayName { get; init; } = "HR Bot";
        public string SmtpHost { get; init; } = string.Empty;
        public int Port { get; init; } = 587;
        public bool UseSsl { get; init; } = true;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public bool EnableDryRunAttachments { get; init; }
        public int MaxRetryAttempts { get; init; } = 3;
        public TimeSpan RetryBackoff { get; init; } = TimeSpan.FromSeconds(10);
    }

    public sealed class TemplateOptions
    {
        public string DefaultTemplate { get; init; } = "classic.en";
        public string BasePath { get; init; } = "Templating/Templates";
        public Dictionary<string, string> TemplateAliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string CompanyName { get; init; } = "Contoso";
        public string Signature { get; init; } = "People Operations";
        public string CardImageUrl { get; init; } = string.Empty;
    }

    public sealed class SchedulerOptions
    {
        public string Cron { get; init; } = "0 0 8 * * ?";
        public string TimeZone { get; init; } = "Asia/Kolkata";
        public bool Enabled { get; init; } = true;
    }

    public sealed class RenderingOptions
    {
        public bool Enabled { get; init; }
        public string OutputDirectory { get; init; } = "out/cards";
        public string Format { get; init; } = "html";
    }

    public sealed class DryRunOptions
    {
        public bool Enabled { get; init; }
        public string OutputDirectory { get; init; } = "out/dry-run";
    }

    public sealed class SentLogOptions
    {
        public string StoragePath { get; init; } = "data/sent-log.json";
        public bool Enabled { get; init; } = true;
    }

    public sealed class NotificationsOptions
    {
        public bool SendSummaryEmail { get; init; }
        public string SummaryRecipient { get; init; } = string.Empty;
    }

    public sealed class OccasionOptions
    {
        public LeapDayPolicy LeapDayPolicy { get; init; } = LeapDayPolicy.Feb28;
    }
}

public enum LeapDayPolicy
{
    Feb28,
    Mar01,
    Exact
}
