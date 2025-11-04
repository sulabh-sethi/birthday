using System.Linq;
using ClosedXML.Excel;
using Congrats.Worker.Config;
using Congrats.Worker.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Congrats.Tests;

public class ExcelReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsAllRowsAndNoErrors_WhenWorkbookIsValid()
    {
        var path = CreateWorkbook(
            rows: new[]
            {
                new object?[] { "E001", "Aditi Sharma", "aditi@example.com", new DateTime(1990,2,28), new DateTime(2015,8,17), "classic", "Engineering", "Mumbai", "en" },
                new object?[] { "E002", "Rohit Patel", "rohit@example.com", new DateTime(1988,2,29), new DateTime(2012,3,1), "festive", "Sales", "Ahmedabad", "en" }
            });

        var options = Options.Create(new AppOptions
        {
            Excel = new AppOptions.ExcelOptions { FilePath = path, SheetName = "People" }
        });

        var reader = new ExcelReader(options, NullLogger<ExcelReader>.Instance);

        var result = await reader.ReadAsync();

        result.HasErrors.Should().BeFalse();
        result.People.Should().HaveCount(2);
        result.People.Select(p => p.EmployeeId).Should().Contain(new[] { "E001", "E002" });
    }

    [Fact]
    public async Task ReadAsync_ReturnsRowErrors_WhenDataIsInvalid()
    {
        var path = CreateWorkbook(
            rows: new[]
            {
                new object?[] { "E001", "Aditi Sharma", "aditi@example.com", "not-a-date", new DateTime(2015,8,17), "classic", "Engineering", "Mumbai", "en" }
            });

        var options = Options.Create(new AppOptions
        {
            Excel = new AppOptions.ExcelOptions { FilePath = path, SheetName = "People" }
        });

        var reader = new ExcelReader(options, NullLogger<ExcelReader>.Instance);

        var result = await reader.ReadAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle();
    }

    private static string CreateWorkbook(IEnumerable<object?[]> rows)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"excel-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("People");
        ws.Cell(1, 1).Value = "EmployeeId";
        ws.Cell(1, 2).Value = "FullName";
        ws.Cell(1, 3).Value = "Email";
        ws.Cell(1, 4).Value = "DateOfBirth";
        ws.Cell(1, 5).Value = "DateOfJoining";
        ws.Cell(1, 6).Value = "PreferredTemplate";
        ws.Cell(1, 7).Value = "Department";
        ws.Cell(1, 8).Value = "Location";
        ws.Cell(1, 9).Value = "Language";

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var col = 0; col < row.Length; col++)
            {
                ws.Cell(rowIndex, col + 1).Value = row[col] ?? string.Empty;
            }

            rowIndex++;
        }

        workbook.SaveAs(filePath);
        return filePath;
    }
}
