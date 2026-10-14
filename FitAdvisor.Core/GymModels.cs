using System.Globalization;
using System.Text;

namespace FitAdvisor.Core;

public sealed record GymExercise(string Id, string English, string Greek, string Muscle, string Equipment,
    string Mode, string Convention, int? RestSeconds = null, bool Custom = false)
{
    public string Display => $"{English} / {Greek} · {Muscle} · {Equipment}";
    public string Key => $"{Id}|{Equipment}|{Mode}|{Convention}|kg|m|s";
    public string WeightMeaning => Convention switch { "total-barbell"=>"total barbell / συνολική μπάρα", "per-dumbbell"=>"per dumbbell, pair / ανά αλτήρα, ζεύγος", "machine"=>"machine load / φορτίο μηχανήματος", "external"=>"external total / εξωτερικό σύνολο", _=>"bodyweight / σωματικό βάρος" };
    public void Validate()
    {
        DiaryValidation.Text(Id, 100, "Exercise ID"); DiaryValidation.Text(English, 100, "Exercise name");
        DiaryValidation.Text(Greek, 100, "Greek name"); DiaryValidation.Text(Muscle, 100, "Muscle group");
        DiaryValidation.Text(Equipment, 100, "Equipment");
        if (Mode is not ("load" or "body" or "added" or "assistance" or "duration" or "distance")) throw new ArgumentException("Invalid exercise fields.");
        if (Convention is not ("total-barbell" or "per-dumbbell" or "machine" or "external" or "none")) throw new ArgumentException("Invalid resistance convention.");
        if (Mode is "body" or "duration" or "distance" && Convention != "none") throw new ArgumentException("This exercise has no load field.");
        if (Mode is "load" or "added" or "assistance" && Convention == "none") throw new ArgumentException("Choose a load convention.");
        if (RestSeconds is {} rest) DiaryValidation.Number(rest, 5, 3600, "Rest seconds");
    }
    public string LoadLabel => Mode == "assistance" ? "Assistance / Υποβοήθηση (kg; lower is less assistance)" :
        Mode == "added" ? "Added resistance / Πρόσθετο βάρος (kg; excludes bodyweight)" : Convention switch
        { "total-barbell" => "Total barbell / Συνολικό βάρος μπάρας (kg, includes bar)",
          "per-dumbbell" => "Per dumbbell / Ανά αλτήρα (kg; paired simultaneous reps / ζεύγος, ταυτόχρονες επαναλήψεις)", "machine" => "Machine load / Φορτίο μηχανήματος (kg)", _ => "External resistance / Εξωτερικό βάρος (kg)" };
}

public sealed record GymSet(Guid Id, string ExerciseId, int? Reps, double? LoadKg, double? Seconds,
    double? DistanceM, bool Warmup, bool Completed = true)
{
    public void Validate(GymExercise e)
    {
        if (Id == Guid.Empty || ExerciseId != e.Id) throw new ArgumentException("Invalid set identity.");
        var rep = e.Mode is "load" or "body" or "added" or "assistance";
        var load = e.Mode is "load" or "added" or "assistance";
        if (rep != Reps.HasValue || load != LoadKg.HasValue || (e.Mode == "duration") != Seconds.HasValue || (e.Mode == "distance") != DistanceM.HasValue)
            throw new ArgumentException("Enter only the fields appropriate to this exercise.");
        if (Reps is {} r) DiaryValidation.Number(r, 1, 1000, "Reps");
        if (LoadKg is {} kg) DiaryValidation.Number(kg, 0, 1000, "Load kg");
        if (Seconds is {} s) DiaryValidation.Number(s, 1, 86400, "Duration seconds");
        if (DistanceM is {} m) DiaryValidation.Number(m, 1, 100000, "Distance metres");
    }
    public string Summary(GymExercise e) => $"{(Completed ? "✓" : "Pending / Εκκρεμές")} {(Warmup ? "Warm-up / Προθέρμανση" : "Working / Κύριο")}: " +
        (Reps is {} r ? $"{r} reps" + (LoadKg is {} kg ? $" × {kg:g} kg ({(e.Mode=="assistance"?"assistance / υποβοήθηση":e.Mode=="added"?"added / πρόσθετο":e.WeightMeaning)})" : " · bodyweight / σωματικό βάρος") : Seconds is {} s ? $"{s:g} s" : $"{DistanceM:g} m");
    // External-load volume only, for conventional loaded reps. A dumbbell set records reps for the pair.
    public double? Volume(GymExercise e) => Completed && !Warmup && e.Mode == "load"
        ? Reps * LoadKg * (e.Convention == "per-dumbbell" ? 2 : 1) : null;
}

