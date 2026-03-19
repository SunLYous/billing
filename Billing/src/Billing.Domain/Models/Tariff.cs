namespace Billing.Domain.Models;

public sealed class Tariff
{
    public int Id { get; init; }

    public string Prefix { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public decimal RatePerMinute { get; init; }

    public decimal ConnectionFee { get; init; }

    public string? Timeband { get; init; }

    public string? Weekday { get; init; }

    public int Priority { get; init; }

    public DateOnly? EffectiveDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public Guid BatchId { get; init; }

    public bool IsActiveAt(DateTime callTime)
    {
        var callDate = DateOnly.FromDateTime(callTime);

        if (EffectiveDate.HasValue && callDate < EffectiveDate.Value) return false;
        if (ExpiryDate.HasValue && callDate > ExpiryDate.Value) return false;

        if (!string.IsNullOrEmpty(Weekday))
        {
            var isoDow = callTime.DayOfWeek == DayOfWeek.Sunday
                ? 7
                : (int)callTime.DayOfWeek;

            if (!IsInWeekdayRange(Weekday, isoDow))
                return false;
        }

        if (!string.IsNullOrEmpty(Timeband))
        {
            var callTimeOfDay = TimeOnly.FromDateTime(callTime);
            if (!IsInTimeband(Timeband, callTimeOfDay))
                return false;
        }

        return true;
    }

    private static bool IsInWeekdayRange(string weekday, int isoDow)
    {
        foreach (var part in weekday.Split(',', StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (int.TryParse(range[0], out var from) &&
                    int.TryParse(range[1], out var to) &&
                    isoDow >= from && isoDow <= to)
                    return true;
            }
            else if (int.TryParse(part, out var day) && day == isoDow)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInTimeband(string timeband, TimeOnly callTime)
    {
        var parts = timeband.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return true;

        if (TimeOnly.TryParse(parts[0], out var from) &&
            TimeOnly.TryParse(parts[1], out var to))
        {
            return from <= to
                ? callTime >= from && callTime <= to
                : callTime >= from || callTime <= to;
        }

        return true;
    }
}
