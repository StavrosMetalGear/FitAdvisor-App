namespace FitAdvisor.Core;
public sealed record GymRoutine(Guid Id, string Title, GymExercise[] Exercises, GymSet[] Sets)
{
    public void Validate() {
        if(Id==Guid.Empty) throw new ArgumentException("Routine identity required.");
        new GymSession(Id,Title,DateTimeOffset.UtcNow,new(),Exercises,Sets).Validate();
        if(Sets.Any(s=>s.Completed)) throw new ArgumentException("Routine sets must be pending.");
    }
    public static GymRoutine From(GymSession session, string title) => new(Guid.NewGuid(),title,session.Exercises.ToArray(),session.Sets.Select(s=>s with {Id=Guid.NewGuid(),Completed=false}).ToArray());
    public GymSession Start(DateTimeOffset now) { Validate();return new(Guid.NewGuid(),Title,now,new(0,now),Exercises.ToArray(),Sets.Select(s=>s with {Id=Guid.NewGuid(),Completed=false}).ToArray()); }
}