public sealed record GymClock(double Accumulated = 0, DateTimeOffset? RunningSince = null)
{
    public double Elapsed(DateTimeOffset now) => Accumulated + (RunningSince is {} start ? Math.Max(0, (now - start).TotalSeconds) : 0);
    public GymClock Pause(DateTimeOffset now) => new(Elapsed(now));
    public GymClock Resume(DateTimeOffset now) => RunningSince == null ? this with { RunningSince = now } : this;
    public void Validate() { DiaryValidation.Number(Accumulated, 0, 31536000, "Timer"); if (RunningSince == default(DateTimeOffset)) throw new ArgumentException("Invalid timer timestamp."); }
}
public sealed record GymRest(bool Active = false, int TargetSeconds = 90, GymClock? Clock = null)
{
    public double Remaining(DateTimeOffset now) => Active ? Math.Max(0, TargetSeconds - (Clock ?? new()).Elapsed(now)) : 0;
    public void Validate() { DiaryValidation.Number(TargetSeconds, 5, 7200, "Rest timer"); Clock?.Validate(); }
}
public sealed record GymPreferences(bool RestEnabled = false, bool AutoRest = true, int DefaultRestSeconds = 90,
    bool Vibration = false, bool Sound = false, bool HideTechnique = false)
{
    public void Validate() => DiaryValidation.Number(DefaultRestSeconds, 5, 3600, "Default rest seconds");
}
public sealed record GymSession(Guid Id, string Title, DateTimeOffset StartedAt, GymClock Clock,
    GymExercise[] Exercises, GymSet[] Sets, string Status = "active", Guid? PlanId = null,
    Guid? ImportedWorkoutId = null, DateTimeOffset? FinishedAt = null, GymRest? Rest = null)
{
    public Guid WorkoutId => PlanId ?? Id;
    public void Validate()
    {
        if (Id == Guid.Empty || StartedAt == default || Status is not ("active" or "finished" or "cancelled")) throw new ArgumentException("Invalid gym session.");
        DiaryValidation.Text(Title, 200, "Session title"); Clock.Validate(); Rest?.Validate();
        if (Exercises == null || Sets == null || Exercises.Length > 100 || Sets.Length > 1000) throw new ArgumentException("Invalid session size.");
        if (Exercises.Select(e => e.Id).Distinct().Count() != Exercises.Length || Sets.Select(s => s.Id).Distinct().Count() != Sets.Length) throw new ArgumentException("Duplicate exercise or set.");
        foreach (var e in Exercises) e.Validate();
        foreach (var s in Sets) s.Validate(Exercises.SingleOrDefault(e => e.Id == s.ExerciseId) ?? throw new ArgumentException("Unknown set exercise."));
        if (Status == "finished" && (FinishedAt == null || FinishedAt < StartedAt || Clock.RunningSince != null || !Sets.Any(s => s.Completed))) throw new ArgumentException("Incomplete finished session.");
        if (Status != "finished" && ImportedWorkoutId != null) throw new ArgumentException("Only finished sessions may be linked.");
    }
    public WorkoutRecord Workout() => new(WorkoutId, DateOnly.FromDateTime(StartedAt.LocalDateTime), (int)StartedAt.ToLocalTime().Offset.TotalMinutes,
        "gym", Title, Math.Clamp(Clock.Accumulated, 1, 604800), null, "Manual Gym Session; detailed sets in Gym Session. Heart rate and calories unavailable.", [], StartedAt, FinishedAt ?? StartedAt, StartTime: StartedAt);
    public double Volume => Sets.Sum(s => s.Volume(Exercises.Single(e => e.Id == s.ExerciseId)) ?? 0);
}

public static class GymRecords
{
    public static GymSet[] Comparable(IEnumerable<GymSession> history, GymExercise exercise) => history.Where(s => s.Status == "finished")
        .SelectMany(s => s.Sets.Where(set => set.Completed && !set.Warmup && s.Exercises.Any(e => e.Id == set.ExerciseId && e.Key == exercise.Key))).ToArray();
    public static string Describe(IEnumerable<GymSession> history, GymExercise e)
    {
        var sets = Comparable(history, e); if (sets.Length == 0) return "No working-set history / Χωρίς ιστορικό κύριων σετ";
        if (e.Mode == "assistance") return $"Least assistance / Λιγότερη υποβοήθηση: {sets.Min(s => s.LoadKg):g} kg (compare reps too)";
        if (e.Mode is "load" or "added") {
            var max = sets.Max(s => s.LoadKg)!.Value; var reps = sets.Where(s => s.LoadKg == max).Max(s => s.Reps);
            var perLoad=string.Join("; ",sets.GroupBy(s=>s.LoadKg).OrderByDescending(g=>g.Key).Take(20).Select(g=>$"{g.Key:g} kg: {g.Max(s=>s.Reps)} reps"));
            return $"Heaviest / Μέγιστο: {max:g} kg · {e.WeightMeaning}{(e.Mode=="added"?" · added only / μόνο πρόσθετο":"")}\nMost reps at each load / Μέγιστες επαναλήψεις ανά φορτίο: {perLoad}";
        }
        return e.Mode == "body" ? $"Most bodyweight reps / Επαναλήψεις: {sets.Max(s => s.Reps)}" : e.Mode == "duration" ? $"Longest / Διάρκεια: {sets.Max(s => s.Seconds):g} s" : $"Longest / Απόσταση: {sets.Max(s => s.DistanceM):g} m";
    }
}

