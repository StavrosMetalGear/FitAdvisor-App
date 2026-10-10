using System.Runtime.Versioning;
using Android.Content;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Android.OS;
using FitAdvisor.Core;
using Java.Interop;
using Java.Time;

namespace FitAdvisor.Mobile.Services;

public sealed class AndroidHealthSessionReader : IHealthSessionReader
{
    public const string ReadPermission = "android.permission.health.READ_EXERCISE";
    public async Task<IReadOnlyList<WorkoutRecord>> ReadAsync(DateOnly from, DateOnly through)
    {
        if (from > through || through > DateOnly.FromDateTime(DateTime.Today) || through.DayNumber - from.DayNumber > 29
            || from < DateOnly.FromDateTime(DateTime.Today).AddDays(-29))
            throw new ArgumentException("Select up to 30 days within the last 30 days.");
        if (!OperatingSystem.IsAndroidVersionAtLeast(34))
            throw new ArgumentException("Health Connect import requires Android 14 or later. Manual logging and backups remain available.");
        return await ReadSupportedAsync(from, through);
    }

    [SupportedOSPlatform("android34.0")]
    private static async Task<IReadOnlyList<WorkoutRecord>> ReadSupportedAsync(DateOnly from, DateOnly through)
    {
        var activity = Platform.CurrentActivity as MainActivity ?? throw new ArgumentException("Open FitAdvisor before importing.");
        using var manager = activity.GetSystemService(Java.Lang.Class.FromType(typeof(HealthConnectManager))) as HealthConnectManager;
        if (manager == null) throw new ArgumentException("Health Connect is unavailable on this device.");
        if (activity.CheckSelfPermission(ReadPermission) != Permission.Granted && !await activity.RequestExerciseReadAsync())
            throw new ArgumentException("Exercise read permission was denied. Nothing was imported.");
        if (!await activity.WaitUntilResumedAsync()) throw new ArgumentException("Import cancelled. Open FitAdvisor and retry.");
        var first = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(from.ToDateTime(TimeOnly.MinValue)));
        var lastDate = through.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var last = new DateTimeOffset(lastDate, TimeZoneInfo.Local.GetUtcOffset(lastDate));
        using var filter = new TimeInstantRangeFilter.Builder().SetStartTime(Instant.OfEpochMilli(first.ToUnixTimeMilliseconds()))!
            .SetEndTime(Instant.OfEpochMilli(last.ToUnixTimeMilliseconds()))!.Build()!;
        var records = new List<WorkoutRecord>(); long page = -1; var seenPages = new HashSet<long>();
        do
        {
            if (!activity.IsForeground) throw new ArgumentException("Import stopped because FitAdvisor left the foreground. Nothing was imported.");
            if (activity.CheckSelfPermission(ReadPermission) != Permission.Granted)
                throw new ArgumentException("Health Connect access was revoked. Nothing was imported.");
            using var builder = new ReadRecordsRequestUsingFilters.Builder(Java.Lang.Class.FromType(typeof(ExerciseSessionRecord)));
            builder.SetTimeRangeFilter(filter); builder.SetPageSize(500); builder.SetAscending(true);
            if (page != -1) builder.SetPageToken(page);
            using var request = builder.Build()!;
            using var receiver = new ReadReceiver();
            manager.ReadRecords(request, activity.MainExecutor!, receiver);
            using var response = await receiver.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
            foreach (var item in response.Records!)
            {
                var record = (item as Android.Runtime.IJavaObject).JavaCast<ExerciseSessionRecord>()
                    ?? throw new ArgumentException("Health Connect returned an invalid session. Nothing was imported.");
                var start = DateTimeOffset.FromUnixTimeMilliseconds(record.StartTime!.ToEpochMilli());
                var offset = record.StartZoneOffset?.TotalSeconds / 60 ?? (int)TimeZoneInfo.Local.GetUtcOffset(start).TotalMinutes;
                start = start.ToOffset(TimeSpan.FromMinutes(offset));
                var duration = (record.EndTime!.ToEpochMilli() - record.StartTime.ToEpochMilli()) / 1000d;
                var modified = DateTimeOffset.FromUnixTimeMilliseconds(record.Metadata!.LastModifiedTime!.ToEpochMilli());
                var type = (int)record.ExerciseType;
                var kind = record.ExerciseType is ExerciseSessionTypeEnum.Running or ExerciseSessionTypeEnum.RunningTreadmill ? "run"
                    : record.ExerciseType is ExerciseSessionTypeEnum.StrengthTraining or ExerciseSessionTypeEnum.Weightlifting ? "gym" : "other";
                // Retain the exact source type; session-only access does not supply run distance.
                var workout = new WorkoutRecord(Guid.NewGuid(), DateOnly.FromDateTime(start.DateTime), offset, kind,
                    string.IsNullOrWhiteSpace(record.Title) ? $"Exercise session ({record.ExerciseType})" : record.Title, duration, null, record.Notes,
                    Array.Empty<StrengthSet>(), start, modified < start ? start : modified, "health-connect",
                    record.Metadata.DataOrigin!.PackageName, record.Metadata.Id, start, type);
                workout.Validate(); records.Add(workout);
                if (records.Count > 10000) throw new ArgumentException("Too many sessions. Select a shorter range. Nothing was imported.");
            }
            page = response.NextPageToken;
            if (page != -1 && !seenPages.Add(page)) throw new ArgumentException("Health Connect returned repeated pages. Nothing was imported.");
        } while (page != -1);
        if (!activity.IsForeground) throw new ArgumentException("Import stopped because FitAdvisor left the foreground. Nothing was imported.");
        return records;
    }

    [SupportedOSPlatform("android34.0")]
    private sealed class ReadReceiver : Java.Lang.Object, IOutcomeReceiver
    {
        public TaskCompletionSource<ReadRecordsResponse> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is ReadRecordsResponse response) Completion.TrySetResult(response);
            else Completion.TrySetException(new ArgumentException("Health Connect returned an invalid response."));
        }
        public void OnError(Java.Lang.Throwable error) => Completion.TrySetException(
            new ArgumentException("Health Connect could not read sessions. Check access and availability, then retry. Nothing was imported."));
    }
}
