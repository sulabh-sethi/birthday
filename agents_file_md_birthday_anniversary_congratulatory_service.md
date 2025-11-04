# 🎉 Congratulatory Service — Agents File (for Codex)

> **Goal:** A production‑ready .NET 8 background service that reads employee birthdays & work anniversaries from an Excel file, generates a personalized greeting card (HTML email + optional image/PDF), and sends it daily at **08:00 Asia/Kolkata**.

---

## 1) High‑Level Overview
- **Inputs:** Excel workbook with employee data (birthdays, anniversaries, email, etc.).
- **Outputs:** Personalized greeting emails with a themed card (inline HTML + optional image/PDF attachment).
- **Schedule:** Every day at **08:00 IST (Asia/Kolkata)**.
- **Runtime:** .NET 8 Worker Service (containerizable). Runs on Windows/Linux; deploy as Windows Service, Linux systemd, or container.
- **Templating:** Tokenized HTML templates (Scriban/RazorLight). Theme + per‑recipient variables.
- **Mail:** SMTP (MailKit) or provider (e.g., SES/SendGrid/ACS), selected via config.
- **Logging:** Serilog to console + rolling files + optional Seq/Grafana Loki.
- **Config:** `appsettings.json` + environment variables + secrets store.

---

## 2) Excel Data Contract (Minimal Schema)
Provide one sheet named `People` (configurable). Columns (case‑insensitive, header row required):

| Column | Required | Type | Notes |
|---|---|---|---|
| `EmployeeId` | ✅ | string | Unique ID used to dedupe & audit. |
| `FullName` | ✅ | string | Used in greeting. |
| `Email` | ✅ | string | Delivery address. |
| `DateOfBirth` | ✅ | date | `yyyy-mm-dd` or Excel date. Leap‑year handling (see Edge Cases). |
| `DateOfJoining` | ✅ | date | Used for work anniversaries. |
| `PreferredTemplate` | ❌ | string | e.g., `classic`, `festive`, `minimal`. |
| `Department` | ❌ | string | Used for personalization/tone. |
| `Location` | ❌ | string | Optional; can inform language/locale. |
| `Language` | ❌ | string | e.g., `en`, `hi`. Selects localized template if present. |

**Optional multi‑sheet:** Allowed. Configure `SheetName` in settings. Additional attributes may be piped to templates as tokens.

---

## 3) Project Structure
```
CongratsService/
├─ src/
│  ├─ Congrats.Worker/                # .NET 8 Worker Service (BackgroundService)
│  │  ├─ Program.cs
│  │  ├─ Scheduling/
│  │  │  └─ Daily8amScheduler.cs
│  │  ├─ Data/
│  │  │  ├─ ExcelReader.cs
│  │  │  └─ Models.cs                 # Person, Occasion, CardPayload
│  │  ├─ Templating/
│  │  │  ├─ TemplateEngine.cs         # Scriban/RazorLight abstraction
│  │  │  └─ Templates/
│  │  │     ├─ classic.en.html
│  │  │     ├─ festive.en.html
│  │  │     └─ minimal.en.html
│  │  ├─ Rendering/
│  │  │  └─ CardRenderer.cs           # optional: SkiaSharp/DinkToPdf
│  │  ├─ Mail/
│  │  │  ├─ MailClient.cs             # SMTP/Provider abstraction
│  │  │  └─ MailModels.cs
│  │  ├─ Config/
│  │  │  ├─ AppOptions.cs
│  │  │  └─ Validation.cs
│  │  ├─ Utils/
│  │  │  └─ DateHelpers.cs
│  │  ├─ appsettings.json
│  │  ├─ appsettings.Development.json
│  │  └─ serilog.json
│  └─ Congrats.Contracts/             # DTOs, shared constants (optional)
│
├─ tests/
│  ├─ Congrats.Tests/
│  │  ├─ ExcelReaderTests.cs
│  │  ├─ DateHelpersTests.cs
│  │  ├─ TemplateEngineTests.cs
│  │  ├─ CardRendererSnapshotTests.cs
│  │  └─ MailClientTests.cs
│
├─ samples/
│  ├─ people.xlsx                     # sample Excel
│  └─ appsettings.sample.json         # sample config
│
├─ Dockerfile
├─ .editorconfig
├─ .gitattributes
├─ .gitignore
└─ .github/workflows/ci.yml
```

---

