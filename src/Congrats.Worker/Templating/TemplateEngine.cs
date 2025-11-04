using System.Collections.Concurrent;
using System.Linq;
using Congrats.Worker.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scriban;
using Scriban.Runtime;

namespace Congrats.Worker.Templating;

public interface ITemplateEngine
{
    Task<string> RenderAsync(string templateKey, IReadOnlyDictionary<string, object?> tokens, CancellationToken cancellationToken);
}

public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly AppOptions _options;
    private readonly ILogger<ScribanTemplateEngine> _logger;
    private readonly ConcurrentDictionary<string, Template> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScribanTemplateEngine(IOptions<AppOptions> options, ILogger<ScribanTemplateEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RenderAsync(string templateKey, IReadOnlyDictionary<string, object?> tokens, CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(templateKey, cancellationToken).ConfigureAwait(false);
        var context = new TemplateContext
        {
            StrictVariables = false
        };

        var scriptObject = new ScriptObject();
        foreach (var token in tokens)
        {
            scriptObject[token.Key] = token.Value;
        }

        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    private async Task<Template> LoadTemplateAsync(string templateKey, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(templateKey, out var cached))
        {
            return cached;
        }

        var path = ResolvePath(templateKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Template '{templateKey}' not found at {path}");
        }

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var template = Template.Parse(content, templateKey);
        if (template.HasErrors)
        {
            var message = string.Join(Environment.NewLine, template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template '{templateKey}' has errors: {message}");
        }

        _cache[templateKey] = template;
        _logger.LogDebug("Loaded template {TemplateKey} from {Path}", templateKey, path);
        return template;
    }

    private string ResolvePath(string templateKey)
    {
        var sanitized = templateKey.Replace('.', Path.DirectorySeparatorChar);
        var directPath = Path.Combine(AppContext.BaseDirectory, _options.Templates.BasePath, templateKey + ".html");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var path = Path.Combine(AppContext.BaseDirectory, _options.Templates.BasePath, sanitized + ".html");
        return path;
    }
}
