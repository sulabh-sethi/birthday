namespace Congrats.Worker.Data;

[Flags]
public enum OccasionType
{
    None = 0,
    Birthday = 1,
    WorkAnniversary = 2,
    BirthdayAndAnniversary = Birthday | WorkAnniversary
}

public sealed record Person(
    string EmployeeId,
    string FullName,
    string Email,
    DateOnly DateOfBirth,
    DateOnly DateOfJoining,
    string? PreferredTemplate,
    string? Department,
    string? Location,
    string? Language,
    IReadOnlyDictionary<string, string?> AdditionalData);

public sealed record OccasionMatch(
    Person Person,
    OccasionType OccasionType,
    DateOnly OccasionDate,
    int? YearsCompleted,
    string TemplateKey,
    string Subject,
    string Language,
    IReadOnlyDictionary<string, object?> Tokens);

public sealed record CardAttachment(string FileName, byte[] Content, string MimeType);

public sealed record CardPayload(string HtmlContent, IReadOnlyCollection<CardAttachment> Attachments);

public sealed record SentLogEntry(string EmployeeId, OccasionType OccasionType, DateOnly Date, DateTimeOffset SentAtUtc);
