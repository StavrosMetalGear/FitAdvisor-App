using System.Text.Json.Serialization;
using FitAdvisor.Core;
namespace FitAdvisor.Storage;
[JsonSerializable(typeof(ProfileRevision))]
[JsonSerializable(typeof(DailyTargetSnapshot))]
[JsonSerializable(typeof(WorkoutRecord))]
[JsonSerializable(typeof(DiaryBackup))]
internal partial class DiaryJsonContext : JsonSerializerContext { }
