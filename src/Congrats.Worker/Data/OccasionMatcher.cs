using System.Linq;
using Congrats.Worker.Config;
using Congrats.Worker.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Data;

public sealed class OccasionMatcher
{
    private readonly AppOptions _options;
    private readonly ILogger<OccasionMatcher> _logger;

    public OccasionMatcher(IOptions<AppOptions> options, ILogger<OccasionMatcher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyCollection<OccasionMatch> MatchOccasions(IEnumerable<Person> people, DateOnly today)
    {
        var matches = new List<OccasionMatch>();
        foreach (var person in people)
        {
            var matchType = OccasionType.None;
            var yearsCompleted = (int?)null;

            if (DateHelpers.IsBirthday(today, person.DateOfBirth, _options.Occasions.LeapDayPolicy))
            {
                matchType |= OccasionType.Birthday;
            }

            if (DateHelpers.IsWorkAnniversary(today, person.DateOfJoining))
            {
                var years = DateHelpers.YearsCompleted(today, person.DateOfJoining);
                if (years > 0)
                {
                    matchType |= OccasionType.WorkAnniversary;
                    yearsCompleted = years;
                }
            }

            if (matchType == OccasionType.None)
            {
                continue;
            }

            var language = person.Language?.TrimToNull() ?? "en";
            var templateKey = ResolveTemplateKey(person, language);
            var subject = BuildSubject(person.FullName, matchType, yearsCompleted);
            var tokens = BuildTokens(person, today, matchType, yearsCompleted, language);

            matches.Add(new OccasionMatch(person, matchType, today, yearsCompleted, templateKey, subject, language, tokens));
        }

        _logger.LogInformation("Matched {Count} occasions for {Date}", matches.Count, today);
        return matches;
    }

    private string ResolveTemplateKey(Person person, string language)
    {
        var template = person.PreferredTemplate?.TrimToNull() ?? _options.Templates.DefaultTemplate;
        if (_options.Templates.TemplateAliases.TryGetValue(template, out var alias))
        {
            template = alias;
        }

        if (!template.EndsWith($".{language}", StringComparison.OrdinalIgnoreCase))
        {
            template = $"{template}.{language}";
        }

        return template;
    }

    private static string BuildSubject(string fullName, OccasionType occasionType, int? yearsCompleted)
    {
        return occasionType switch
        {
            OccasionType.Birthday => $"Happy Birthday, {fullName}!",
            OccasionType.WorkAnniversary => yearsCompleted.HasValue
                ? $"Happy {yearsCompleted}-Year Work Anniversary, {fullName}!"
                : $"Happy Work Anniversary, {fullName}!",
            OccasionType.BirthdayAndAnniversary => yearsCompleted.HasValue
                ? $"Celebrating {fullName}: Birthday & {yearsCompleted}-Year Anniversary!"
                : $"Celebrating {fullName}: Birthday & Work Anniversary!",
            _ => $"Celebrations for {fullName}"
        };
    }

    private IReadOnlyDictionary<string, object?> BuildTokens(
        Person person,
        DateOnly today,
        OccasionType occasionType,
        int? yearsCompleted,
        string language)
    {
        var tokens = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = person.FullName,
            ["first_name"] = person.FullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? person.FullName,
            ["email"] = person.Email,
            ["department"] = person.Department,
            ["location"] = person.Location,
            ["language"] = language,
            ["today"] = today.ToString("yyyy-MM-dd"),
            ["occasion"] = DescribeOccasion(occasionType, yearsCompleted),
            ["years_completed"] = yearsCompleted,
            ["company_name"] = _options.Templates.CompanyName,
            ["signature"] = _options.Templates.Signature,
            ["card_image_url"] = _options.Templates.CardImageUrl,
            ["subject"] = BuildSubject(person.FullName, occasionType, yearsCompleted)
        };

        foreach (var pair in person.AdditionalData)
        {
            tokens[pair.Key] = pair.Value;
        }

        return tokens;
    }

    private static string DescribeOccasion(OccasionType occasionType, int? yearsCompleted)
    {
        return occasionType switch
        {
            OccasionType.Birthday => "Birthday",
            OccasionType.WorkAnniversary => yearsCompleted.HasValue
                ? $"Work Anniversary ({yearsCompleted} years)"
                : "Work Anniversary",
            OccasionType.BirthdayAndAnniversary => yearsCompleted.HasValue
                ? $"Birthday & Work Anniversary ({yearsCompleted} years)"
                : "Birthday & Work Anniversary",
            _ => "Celebration"
        };
    }
}
