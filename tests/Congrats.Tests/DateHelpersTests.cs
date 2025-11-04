using Congrats.Worker.Config;
using Congrats.Worker.Utils;
using FluentAssertions;
using Xunit;

namespace Congrats.Tests;

public class DateHelpersTests
{
    [Theory]
    [InlineData(2024, 2, 29, true)]
    [InlineData(2023, 2, 28, true)]
    [InlineData(2023, 3, 1, false)]
    public void IsBirthday_LeapDayPolicyFeb28(int year, int month, int day, bool expected)
    {
        var today = new DateOnly(year, month, day);
        var dob = new DateOnly(1988, 2, 29);

        DateHelpers.IsBirthday(today, dob, LeapDayPolicy.Feb28).Should().Be(expected);
    }

    [Fact]
    public void YearsCompleted_ComputesAccurately()
    {
        var today = new DateOnly(2024, 8, 17);
        var joining = new DateOnly(2015, 8, 17);

        DateHelpers.YearsCompleted(today, joining).Should().Be(9);
    }

    [Fact]
    public void IsWorkAnniversary_ReturnsFalse_WhenNotReached()
    {
        var today = new DateOnly(2024, 8, 16);
        var joining = new DateOnly(2015, 8, 17);

        DateHelpers.IsWorkAnniversary(today, joining).Should().BeFalse();
    }

    [Fact]
    public void IsWorkAnniversary_ReturnsTrue_WhenReached()
    {
        var today = new DateOnly(2024, 8, 17);
        var joining = new DateOnly(2015, 8, 17);

        DateHelpers.IsWorkAnniversary(today, joining).Should().BeTrue();
    }
}
