using System.Text;
using Congrats.Worker.Config;
using Congrats.Worker.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Rendering;

public interface ICardRenderer
{
    Task<IReadOnlyCollection<CardAttachment>> RenderAsync(OccasionMatch match, string htmlContent, CancellationToken cancellationToken);
}

public sealed class CardRenderer : ICardRenderer
{
    private readonly AppOptions _options;
    private readonly ILogger<CardRenderer> _logger;

    public CardRenderer(IOptions<AppOptions> options, ILogger<CardRenderer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<CardAttachment>> RenderAsync(OccasionMatch match, string htmlContent, CancellationToken cancellationToken)
    {
        if (!_options.Rendering.Enabled)
        {
            return Task.FromResult<IReadOnlyCollection<CardAttachment>>(Array.Empty<CardAttachment>());
        }

        Directory.CreateDirectory(_options.Rendering.OutputDirectory);
        var fileName = $"{match.Person.EmployeeId}_{match.OccasionDate:yyyyMMdd}_{match.OccasionType}.html";
        var path = Path.Combine(_options.Rendering.OutputDirectory, fileName);
        File.WriteAllText(path, htmlContent, Encoding.UTF8);

        _logger.LogInformation("Rendered card for {EmployeeId} at {Path}", match.Person.EmployeeId, path);

        var attachment = new CardAttachment(fileName, Encoding.UTF8.GetBytes(htmlContent), "text/html");
        return Task.FromResult<IReadOnlyCollection<CardAttachment>>(new[] { attachment });
    }
}
