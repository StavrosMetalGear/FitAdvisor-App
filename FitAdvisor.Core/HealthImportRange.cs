namespace FitAdvisor.Core;

public static class HealthImportRange
{
    // Each boundary is resolved separately: a local day is not always 24 hours.
    public static (DateTimeOffset Start, DateTimeOffset End) Create(DateOnly from, DateOnly through, DateOnly today, TimeZoneInfo zone)
    {
        if (from > through || through > today || through == DateOnly.MaxValue)
            throw new ArgumentException("Health Connect import dates must run from From through today or earlier.");
        DateTimeOffset Boundary(DateOnly day)
        {
            var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            // Some time zones move their clocks at midnight. Use the first valid minute.
            while (zone.IsInvalidTime(midnight)) midnight = midnight.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(midnight) ? zone.GetAmbiguousTimeOffsets(midnight).Max() : zone.GetUtcOffset(midnight);
            return new DateTimeOffset(midnight, offset);
        }
        return (Boundary(from), Boundary(through.AddDays(1)));
    }
}
