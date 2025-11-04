# Congratulatory Service

A .NET 8 background worker that delivers daily birthday and work-anniversary greetings at 08:00 Asia/Kolkata. The service ingests employee data from an Excel workbook, renders personalized HTML cards, optionally generates downloadable artifacts, and sends the result via SMTP or in dry-run mode for staging environments.

## Features

- Time-zone aware daily scheduling (Cron: `0 0 8 * * ?` with `Asia/Kolkata`).
- Robust Excel ingestion via ClosedXML with row-level validation feedback.
- Personalization using Scriban templates with localization-ready token sets.
- Dry-run support that writes `.eml` messages and optional attachments to disk.
- File-based idempotency log to prevent duplicate sends across restarts.
- Serilog logging to console and rolling files.
- Dockerfile and GitHub Actions workflow for CI/CD pipelines.

## Getting Started

1. **Configure settings** — copy `samples/appsettings.sample.json` to `src/Congrats.Worker/appsettings.Development.json` and update SMTP credentials, file paths, and feature toggles.
2. **Prepare data** — restore the sample workbook with `bash samples/restore-sample.sh` (creates `samples/people.xlsx`) or point to your own Excel file that follows the schema described in `agents_file_md_birthday_anniversary_congratulatory_service.md`.
3. **Run locally**
   ```bash
   dotnet run --project src/Congrats.Worker
   ```
4. **Build container**
   ```bash
   docker build -t congrats-service .
   ```

## Testing

Run the automated test suite with:

```bash
dotnet test --configuration Release
```

## Configuration Highlights

- `CongratulatoryService:Excel` — input workbook location and sheet name.
- `CongratulatoryService:Mail` — SMTP sender, host, credentials, and retry settings.
- `CongratulatoryService:DryRun` — toggles `.eml` generation instead of actual delivery.
- `CongratulatoryService:SentLog` — JSON file used to enforce idempotent sends.
- `CongratulatoryService:Occasions:LeapDayPolicy` — choose between `Feb28`, `Mar01`, or `Exact` for leap-year birthdays.

See `agents_file_md_birthday_anniversary_congratulatory_service.md` for the complete runbook and functional requirements.