public static class GymCatalogue
{
    private static GymExercise E(string id, string en, string el, string muscle, string equipment, string mode = "load", string convention = "machine") => new(id,en,el,muscle,equipment,mode,convention);
    public static GymExercise[] Starter { get; } = [
        E("bb-squat","Barbell back squat","Καθίσματα με μπάρα","Legs / Πόδια","Barbell / Μπάρα","load","total-barbell"),
        E("bw-squat","Bodyweight squat","Καθίσματα χωρίς βάρος","Legs / Πόδια","None / Χωρίς εξοπλισμό","body","none"),
        E("leg-press","Leg press","Πρέσα ποδιών","Legs / Πόδια","Machine / Μηχάνημα"),
        E("leg-extension","Leg extension","Εκτάσεις τετρακεφάλων","Legs / Πόδια","Machine / Μηχάνημα"),
        E("leg-curl","Seated leg curl","Καθιστές κάμψεις ποδιών","Legs / Πόδια","Machine / Μηχάνημα"),
        E("deadlift","Barbell deadlift","Άρσεις θανάτου με μπάρα","Back / Πλάτη","Barbell / Μπάρα","load","total-barbell"),
        E("bench","Barbell bench press","Πιέσεις πάγκου με μπάρα","Chest / Στήθος","Barbell / Μπάρα","load","total-barbell"),
        E("db-bench","Dumbbell bench press (pair)","Πιέσεις πάγκου με δύο αλτήρες","Chest / Στήθος","Dumbbells / Αλτήρες","load","per-dumbbell"),
        E("push-up","Floor push-up","Κάμψεις στο έδαφος","Chest / Στήθος","None / Χωρίς εξοπλισμό","body","none"),
        E("lat-pull","Lat pulldown","Έλξεις τροχαλίας","Back / Πλάτη","Cable machine / Τροχαλία"),
        E("row","Seated cable row","Καθιστή κωπηλατική τροχαλίας","Back / Πλάτη","Cable machine / Τροχαλία"),
        E("pull-up","Pull-up","Μονόζυγο","Back / Πλάτη","Pull-up bar / Μονόζυγο","body","none"),
        E("weighted-pull","Weighted pull-up","Μονόζυγο με πρόσθετο βάρος","Back / Πλάτη","Pull-up bar / Μονόζυγο","added","external"),
        E("assisted-pull","Machine assisted pull-up","Μονόζυγο με υποβοήθηση μηχανήματος","Back / Πλάτη","Assisted machine / Μηχάνημα υποβοήθησης","assistance","machine"),
        E("db-press","Seated dumbbell shoulder press (pair)","Καθιστές πιέσεις ώμων με δύο αλτήρες","Shoulders / Ώμοι","Dumbbells / Αλτήρες","load","per-dumbbell"),
        E("lateral","Dumbbell lateral raise (pair)","Πλάγιες εκτάσεις με δύο αλτήρες","Shoulders / Ώμοι","Dumbbells / Αλτήρες","load","per-dumbbell"),
        E("curl","Dumbbell curl (pair, simultaneous)","Ταυτόχρονες κάμψεις δικεφάλων με δύο αλτήρες","Arms / Χέρια","Dumbbells / Αλτήρες","load","per-dumbbell"),
        E("triceps","Cable triceps pushdown","Πιέσεις τρικεφάλων τροχαλίας","Arms / Χέρια","Cable machine / Τροχαλία"),
        E("plank","Forearm plank","Σανίδα στους πήχεις","Core / Κορμός","None / Χωρίς εξοπλισμό","duration","none"),
        E("walk","Treadmill walk","Περπάτημα σε διάδρομο","Full body / Ολόσωμο","Treadmill / Διάδρομος","distance","none") ];
    public static string Normalize(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Replace('ς','σ');
    public static bool Matches(GymExercise e, string query) => Normalize(e.Display).Contains(Normalize(query.Trim()));
}