## 4) Recommended Packages (NuGet)
- **Excel:** `ClosedXML` (simple & robust) *(or `EPPlus`)*
- **Templating:** `Scriban` *(lightweight)* **or** `RazorLight` (Razor syntax)
- **Mail:** `MailKit` (SMTP) *(plus provider SDKs if needed)*
- **Scheduling:** `Quartz` **or** `Cronos` + `PeriodicTimer` for daily trigger
- **Logging:** `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File` *(optional: Seq sink)*
- **Config validation:** `FluentValidation` *(validate `AppOptions`)*
- **Rendering (optional):** `SkiaSharp` (image), `DinkToPdf`/`QuestPDF` (PDF)
- **Testing:** `xunit`, `FluentAssertions`, `NSubstitute`/`Moq`, `Verify` (snapshots), `Bogus` (fake data)

---

## 5) Scheduling @ 08:00 Asia/Kolkata
- Use **Quartz** with time‑zone aware trigger:
  - Cron: `0 0 8 * * ?` with `TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")`.
- Alternatively, compute next 08:00 IST in `BackgroundService` loop using `Cronos` and `await Task.Delay(nextRun - now)`.
- **Idempotency:** Persist a daily `RunId` to avoid duplicates on restarts (e.g., disk file/SQLite/Redis). Write a `SentLog` (EmployeeId+Occasion+Date).

---

## 6) Occasion Rules
- **Birthday:** `matches(MM-dd)` with leap‑day policy:
  - If `DateOfBirth` is `Feb 29`, send on **Feb 29** in leap years, **Feb 28** otherwise (configurable: `Feb28` or `Mar01`).
- **Work Anniversary:** Use `DateOfJoining`. Compute years completed as `years = now.Year - doj.Year` (adjust if anniversary not reached yet this year). Skip year 0.
- **Multiple matches same day:** Send one combined mail with both sections (Birthday + Anniversary) using a combined template.

---

## 7) Personalization & Templates
- **Engine:** Default **Scriban** (tokens: `{{ name }}`, `{{ years }}`, `{{ department }}`, custom `{{ quote }}` etc.).
- **Template selection order:**
  1. `PreferredTemplate` from Excel if available
  2. Department‑specific override (e.g., `sales.en.html`)
  3. Default theme from config (e.g., `classic.en.html`)
- **Tokens available:**
  - `name`, `first_name`, `department`, `location`, `email`, `today`, `occasion` (`birthday`/`anniversary`), `years_completed` (for anniversary), `company_name`, `signature`, `card_image_url` (if you host static assets).
- **Assets:** Put inline CSS in the HTML template for email‑client compatibility. CID‑embed images through `MailKit` if required.
- **Localization:** Suffix templates with `.en`, `.hi`, etc. Auto‑pick by `Language` or default.

**Sample (Scriban) HTML Extract:**
```html
<!DOCTYPE html>
<html>
  <body style="font-family: system-ui, -apple-system, Segoe UI, Roboto; background:#faf7f2; margin:0; padding:24px;">
    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:640px;margin:auto;background:#ffffff;border-radius:12px;box-shadow:0 4px 24px rgba(0,0,0,.06);">
      <tr><td style="padding:32px 32px 16px;">
        <h1 style="margin:0 0 8px;">Happy {{ if occasion == "birthday" }}Birthday{{ else }}Work Anniversary{{ end }}, {{ first_name }}! 🎉</h1>
        {{ if occasion == "anniversary" }}
          <p>Congratulations on completing <strong>{{ years_completed }}</strong> {{ if years_completed == 1 }}year{{ else }}years{{ end }} with {{ company_name }}.</p>
        {{ end }}
        <p>Wishing you a wonderful day ahead. — {{ signature }}</p>
      </td></tr>
    </table>
  </body>
</html>
```

---

## 8) Mail Delivery
- **SMTP:** Host, Port, SSL, Username/Password from `appsettings.json` or environment.
- **Providers:** Switchable via `AppOptions:Mail:Provider`. Implement `IMailClient` with provider‑specific clients.
- **From/Reply‑To:** Configurable; DKIM/SPF recommended at domain level.
- **Retries:** Exponential backoff on transient failures. Quarantine hard bounces.
- **Preview Mode:** `DryRun=true` writes EML/HTML files to `/outbox` without sending.

---

## 9) Optional Card Rendering (Attachment)
- **Image (PNG/JPEG):** Use `SkiaSharp` to render dynamic text over a background. Export as `card_{employeeId}_{date}.png` and embed as inline attachment with CID.
- **PDF:** Use `QuestPDF` or `DinkToPdf` to convert the same HTML to PDF.
- **Snapshot tests:** `Verify` to ensure visual stability of generated cards/templates.

