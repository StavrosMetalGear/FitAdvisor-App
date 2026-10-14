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
    public const string HistoryPermission = "android.permission.health.READ_HEALTH_DATA_HISTORY";
    public async Task<HealthSessionReadResult> ReadAsync(DateOnly from, DateOnly through, bool requestHistory = false)
    {
        if (from > through || through > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("Select From on or before Through, with neither date in the future.");
        if (!OperatingSystem.IsAndroidVersionAtLeast(34))
            throw new ArgumentException("Health Connect import requires Android 14 or later. Manual logging and backups remain available.");
        return await ReadSupportedAsync(from, through, requestHistory);
    }

    [SupportedOSPlatform("android34.0")]
    private static async Task<HealthSessionReadResult> ReadSupportedAsync(DateOnly from, DateOnly through, bool requestHistory)
    {
        var activity = Platform.CurrentActivity as MainActivity ?? throw new ArgumentException("Open FitAdvisor before importing.");
        using var manager = activity.GetSystemService(Java.Lang.Class.FromType(typeof(HealthConnectManager))) as HealthConnectManager;
        if (manager == null) throw new ArgumentException("Health Connect is unavailable on this device.");
        if (activity.CheckSelfPermission(ReadPermission) != Permission.Granted && !await activity.RequestExerciseReadAsync())
            throw new ArgumentException("Exercise read permission was denied. Nothing was imported.");
        if (!await activity.WaitUntilResumedAsync()) throw new ArgumentException("Import cancelled. Open FitAdvisor and retry.");
        // The platform history permission arrived in API 35 / Android 14 extension 13.
        var historyAvailable = OperatingSystem.IsAndroidVersionAtLeast(35)
            || Android.OS.Ext.SdkExtensions.GetExtensionVersion((int)BuildVersionCodes.UpsideDownCake) >= 13;
        var historyGranted = activity.CheckSelfPermission(HistoryPermission) == Permission.Granted;
        if (requestHistory && historyAvailable && !historyGranted)
        {
            await activity.RequestExerciseReadAsync(HistoryPermission);
            if (!await activity.WaitUntilResumedAsync()) throw new ArgumentException("Import cancelled. Open FitAdvisor and retry.");
            historyGranted = activity.CheckSelfPermission(HistoryPermission) == Permission.Granted;
        }
        var historyStatus = historyGranted ? "Older-history access granted."
            : !historyAvailable ? "Older-history permission is unavailable on this Android/Health Connect version; using standard access."
            : requestHistory ? "Older-history access was not granted; using standard access."
            : "Older-history access not granted; enable the optional history request if needed.";
        if (!historyGranted) historyStatus += " Standard access starts 30 days before FitAdvisor's first Health Connect permission grant, not 30 days before today. Health Connect enforces the cutoff; earlier sessions may be omitted. The exact grant cutoff is not known to FitAdvisor.";
        var zone = TimeZoneInfo.Local;
        var (first, last) = HealthImportRange.Create(from, through, DateOnly.FromDateTime(DateTime.Today), zone);
        using var filter = new TimeInstantRangeFilter.Builder().SetStartTime(Instant.OfEpochMilli(first.ToUnixTimeMilliseconds()))!
            .SetEndTime(Instant.OfEpochMilli(last.ToUnixTimeMilliseconds()))!.Build()!;
        var records = new List<WorkoutRecord>(); long page = -1; var seenPages = new HashSet<long>();
        do
        {
            if (!activity.IsForeground) throw new ArgumentException("Import stopped because FitAdvisor left the foreground. Nothing was imported.");
            if (activity.CheckSelfPermission(ReadPermission) != Permission.Granted)
                throw new ArgumentException("Health Connect access was revoked. Nothing was imported.");
            using var builder = new ReadRecordsRequestUsingFilters.Builder(Java.Lang.Class.FromType(typeof(ExerciseSessionRecord)));
            builder.SetTimeRangeFilter(filter); builder.SetPageSize(500);
            // Native API forbids an explicit sort order together with a continuation token.
            if (page == -1) builder.SetAscending(true);
            else builder.SetPageToken(page);
            using var request = builder.Build()!;
            using var receiver = new ReadReceiver(historyGranted);
            try { manager.ReadRecords(request, activity.MainExecutor!, receiver); }
            catch (Java.Lang.SecurityException)
            { throw new ArgumentException("Health Connect denied the Exercise query. Check Exercise read access and older-history access for these dates. Nothing was imported."); }
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
        if (activity.CheckSelfPermission(ReadPermission) != Permission.Granted)
            throw new ArgumentException("Exercise read permission was revoked. Nothing was imported; existing workouts are unchanged.");
        var sources = string.Join(", ", records.Select(r => r.SourcePackage).Distinct().OrderBy(p => p));
        var diagnostics = $"Exercise read permission granted. Health Connect returned {records.Count} Exercise sessions for {from:yyyy-MM-dd} through {through:yyyy-MM-dd} (inclusive), using phone time zone {zone.Id}. {historyStatus}";
        if (records.Count == 0) diagnostics += " No sessions were returned; this is not proof of a permission failure or successful band sync. In Health Connect, open Data and access → Activity → Exercise and check the workout and its source. Mi Fitness must export an Exercise session. Steps and active calories (including Pixel-sourced records) do not establish that a Mi Fitness workout exists and are never converted into workouts.";
        else diagnostics += $" Source packages: {sources}. Saved workout dates use each session's recorded offset, or the phone zone if absent; they can differ from the query date when travelling.";
        return new(records, diagnostics);
    }

    [SupportedOSPlatform("android34.0")]
    private sealed class ReadReceiver(bool historyGranted) : Java.Lang.Object, IOutcomeReceiver
    {
        public TaskCompletionSource<ReadRecordsResponse> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is ReadRecordsResponse response) Completion.TrySetResult(response);
            else Completion.TrySetException(new ArgumentException("Health Connect returned an invalid response."));
        }
        public void OnError(Java.Lang.Throwable error)
        {
            var code = error is HealthConnectException healthError ? (int)healthError.ErrorCode : -1;
            var detail = code switch
            {
                5 => "Health Connect denied access. Check FitAdvisor's Exercise read permission. " +
                    (historyGranted ? "" : "The selected dates may also exceed standard history access; optionally request older history or try a newly exported session. "),
                3 => "Health Connect rejected the date query. " +
                    (historyGranted ? "" : "Older dates may exceed standard history access; try a newly exported session or optional older-history access. "),
                7 => "Health Connect rate limit reached. Wait before retrying. ",
                _ => "Health Connect could not complete the session query. Check service availability and retry. "
            };
            Completion.TrySetException(new ArgumentException($"{detail}Service error code: {code}. Nothing was imported; existing workouts are unchanged."));
        }
    }
}
