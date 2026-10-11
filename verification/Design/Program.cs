using FitAdvisor.Core;

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}
var first = new DateOnly(2026, 12, 31);
for (var offset = 0; offset < 400; offset++)
{
    var day = first.AddDays(offset);
    var message = DailyEncouragement.ForDay(day);
    Check(!string.IsNullOrWhiteSpace(message.English) && !string.IsNullOrWhiteSpace(message.Greek), "Both languages exist");
    Check(DailyEncouragement.ForDay(day) == message, "Repeated requests remain stable");
    var snapshot = DailyEncouragement.Snapshot(day);
    Check(DailyEncouragement.TryRead(snapshot, day, out var reread) && reread == message, "Snapshot round trip");
    Check(!DailyEncouragement.TryRead(snapshot, day.AddDays(1), out _), "Next local day invalidates snapshot");
    Check(DailyEncouragement.ForDay(day.AddDays(1)) != message, "Next day changes the pair");
}
Check(DailyEncouragement.TryRead("2026-12-31|Saved English|Αποθηκευμένο ελληνικό", first, out var preserved)
    && preserved.English == "Saved English", "Previously displayed wording survives catalog edits");
foreach (var malformed in new[] { "", "bad|text", "2026-12-31||", "2026-12-30|a|b" })
    Check(!DailyEncouragement.TryRead(malformed, first, out _), "Invalid snapshot falls back safely");
var before = new DateTimeOffset(2026, 10, 11, 23, 59, 0, TimeSpan.FromHours(3));
var after = before.AddMinutes(2);
Check(DailyEncouragement.ForDay(DateOnly.FromDateTime(before.DateTime)) !=
      DailyEncouragement.ForDay(DateOnly.FromDateTime(after.DateTime)), "Local midnight, independent of UTC date");
Console.WriteLine("PASS: 400 days, bilingual stability, daily rollover, saved wording, invalid snapshots and local midnight.");
