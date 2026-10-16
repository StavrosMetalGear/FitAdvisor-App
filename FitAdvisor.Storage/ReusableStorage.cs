using FitAdvisor.Core;
namespace FitAdvisor.Storage;
public sealed partial class SqlitePersonalDiary
{
    public Task<SavedFood[]> GetSavedFoodsAsync() => Execute(db => HabitRead<SavedFood>(db,"saved_foods"));
    public Task SaveFoodAsync(SavedFood food) { food.Validate(); return Execute(db=> { using var tx=db.BeginTransaction(); GymWrite(db,"saved_foods",food.Id,food,tx);tx.Commit();return true; }); }
    public Task<GymRoutine[]> GetGymRoutinesAsync() => Execute(db => HabitRead<GymRoutine>(db,"gym_routines"));
    public Task SaveGymRoutineAsync(GymRoutine routine) { routine.Validate();return Execute(db=> { using var tx=db.BeginTransaction();GymWrite(db,"gym_routines",routine.Id.ToString(),routine,tx);tx.Commit();return true; }); }
}
