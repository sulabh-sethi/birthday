using Congrats.Worker.Config;
using Congrats.Worker.Data;
using Congrats.Worker.Mail;
using Congrats.Worker.Rendering;
using Congrats.Worker.Scheduling;
using Congrats.Worker.Templating;
using Congrats.Worker.Utils;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

ConfigureConfiguration(builder.Configuration, builder.Environment);
ConfigureLogging(builder);
ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();
await host.RunAsync();

static void ConfigureConfiguration(ConfigurationManager configuration, IHostEnvironment environment)
{
    configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
        .AddJsonFile("serilog.json", optional: true)
        .AddEnvironmentVariables();
}

static void ConfigureLogging(HostApplicationBuilder builder)
{
    var loggerConfiguration = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    if (!builder.Environment.IsDevelopment())
    {
        loggerConfiguration.MinimumLevel.Override("Microsoft", LogEventLevel.Information);
    }

    Log.Logger = loggerConfiguration.CreateLogger();
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<AppOptions>()
        .Bind(configuration.GetSection(AppOptions.SectionName))
        .ValidateFluently();

    services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
    services.AddSingleton<IExcelReader, ExcelReader>();
    services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
    services.AddSingleton<ICardRenderer, CardRenderer>();
    services.AddSingleton<IMailClient, MailClient>();
    services.AddSingleton<OccasionMatcher>();
    services.AddSingleton<SentLog>();
    services.AddHttpClient();

    services.AddHostedService<Daily8amScheduler>();
    services.AddValidatorsFromAssemblyContaining<AppOptionsValidator>();
}
