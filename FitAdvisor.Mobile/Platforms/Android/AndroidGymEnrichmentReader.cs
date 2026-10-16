using System.Runtime.Versioning;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Java.Interop;
using Android.OS;
using Java.Time;
using FitAdvisor.Core;
namespace FitAdvisor.Mobile.Services;
public static class AndroidGymEnrichmentReader
{
    public const string HeartPermission="android.permission.health.READ_HEART_RATE";
    public const string EnergyPermission="android.permission.health.READ_ACTIVE_CALORIES_BURNED";
    public static async Task<GymEnrichment> ReadAsync(GymSession session, WorkoutRecord import,bool heart,bool energy)
    {
        if(!OperatingSystem.IsAndroidVersionAtLeast(34))throw new ArgumentException("Sensor enrichment requires Android 14 or later.");
        if(session.Status!="finished"||session.ImportedWorkoutId!=import.Id||import.IsManual||import.StartTime==null||string.IsNullOrWhiteSpace(import.SourcePackage))throw new ArgumentException("Explicitly link a completed imported session first.");
        if(!heart&&!energy)throw new ArgumentException("Enable at least one optional measurement.");
        return await ReadSupported(session,import,heart,energy);
    }
    [SupportedOSPlatform("android34.0")]
    private static async Task<GymEnrichment> ReadSupported(GymSession session,WorkoutRecord import,bool heart,bool energy)
    {
        var activity=Platform.CurrentActivity as MainActivity??throw new ArgumentException("Open FitAdvisor first.");
        var diagnostics=new List<string>();
        async Task<bool> EnsurePermission(string permission,bool enabled) {
            if(!enabled)return false;
            var granted=activity.CheckSelfPermission(permission)==Permission.Granted||await activity.RequestExerciseReadAsync(permission);
            if(!await activity.WaitUntilResumedAsync())throw new ArgumentException("Sensor read cancelled.");
            if(!granted)diagnostics.Add(permission+" denied; measurement unavailable.");return granted;
        }
        heart=await EnsurePermission(HeartPermission,heart);energy=await EnsurePermission(EnergyPermission,energy);
        var from=session.StartedAt>import.StartTime!.Value?session.StartedAt:import.StartTime!.Value;
        var end=import.StartTime!.Value.AddSeconds(import.DurationSeconds);var through=session.FinishedAt!.Value<end?session.FinishedAt.Value:end;
        if(through<=from)throw new ArgumentException("Session times do not overlap.");
        using var manager=activity.GetSystemService(Java.Lang.Class.FromType(typeof(HealthConnectManager))) as HealthConnectManager??throw new ArgumentException("Health Connect unavailable.");
        using var filter=new TimeInstantRangeFilter.Builder().SetStartTime(Instant.OfEpochMilli(from.ToUnixTimeMilliseconds()))!.SetEndTime(Instant.OfEpochMilli(through.ToUnixTimeMilliseconds()))!.Build()!;
        using var origin=new DataOrigin.Builder().SetPackageName(import.SourcePackage!)!.Build()!;
        var samples=new List<HeartSample>();var intervals=new List<EnergyInterval>();
        async Task Read(Type type,Action<Java.Lang.Object> consume) {
            long page=-1;var pages=new HashSet<long>();var count=0;
            do {
                if(!activity.IsForeground)throw new ArgumentException("Sensor read stopped outside foreground. Existing snapshot retained.");
                using var builder=new ReadRecordsRequestUsingFilters.Builder(Java.Lang.Class.FromType(type));builder.SetTimeRangeFilter(filter);builder.AddDataOrigins(origin);builder.SetPageSize(500);
                if(page==-1)builder.SetAscending(true);else builder.SetPageToken(page);
                using var request=builder.Build()!;using var receiver=new Receiver();manager.ReadRecords(request,activity.MainExecutor!,receiver);
                using var response=await receiver.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
                foreach(var item in response.Records!){if(++count>10000)throw new ArgumentException("Too many sensor records. Existing snapshot retained.");consume((Java.Lang.Object)item);}
                page=response.NextPageToken;if(page!=-1&&!pages.Add(page))throw new ArgumentException("Repeated sensor page. Existing snapshot retained.");
            }while(page!=-1);
        }
        try {
            if(heart)await Read(typeof(HeartRateRecord),item=>{var r=item.JavaCast<HeartRateRecord>()!;if(r.Metadata?.DataOrigin?.PackageName!=import.SourcePackage)return;foreach(var sample in r.Samples!){var s=sample.JavaCast<HeartRateRecord.HeartRateSample>()!;samples.Add(new(DateTimeOffset.FromUnixTimeMilliseconds(s.Time!.ToEpochMilli()),s.BeatsPerMinute));}});
            if(energy)await Read(typeof(ActiveCaloriesBurnedRecord),item=>{var r=item.JavaCast<ActiveCaloriesBurnedRecord>()!;if(r.Metadata?.DataOrigin?.PackageName!=import.SourcePackage)return;intervals.Add(new(r.Metadata!.Id!,DateTimeOffset.FromUnixTimeMilliseconds(r.StartTime!.ToEpochMilli()),DateTimeOffset.FromUnixTimeMilliseconds(r.EndTime!.ToEpochMilli()),r.Energy!.InCalories/1000d));});
        }catch(Exception e)when(e is Java.Lang.SecurityException or TimeoutException){throw new ArgumentException("Sensor access denied or timed out. Existing snapshot retained; retry after sync.");}
        diagnostics.Add("Post-sync snapshot, exact linked origin only. No live monitoring. Time intersection of manual and imported session; source intervals may be incomplete. Xiaomi training-effect scores are not available through Health Connect.");
        return GymEnrichment.Create(import,from,through,samples,intervals,string.Join(" ",diagnostics));
    }
    [SupportedOSPlatform("android34.0")]
    private sealed class Receiver : Java.Lang.Object,IOutcomeReceiver
    {
        public TaskCompletionSource<ReadRecordsResponse> Completion {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result) {if(result is ReadRecordsResponse response)Completion.TrySetResult(response);else Completion.TrySetException(new ArgumentException("Invalid sensor response."));}
        public void OnError(Java.Lang.Throwable error)=>Completion.TrySetException(new ArgumentException("Health Connect could not read sensors (permission, history access, service or rate limit). Existing snapshot retained. Check Health Connect and retry later."));
    }
}