---

## 10) Configuration & Secrets
**`appsettings.json` (sample):**
```json
{
  "AppOptions": {
    "TimeZone": "Asia/Kolkata",
    "RunAtLocalTime": "08:00:00",
    "Excel": {
      "Path": "./samples/people.xlsx",
      "SheetName": "People",
      "DateFormat": "yyyy-MM-dd"
    },
    "Occasions": { "EnableBirthdays": true, "EnableAnniversaries": true, "LeapDayPolicy": "Feb28" },
    "Mail": {
      "Provider": "Smtp",
      "From": "HR Team <hr@example.com>",
      "Smtp": { "Host": "smtp.example.com", "Port": 587, "UseStartTls": true, "User": "smtp_user", "Password": "__from_secrets__" }
    },
    "Templating": { "Engine": "Scriban", "DefaultTemplate": "classic.en.html", "TemplatesPath": "./src/Congrats.Worker/Templating/Templates" },
    "Rendering": { "EnableImage": false, "EnablePdf": false, "OutDir": "./out" },
    "DryRun": false,
    "Persistence": { "SentLog": "./state/sentlog.sqlite" }
  },
  "Serilog": { "MinimumLevel": "Information" }
}
```

**Secrets:** Use `.NET user-secrets` locally and environment variables in CI/CD. Never commit credentials.

---

## 11) Best Practices Checklist
- [x] Time‑zone accurate scheduling with DST‑safe math (IST has no DST, but code should be TZ‑aware).
- [x] Idempotent sending (SentLog ensures no duplicate mails per person/occasion/date).
- [x] Robust parsing of Excel (trim headers, flexible date parsing, explicit error reports with row/column references).
- [x] HTML email tested on major clients (Gmail, Outlook, Apple Mail). Inline CSS. Avoid heavy external assets.
- [x] Retry/transient fault handling for SMTP.
- [x] Structured logging with correlation IDs per run.
- [x] Config validation at startup (fail fast with actionable errors).
- [x] Feature flags: DryRun, EnableBirthdays/Anniversaries, Rendering toggles.
- [x] Telemetry hooks (OpenTelemetry) optional.

---

## 12) Edge Cases
- **Leap year birthdays.**
- **Missing/invalid emails** → skip with error in report.
- **Multiple people with same email** → send separate mails unless configured to group.
- **People without `EmployeeId`** → auto‑generate hash of name+email (warn).
- **Anniversary year 0** (same year join) → skip or special wording (configurable).
- **Template not found** → fallback to default and log warning.

---

## 13) Build & Run
```bash
# Create solution
mkdir CongratsService && cd CongratsService
mkdir src tests samples

# Worker
dotnet new worker -n Congrats.Worker -o src/Congrats.Worker

# Tests
dotnet new xunit -n Congrats.Tests -o tests/Congrats.Tests

dotnet new sln -n CongratsService

# Add to solution
dotnet sln add src/Congrats.Worker/Congrats.Worker.csproj
dotnet sln add tests/Congrats.Tests/Congrats.Tests.csproj

# Packages (core)
dotnet add src/Congrats.Worker package ClosedXML
dotnet add src/Congrats.Worker package Scriban
dotnet add src/Congrats.Worker package MailKit
dotnet add src/Congrats.Worker package Quartz
dotnet add src/Congrats.Worker package Serilog Serilog.Sinks.Console Serilog.Sinks.File
(dotnet add src/Congrats.Worker package SkiaSharp)             # optional image
(dotnet add src/Congrats.Worker package QuestPDF)              # optional PDF

# Test packages
dotnet add tests/Congrats.Tests package FluentAssertions
dotnet add tests/Congrats.Tests package NSubstitute
(dotnet add tests/Congrats.Tests package Verify.Xunit)         # snapshot tests

# Restore & build
dotnet restore
dotnet build -c Release

# Run (Development)
dotnet run --project src/Congrats.Worker
```

---

## 14) Minimal Code Sketches
**`Program.cs`**
```csharp
using Congrats.Worker.Config;
using Congrats.Worker.Scheduling;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("AppOptions"));
// register: ExcelReader, TemplateEngine, MailClient, CardRenderer, SentLogStore, etc.

builder.Services.AddHostedService<Daily8amScheduler>();

var app = builder.Build();
await app.RunAsync();
```

