using System.Linq;
using ClosedXML.Excel;
using Congrats.Worker.Config;
using Congrats.Worker.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Congrats.Worker.Data;

public interface IExcelReader
{
    Task<ExcelReadResult> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record ExcelRowError(int RowNumber, string Message);

public sealed record ExcelReadResult(
    IReadOnlyCollection<Person> People,
    IReadOnlyCollection<ExcelRowError> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}

public sealed class ExcelReader : IExcelReader
{
    private static readonly HashSet<string> RequiredColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "EmployeeId",
        "FullName",
        "Email",
        "DateOfBirth",
        "DateOfJoining"
    };

    private static readonly HashSet<string> KnownColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "EmployeeId",
        "FullName",
        "Email",
        "DateOfBirth",
        "DateOfJoining",
        "PreferredTemplate",
        "Department",
        "Location",
        "Language"
    };

    private readonly AppOptions _options;
    private readonly ILogger<ExcelReader> _logger;

    public ExcelReader(IOptions<AppOptions> options, ILogger<ExcelReader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ExcelReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var excelOptions = _options.Excel;
        if (!File.Exists(excelOptions.FilePath))
        {
            if (excelOptions.Optional)
            {
                _logger.LogWarning("Excel file {Path} not found; returning empty dataset", excelOptions.FilePath);
                return Task.FromResult(new ExcelReadResult(Array.Empty<Person>(), Array.Empty<ExcelRowError>()));
            }

            throw new FileNotFoundException($"Excel file not found at {excelOptions.FilePath}");
        }

        using var workbook = new XLWorkbook(excelOptions.FilePath);
        var worksheet = !string.IsNullOrWhiteSpace(excelOptions.SheetName)
            ? workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name, excelOptions.SheetName, StringComparison.OrdinalIgnoreCase))
            : workbook.Worksheets.First();

        if (worksheet is null)
        {
            throw new InvalidOperationException($"Worksheet '{excelOptions.SheetName}' was not found in workbook");
        }

        var headerRow = worksheet.FirstRowUsed();
        var headers = headerRow.CellsUsed()
            .Select((cell, index) => new { index, header = cell.GetString().Trim() })
            .ToDictionary(x => x.header, x => x.index + 1, StringComparer.OrdinalIgnoreCase);

        ValidateHeaders(headers);

        var people = new List<Person>();
        var errors = new List<ExcelRowError>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var person = ParseRow(row, headers, headerRow);
                people.Add(person);
            }
            catch (Exception ex)
            {
                errors.Add(new ExcelRowError(row.RowNumber(), ex.Message));
            }
        }

        return Task.FromResult(new ExcelReadResult(people, errors));
    }

    private static void ValidateHeaders(IReadOnlyDictionary<string, int> headers)
    {
        var missing = RequiredColumns.Where(required => !headers.ContainsKey(required)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Missing required columns: {string.Join(", ", missing)}");
        }
    }

    private Person ParseRow(IXLRow row, IReadOnlyDictionary<string, int> headers, IXLRow headerRow)
    {
        string GetString(string column)
        {
            var index = headers[column];
            return row.Cell(index).GetString().Trim();
        }

        string? GetOptional(string column)
        {
            return headers.TryGetValue(column, out var idx)
                ? row.Cell(idx).GetString().TrimToNull()
                : null;
        }

        DateOnly GetDate(string column)
        {
            var index = headers[column];
            var cell = row.Cell(index);
            if (cell.DataType == XLDataType.DateTime)
            {
                return DateOnly.FromDateTime(cell.GetDateTime());
            }

            if (DateOnly.TryParse(cell.GetString(), out var result))
            {
                return result;
            }

            if (double.TryParse(cell.GetString(), out var numeric))
            {
                return DateOnly.FromDateTime(DateTime.FromOADate(numeric));
            }

            throw new FormatException($"Column '{column}' is not a valid date value.");
        }

        var additional = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.CellsUsed())
        {
            var columnNumber = cell.Address.ColumnNumber;
            var columnHeader = headerRow.Cell(columnNumber).GetString().Trim();
            if (!KnownColumns.Contains(columnHeader))
            {
                additional[columnHeader] = cell.GetString().TrimToNull();
            }
        }

        return new Person(
            EmployeeId: GetString("EmployeeId"),
            FullName: GetString("FullName"),
            Email: GetString("Email"),
            DateOfBirth: GetDate("DateOfBirth"),
            DateOfJoining: GetDate("DateOfJoining"),
            PreferredTemplate: GetOptional("PreferredTemplate"),
            Department: GetOptional("Department"),
            Location: GetOptional("Location"),
            Language: GetOptional("Language"),
            AdditionalData: additional);
    }
}
