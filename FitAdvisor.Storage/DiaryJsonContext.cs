using System.Text.Json.Serialization;
using FitAdvisor.Core;
namespace FitAdvisor.Storage;
[JsonSerializable(typeof(ProfileRevision))]
[JsonSerializable(typeof(DailyTargetSnapshot))]
[JsonSerializable(typeof(WorkoutRecord))]
[JsonSerializable(typeof(DiaryBackup))]
[JsonSerializable(typeof(HabitRecord))]
[JsonSerializable(typeof(HabitGoal))]
[JsonSerializable(typeof(HabitSettings))]
[JsonSerializable(typeof(FoodNutritionSnapshot))]
internal partial class DiaryJsonContext : JsonSerializerContext { }