**`Daily8amScheduler.cs`**
```csharp
public class Daily8amScheduler : BackgroundService
{
    private readonly ILogger<Daily8amScheduler> _log;
    private readonly IServiceProvider _sp;
    private readonly AppOptions _opts;

    public Daily8amScheduler(ILogger<Daily8amScheduler> log, IOptions<AppOptions> opts, IServiceProvider sp)
    { _log = log; _sp = sp; _opts = opts.Value; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_opts.TimeZone);
        while (!ct.IsCancellationRequested)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var todayRun = now.Date + TimeSpan.Parse(_opts.RunAtLocalTime);
            var nextRun = now <= todayRun ? todayRun : todayRun.AddDays(1);
            var delay = nextRun - now;
            _log.LogInformation("Next run at {NextRun}", nextRun);
            await Task.Delay(delay, ct);

            using var scope = _sp.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<DailyJob>();
            await job.RunAsync(ct);
        }
    }
}
```

**`DailyJob.RunAsync` (pseudo):**
```csharp
var people = await _excel.ReadAsync();
var matches = MatchOccasions(people, todayIST);
foreach (var m in matches)
{
    if (await _sentLog.AlreadySentAsync(m.EmployeeId, m.Date, m.Type)) continue;

    var html = await _templating.RenderAsync(templateKey: m.TemplateKey, new { /* tokens */ });
    var attachments = await _renderer.RenderIfEnabledAsync(m);
    await _mail.SendAsync(to: m.Email, subject: m.Subject, htmlBody: html, attachments);

    await _sentLog.MarkSentAsync(m.EmployeeId, m.Date, m.Type);
}
```

---

## 15) Tests (Must‑Have)
1. **ExcelReaderTests**
   - Reads valid sheet, trims headers, parses dates.
   - Reports row‑level errors on malformed data.
2. **DateHelpersTests**
   - Birthday/anniversary matching including leap years; Feb 29 policy.
3. **TemplateEngineTests**
   - Token substitution, fallbacks, localization selection.
4. **MailClientTests**
   - DryRun mode stores files; SMTP errors retried; invalid addresses rejected.
5. **RendererSnapshotTests** *(optional)*
   - Generated card image/PDF matches baseline (Verify snapshot).
6. **IdempotencyTests**
   - No duplicate sends with SentLog.

---

## 16) Docker & Deployment
**Dockerfile (sample):**
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./src/Congrats.Worker/Congrats.Worker.csproj \
 && dotnet publish ./src/Congrats.Worker/Congrats.Worker.csproj -c Release -o /out /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Congrats.Worker.dll"]
```

**Kubernetes (hint):** run as single replica Cron‑style is **not** desired; keep a single long‑running worker (use leader election if multiple replicas).

**Windows Service:** `sc create` with `--windows-service` publish option, or `nssm`.

---

## 17) CI (GitHub Actions) — Build & Tests
```yaml
name: ci
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage"
```

---

## 18) Operational Runbook
- **Startup:** Validate config & connectivity (SMTP ping). Fail fast with clear diagnostics.
- **Daily Report:** After each run, emit summary (count sent, skipped, errors) to logs + optional email to HR.
- **On Failure:** Retries; if still failing, raise alert (email/Slack webhook).
- **Template Changes:** Keep templates versioned. Use snapshot tests and staging DryRun before promoting to prod.
- **Data Changes:** Validate Excel headers/types; maintain audit diff between runs if required.

---

## 19) Customization Notes
- Add department‑specific greetings, quotes, or signatures via per‑dept partials.
- Enable multi‑language by dropping `*.hi.html` alongside English.
- Theming via CSS variables inline to maximize client compatibility.
- Optionally, host backgrounds/assets on a CDN and reference via CID fallbacks for Outlook.

---

## 20) Definition of Done (DoD)
- ✅ Schedules daily at **08:00 IST** and sends correct greetings.
- ✅ Reads Excel robustly; ignores malformed rows with actionable errors.
- ✅ Uses configurable templates with personalization and localization.
- ✅ Logging, retries, idempotency, and DryRun implemented.
- ✅ Unit tests cover critical logic; optional snapshot tests for visuals.
- ✅ Docker image builds; CI green; secrets not in repo.
- ✅ Runbook + sample files included.

---

### Appendix A: Sample `people.xlsx`
Include `samples/people.xlsx` with 6–10 fake entries covering:
- Leap‑year birthday, mixed templates, missing department, multiple same‑day occasions.

### Appendix B: Token Map (min set)
```
name, first_name, email, department, location, today, occasion, years_completed,
company_name, signature, card_image_url
```

---

**End of Agents File**

