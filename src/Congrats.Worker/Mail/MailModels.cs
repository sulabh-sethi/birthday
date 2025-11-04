using Congrats.Worker.Data;

namespace Congrats.Worker.Mail;

public sealed record MailRequest(
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string HtmlBody,
    IReadOnlyCollection<CardAttachment> Attachments,
    bool IsSummary = false,
    string? SummaryBody = null);
