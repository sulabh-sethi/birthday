using System.Collections.Generic;
using Congrats.Worker.Config;
using Congrats.Worker.Templating;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Congrats.Tests;

public class TemplateEngineTests
{
    [Fact]
    public async Task RenderAsync_ReplacesTokens()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "TestTemplates");
        Directory.CreateDirectory(basePath);
        var templatePath = Path.Combine(basePath, "classic.en.html");
        await File.WriteAllTextAsync(templatePath, "Hello {{ name }} from {{ company_name }}!");

        var options = Options.Create(new AppOptions
        {
            Templates = new AppOptions.TemplateOptions
            {
                BasePath = "TestTemplates",
                DefaultTemplate = "classic.en"
            }
        });

        var engine = new ScribanTemplateEngine(options, NullLogger<ScribanTemplateEngine>.Instance);

        var html = await engine.RenderAsync("classic.en", new Dictionary<string, object?>
        {
            ["name"] = "Aditi",
            ["company_name"] = "Contoso"
        }, CancellationToken.None);

        html.Should().Contain("Hello Aditi from Contoso!");
    }

    [Fact]
    public async Task RenderAsync_Throws_WhenTemplateMissing()
    {
        var options = Options.Create(new AppOptions
        {
            Templates = new AppOptions.TemplateOptions
            {
                BasePath = "NonExisting",
                DefaultTemplate = "classic.en"
            }
        });

        var engine = new ScribanTemplateEngine(options, NullLogger<ScribanTemplateEngine>.Instance);

        await Assert.ThrowsAsync<FileNotFoundException>(() => engine.RenderAsync("classic.en", new Dictionary<string, object?>(), CancellationToken.None));
    }
}
