using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Config;

public static class OptionsBuilderExtensions
{
    public static OptionsBuilder<TOptions> ValidateFluently<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(sp =>
        {
            var validators = sp.GetServices<IValidator<TOptions>>().ToArray();
            return new FluentValidateOptions<TOptions>(builder.Name, validators);
        });
        return builder;
    }

    private sealed class FluentValidateOptions<TOptions> : IValidateOptions<TOptions>
    {
        private readonly string _name;
        private readonly IValidator<TOptions>[] _validators;

        public FluentValidateOptions(string name, IValidator<TOptions>[] validators)
        {
            _name = name;
            _validators = validators;
        }

        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            if (_validators.Length == 0)
            {
                return ValidateOptionsResult.Skip;
            }

            if (_name != Options.DefaultName && _name != name)
            {
                return ValidateOptionsResult.Skip;
            }

            var failures = _validators
                .Select(v => v.Validate(options))
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .Select(failure => failure!.ErrorMessage)
                .ToArray();

            return failures.Length > 0
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }
    }
}

public sealed class AppOptionsValidator : AbstractValidator<AppOptions>
{
    public AppOptionsValidator()
    {
        RuleFor(o => o.Excel.FilePath).NotEmpty();
        RuleFor(o => o.Mail.From).NotEmpty().EmailAddress();
        RuleFor(o => o.Mail.SmtpHost).NotEmpty();
        RuleFor(o => o.Mail.Port).InclusiveBetween(1, 65535);
        RuleFor(o => o.Scheduler.Cron).NotEmpty();
        RuleFor(o => o.Scheduler.TimeZone).NotEmpty();
        RuleFor(o => o.Occasions.LeapDayPolicy).IsInEnum();
        When(o => o.Notifications.SendSummaryEmail, () =>
        {
            RuleFor(o => o.Notifications.SummaryRecipient).NotEmpty().EmailAddress();
        });
        When(o => o.Rendering.Enabled, () =>
        {
            RuleFor(o => o.Rendering.OutputDirectory).NotEmpty();
        });
        When(o => o.DryRun.Enabled, () =>
        {
            RuleFor(o => o.DryRun.OutputDirectory).NotEmpty();
        });
        When(o => o.SentLog.Enabled, () =>
        {
            RuleFor(o => o.SentLog.StoragePath).NotEmpty();
        });
    }
}
