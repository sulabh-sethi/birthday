using Congrats.Worker.Config;

namespace Congrats.Worker.Utils;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class DateHelpers
{
    public static bool IsBirthday(DateOnly today, DateOnly dateOfBirth, LeapDayPolicy policy)
    {
        if (dateOfBirth.Month == 2 && dateOfBirth.Day == 29)
        {
            return policy switch
            {
                LeapDayPolicy.Feb28 => today.Month == 2 && today.Day == (DateTime.IsLeapYear(today.Year) ? 29 : 28),
                LeapDayPolicy.Mar01 => today.Month == 3 && today.Day == (DateTime.IsLeapYear(today.Year) ? 1 : 1),
                LeapDayPolicy.Exact => today.Month == 2 && today.Day == 29 && DateTime.IsLeapYear(today.Year),
                _ => false
            };
        }

        return today.Month == dateOfBirth.Month && today.Day == dateOfBirth.Day;
    }

    public static bool IsWorkAnniversary(DateOnly today, DateOnly joiningDate)
    {
        if (today.Day == joiningDate.Day && today.Month == joiningDate.Month)
        {
            return YearsCompleted(today, joiningDate) > 0;
        }

        return false;
    }

    public static int YearsCompleted(DateOnly today, DateOnly startDate)
    {
        var years = today.Year - startDate.Year;
        var anniversaryThisYear = startDate.AddYears(years);
        if (today < anniversaryThisYear)
        {
            years--;
        }

        return Math.Max(0, years);
    }

    public static DateOnly ConvertToTimeZone(DateTimeOffset utc, TimeZoneInfo timeZone)
    {
        var localTime = TimeZoneInfo.ConvertTime(utc, timeZone);
        return DateOnly.FromDateTime(localTime.DateTime);
    }
}
