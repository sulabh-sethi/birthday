namespace Congrats.Worker.Utils;

public static class StringExtensions
{
    public static string? TrimToNull(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return input.Trim();
    }
}
