using System.Text.Json.Serialization;
using FitAdvisor.Core;
namespace FitAdvisor.Storage;
[JsonSerializable(typeof(ProfileRevision))]
[JsonSerializable(typeof(DailyTargetSnapshot))]
internal partial class DiaryJsonContext : JsonSerializerContext { }
