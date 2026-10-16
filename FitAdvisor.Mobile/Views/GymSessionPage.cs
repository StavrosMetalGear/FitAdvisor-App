using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class GymSessionPage : ContentPage
{
    private readonly SqlitePersonalDiary diary;
    private GymSession? session;
    private GymSession[] history = [];
    private GymExercise[] custom = [];
    private WorkoutRecord[] workouts = [];
    private GymRoutine[] routines = [];
    private string? activeExercise;
    private GymPreferences prefs = new();
    private readonly VerticalStackLayout body = new() { Padding = 16, Spacing = 10 };
    private readonly Label clock = new() { FontSize = 20 };
    private readonly Label restLabel = new() { FontSize = 18 };
    private readonly Label status = new();
    private readonly IDispatcherTimer ticker;
    private bool busy, loaded, alerted;
    public GymSessionPage(SqlitePersonalDiary diary)
    {
        this.diary = diary; Title = "Gym Session / Γυμναστήριο";
        Content = new ScrollView { Content = body };
        ticker = Dispatcher.CreateTimer(); ticker.Interval = TimeSpan.FromSeconds(2); ticker.Tick += (_, _) => Tick();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); await Run(async () => {
            routines = await diary.GetGymRoutinesAsync(); history = await diary.GetGymSessionsAsync(); custom = await diary.GetCustomExercisesAsync(); prefs = await diary.GetGymPreferencesAsync();
            workouts = (await diary.GetWorkoutsAsync()).ToArray();
            if (!loaded || session?.Status == "active") session = history.SingleOrDefault(s => s.Status == "active");
            else if(session!=null)session=history.FirstOrDefault(s=>s.Id==session.Id);
            loaded = true; Render();
            if (session?.Status == "active") status.Text = L("Saved session recovered. Pause when needed.","Ανακτήθηκε η συνεδρία. Κάνε παύση όποτε χρειάζεται.");
        }); ticker.Start();
    }
    protected override void OnDisappearing() { ticker.Stop(); base.OnDisappearing(); }
    private string L(string en, string el) => AppearancePreferences.Language == 1 ? el : en;
    private Button Button(string text, Func<Task> action, string? id = null)
    {
        var b = DesignComponents.Secondary(text); if (id != null) b.AutomationId = id;
        SemanticProperties.SetDescription(b,text); b.Clicked += async (_, _) => await Run(action); return b;
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; body.IsEnabled = false; ticker.Stop();
        try { await action(); }
        catch (ArgumentException e) { status.Text = e.Message; }
        catch (Exception) { status.Text = L("Could not save. Retry before leaving; the last saved data is retained.","Η αποθήκευση απέτυχε. Δοκίμασε ξανά πριν φύγεις· τα αποθηκευμένα δεδομένα διατηρούνται."); }
        finally { busy = false; body.IsEnabled = true; if(Shell.Current?.CurrentPage==this)ticker.Start(); }
    }
    private async Task Store(GymSession next)
    {
        await diary.SaveGymSessionAsync(next); session = next; history = await diary.GetGymSessionsAsync(); workouts=(await diary.GetWorkoutsAsync()).ToArray(); Render();
    }
    public async Task LaunchPlan(WorkoutRecord plan)
    {
        history = await diary.GetGymSessionsAsync();
        if (history.Any(s => s.Status == "active")) { session = history.Single(s => s.Status == "active"); loaded = true; return; }
        var now = DateTimeOffset.UtcNow;
        var exercises=plan.Sets.Select(s=>s.Exercise).Distinct().Select(name=>new GymExercise(Guid.NewGuid().ToString(),name,name,"Plan / Πρόγραμμα","Unspecified external load / Μη καθορισμένο εξωτερικό φορτίο","load","external",Custom:true)).ToArray();
        var pending=plan.Sets.Select(s=>new GymSet(s.Id,exercises.Single(e=>e.English==s.Exercise).Id,s.Reps,s.LoadKg,null,null,s.Warmup,false)).ToArray();
        await Store(new(Guid.NewGuid(), plan.Title, now, new(0,now), exercises, pending, PlanId: plan.Id)); loaded = true;
    }
    public void ShowSession(Guid workoutId) { session = history.FirstOrDefault(s => s.WorkoutId == workoutId); loaded = true; }
    public async Task OpenWorkout(Guid workoutId) { history = await diary.GetGymSessionsAsync(); session = history.FirstOrDefault(s => s.WorkoutId == workoutId || s.ImportedWorkoutId == workoutId); loaded = true; }
    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        clock.Text = session == null ? "" : $"{L("Elapsed (excluding pauses)","Χρόνος (χωρίς παύσεις)")}: {TimeSpan.FromSeconds(session.Clock.Elapsed(now)):hh\\:mm\\:ss} · {(session.Clock.RunningSince == null ? L("Paused","Παύση") : L("Running","Σε εξέλιξη"))}";
        restLabel.Text = session?.Rest is { Active:true } r ? $"{L("Rest","Διάλειμμα")}: {Math.Ceiling(r.Remaining(now))} s {(r.Clock?.RunningSince == null ? L("Paused","Παύση") : "")}" : L("Rest timer off / skipped","Χρονόμετρο διαλείμματος ανενεργό");
        if (session?.Rest is { Active:true } rest && rest.Remaining(now) == 0 && !alerted) {
#if ANDROID
            if (Platform.CurrentActivity is not MainActivity {IsForeground:true}) return;
#endif
            alerted = true;
            if (prefs.Vibration) { try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300)); } catch { } }
#if ANDROID
            if (prefs.Sound) { try { var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 60); tone.StartTone(Android.Media.Tone.PropBeep, 250); Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500),()=>tone.Dispose()); } catch { } }
#endif
        }
    }
    private void Render()
    {
        body.Clear(); body.Add(status); status.Text = "";
        if (session == null) {
            var latest=history.Where(s=>s.Status=="finished").OrderByDescending(s=>s.StartedAt).FirstOrDefault();
            if(latest!=null)body.Add(Button(L("Repeat previous workout","Επανάληψη προηγούμενης προπόνησης"),async()=>await Store(GymRoutine.From(latest,latest.Title).Start(DateTimeOffset.UtcNow))));
            foreach(var routine in routines) {
                body.Add(Button(L("Start routine: ","Έναρξη ρουτίνας: ")+routine.Title,async()=>await Store(routine.Start(DateTimeOffset.UtcNow))));
                body.Add(Button(L("Edit routine: ","Επεξεργασία ρουτίνας: ")+routine.Title,()=>EditRoutine(routine)));
            }
            body.Add(Button(L("Start session","Έναρξη συνεδρίας"), async () => {
                var title = await DisplayPromptAsync(L("Session","Συνεδρία"), L("Session title","Τίτλος συνεδρίας"), initialValue: "Gym Session"); if(title == null) return;
                var now = DateTimeOffset.UtcNow; await Store(new(Guid.NewGuid(),title,now,new(0,now),[],[]));
            },"GymStart"));
        } else {
            body.Add(new Label { Text = session.Title + (session.Effort is {} effort ? $" · Effort / Προσπάθεια {effort}/10 (subjective)" : ""), FontSize = 22 }); body.Add(clock);
            if(session.Status == "active") {
                body.Add(Button(session.Clock.RunningSince == null ? L("Resume session","Συνέχεια συνεδρίας") : L("Pause session","Παύση συνεδρίας"), async () => await Store(session with { Clock = session.Clock.RunningSince == null ? session.Clock.Resume(DateTimeOffset.UtcNow) : session.Clock.Pause(DateTimeOffset.UtcNow) }),"GymPause"));
                body.Add(Button(L("Add exercise","Προσθήκη άσκησης"), AddExercise,"GymAddExercise"));

                if (prefs.RestEnabled) RenderRest();
            }

            if(session.Exercises.Length>0 && !session.Exercises.Any(e=>e.Id==activeExercise))activeExercise=session.Exercises[0].Id;
            foreach(var e in session.Exercises) {
                body.Add(Button((e.Id==activeExercise?"▶ ":"")+e.English+" / "+e.Greek,()=>{activeExercise=e.Id;Render();return Task.CompletedTask;}));
                if(e.Id==activeExercise)RenderExercise(e);
            }
            if(session.Exercises.Length>0)body.Add(Button(L("Save as reusable routine","Αποθήκευση ως ρουτίνα"),async()=>{var title=await DisplayPromptAsync("Routine / Ρουτίνα","Name / Όνομα",initialValue:session.Title);if(title==null)return;await diary.SaveGymRoutineAsync(GymRoutine.From(session,title));routines=await diary.GetGymRoutinesAsync();status.Text="Routine saved / Αποθηκεύτηκε";}));
            body.Add(DesignComponents.Caption($"{L("Working external-load volume","Όγκος κύριων σετ εξωτερικού φορτίου")}: {session.Volume:g} kg·reps. " + L("Loaded reps only, no warm-ups/bodyweight/added/assistance. Paired dumbbells count twice per rep; not comparable across exercises or machines.","Μόνο φορτισμένες επαναλήψεις, χωρίς προθέρμανση/σωματικό βάρος/πρόσθετο/υποβοήθηση. Ζεύγος αλτήρων μετρά διπλά ανά επανάληψη· όχι σύγκριση μεταξύ ασκήσεων ή μηχανημάτων.")));
            if(session.Status == "active") {
                body.Add(Button(L("Finish & review","Ολοκλήρωση & ανασκόπηση"), Finish,"GymFinish"));
                body.Add(Button(L("Cancel session","Ακύρωση συνεδρίας"), async () => { if(await DisplayAlertAsync(L("Cancel session?","Ακύρωση συνεδρίας;"),L("Saved sets remain in a cancelled session, excluded from totals and records.","Τα σετ διατηρούνται ως ακυρωμένα, εκτός συνόλων και ρεκόρ."),L("Cancel session","Ακύρωση"),L("Keep session","Διατήρηση"))) await Store(session with {Status="cancelled", Clock=session.Clock.Pause(DateTimeOffset.UtcNow), Rest=null}); }));
            } else {
                body.Add(Button(session.Status=="cancelled"?L("Resume cancelled session","Ανάκτηση ακυρωμένης συνεδρίας"):L("Undo finish / resume","Αναίρεση ολοκλήρωσης / συνέχεια"), async () => { if(await DisplayAlertAsync(L("Resume session?","Συνέχεια συνεδρίας;"),L("Remove any completed workout total and resume these saved sets?","Αφαίρεση τυχόν ολοκληρωμένου συνόλου και συνέχεια αποθηκευμένων σετ;"),L("Resume","Συνέχεια"),L("Back","Πίσω"))) await Store(session with {Status="active", FinishedAt=null, ImportedWorkoutId=null, Enrichment=null, Clock=session.Clock.Resume(DateTimeOffset.UtcNow)}); }));
                if(session.Status=="finished")body.Add(Button(L("Link imported strength session","Σύνδεση εισαγόμενης προπόνησης"), LinkImport));
                if(session.ImportedWorkoutId is {} linked && workouts.FirstOrDefault(w=>w.Id==linked) is {} imported)
                    body.Add(new Label {Text=$"Linked / Συνδεδεμένο: {imported.SourcePackage} · {imported.StartTime?.ToLocalTime():g} · {imported.DurationSeconds/60:g} min · Health Connect; one workout total."});
                if(session.Enrichment is {} sensor)body.Add(new Label {Text=sensor.Summary});
                if(session.ImportedWorkoutId != null)body.Add(Button(L("Read optional synced measurements","Ανάγνωση προαιρετικών συγχρονισμένων μετρήσεων"),SensorDialog));
                if(session.ImportedWorkoutId != null) body.Add(Button(L("Unlink import","Αποσύνδεση εισαγωγής"),async()=> await Store(session with {ImportedWorkoutId=null,Enrichment=null})));
                body.Add(Button(L("Back to new session","Νέα συνεδρία"), () => { session=null; Render(); return Task.CompletedTask; }));
            }
        }
        body.Add(Button(L("Preferences / Rest timer","Προτιμήσεις / Διάλειμμα"), PreferencesDialog));
        if(session?.Status=="active")body.Add(Button(L("Custom exercise","Προσαρμοσμένη άσκηση"), CustomExercise));
        body.Add(new Label {Text=L("Session history & trends","Ιστορικό & τάσεις"),FontSize=22});
        foreach(var s in history.OrderByDescending(s=>s.StartedAt).Take(30)) {
            body.Add(Button($"{s.StartedAt.ToLocalTime():g} · {s.Title} · {s.Status} · {s.Sets.Count(x=>x.Completed)} sets · {s.Volume:g} kg·reps", () => {session=s;Render();return Task.CompletedTask;}));
        }
        Tick();
    }
    private void RenderExercise(GymExercise e)
    {
        var card = new VerticalStackLayout {Spacing=10}; card.Add(new Label {Text=e.Display,FontSize=20});
        var details=new VerticalStackLayout {Spacing=8,IsVisible=false};
        if(session!.Status=="active") {
            details.Add(Button(L("Move exercise earlier","Μετακίνηση νωρίτερα"),async()=>{var order=session.Exercises.ToArray();var index=Array.FindIndex(order,x=>x.Id==e.Id);if(index>0){(order[index-1],order[index])=(order[index],order[index-1]);await Store(session with {Exercises=order});}}));
            details.Add(Button(L("Move exercise later","Μετακίνηση αργότερα"),async()=>{var order=session.Exercises.ToArray();var index=Array.FindIndex(order,x=>x.Id==e.Id);if(index<order.Length-1){(order[index+1],order[index])=(order[index],order[index+1]);await Store(session with {Exercises=order});}}));
        }
        var previous=history.Where(s=>s.Status=="finished" && s.Id!=session!.Id).OrderByDescending(s=>s.StartedAt).FirstOrDefault(s=>s.Exercises.Any(x=>x.Key==e.Key));
        details.Add(DesignComponents.Caption(previous==null ? L("Previous: none","Προηγούμενα: κανένα") : $"{L("Previous","Προηγούμενα")} {previous.StartedAt.ToLocalTime():d}: " + string.Join("; ",previous.Sets.Where(s=>s.ExerciseId==e.Id && s.Completed).Select(s=>s.Summary(e)))));
        details.Add(DesignComponents.Caption(GymRecords.Describe(history.Where(s=>s.Id!=session!.Id),e)));
        var trend=history.Where(s=>s.Id!=session!.Id && s.Status=="finished" && s.Exercises.Any(x=>x.Key==e.Key)).OrderBy(s=>s.StartedAt).TakeLast(5).ToArray();
        if(trend.Length>0) {
            details.Add(new Label {Text=L("Recent working-set trend (oldest → newest)","Τάση κύριων σετ (παλιότερο → νεότερο)")});
            foreach(var past in trend) {
                var relevant=past.Sets.Where(s=>s.ExerciseId==e.Id && s.Completed && !s.Warmup).ToArray();
                details.Add(DesignComponents.Caption($"{past.StartedAt.ToLocalTime():d}: "+string.Join("; ",relevant.Select(s=>s.Summary(e)))));
            }
        }
        if(session!.Status=="active")card.Add(Button(L("Log completed set","Καταγραφή ολοκληρωμένου σετ"),()=>EditSet(e,null)));
        if(!prefs.HideTechnique) card.Add(Button(L("Exercise technique","Τεχνική άσκησης"), ()=>Technique(e)));
        foreach(var set in session!.Sets.Where(s=>s.ExerciseId==e.Id)) {
            card.Add(new Label {Text=set.Summary(e)});
            details.Add(Button(L("Edit set","Επεξεργασία σετ")+" · "+set.Summary(e),()=>EditSet(e,set)));
            card.Add(Button(set.Completed ? L("Undo set completion","Αναίρεση ολοκλήρωσης σετ") : L("Complete set","Ολοκλήρωση σετ"),async()=> {
                var nextRest=session.Rest;
                if(!set.Completed && session.Status=="active" && prefs.RestEnabled && prefs.AutoRest){nextRest=new(true,e.RestSeconds??prefs.DefaultRestSeconds,new(0,DateTimeOffset.UtcNow));alerted=false;}
                await Store(session with {Sets=session.Sets.Select(s=>s.Id==set.Id?s with {Completed=!s.Completed}:s).ToArray(),Rest=nextRest});
            }));
            details.Add(Button(L("Delete set","Διαγραφή σετ")+" · "+set.Summary(e),async()=> { if(await DisplayAlertAsync(L("Delete set?","Διαγραφή σετ;"),set.Summary(e),L("Delete","Διαγραφή"),L("Keep","Διατήρηση"))) await Store(session with {Sets=session.Sets.Where(s=>s.Id!=set.Id).ToArray()}); }));
        }
        if(session.Status=="active") {

            details.Add(Button(L("Exercise rest override","Διάλειμμα άσκησης"),async()=> {
                var text=await DisplayPromptAsync(L("Rest override","Διάλειμμα άσκησης"),L("Seconds, 5–3600 (blank uses default)","Δευτερόλεπτα, 5–3600 (κενό = προεπιλογή)"),initialValue:e.RestSeconds?.ToString(),keyboard:Keyboard.Numeric);
                if(text==null)return; int? seconds=string.IsNullOrWhiteSpace(text)?null:(int)Number(text);
                await Store(session with {Exercises=session.Exercises.Select(x=>x.Id==e.Id?x with {RestSeconds=seconds}:x).ToArray()});
            }));
        }
        card.Add(Button(L("Edit sets, reorder & history","Επεξεργασία σετ, σειρά & ιστορικό"),()=>{details.IsVisible=!details.IsVisible;return Task.CompletedTask;}));card.Add(details);
        body.Add(DesignComponents.Card(card));
    }
    private static double Number(string? text) => double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out var n) && double.IsFinite(n) ? n : throw new ArgumentException("Enter a valid number / Έγκυρος αριθμός");
    private async Task EditSet(GymExercise e,GymSet? old)
    {
        int? reps=null; double? kg=null, seconds=null, metres=null;
        async Task<string?> Field(string label,object? initial) => await DisplayPromptAsync(e.English,label,initialValue:Convert.ToString(initial,CultureInfo.CurrentCulture),keyboard:Keyboard.Numeric);
        if(e.Mode is "load" or "body" or "added" or "assistance") {var t=await Field(L("Whole-number reps","Ακέραιες επαναλήψεις"),old?.Reps);if(t==null)return; if(!int.TryParse(t,out var r))throw new ArgumentException("Whole-number reps required.");reps=r;}
        if(e.Mode is "load" or "added" or "assistance") {var t=await Field(e.LoadLabel,old?.LoadKg);if(t==null)return;kg=Number(t);}
        if(e.Mode=="duration") {var t=await Field(L("Duration (seconds)","Διάρκεια (δευτερόλεπτα)"),old?.Seconds);if(t==null)return;seconds=Number(t);}
        if(e.Mode=="distance") {var t=await Field(L("Distance (metres)","Απόσταση (μέτρα)"),old?.DistanceM);if(t==null)return;metres=Number(t);}
        var type=await DisplayActionSheetAsync(L("Set type","Τύπος σετ"),L("Back","Πίσω"),null,"Working / Κύριο","Warm-up / Προθέρμανση");
        if(type is not ("Working / Κύριο" or "Warm-up / Προθέρμανση"))return;
        var set=new GymSet(old?.Id??Guid.NewGuid(),e.Id,reps,kg,seconds,metres,type.StartsWith("Warm"),old?.Completed??true);set.Validate(e);
        var rest=session!.Rest;
        if(old==null && prefs.RestEnabled && prefs.AutoRest) {rest=new(true,e.RestSeconds??prefs.DefaultRestSeconds,new(0,DateTimeOffset.UtcNow));alerted=false;}
        var prior=GymRecords.Comparable(history.Where(s=>s.Id!=session.Id),e);
        var pr=set.Completed && !set.Warmup && prior.Length>0 && ((e.Mode is "load" or "added" && kg>prior.Max(s=>s.LoadKg)) || (e.Mode=="assistance" && kg<prior.Min(s=>s.LoadKg)) || prior.Any(s=>s.LoadKg==kg && reps>s.Reps) && reps>prior.Where(s=>s.LoadKg==kg).Max(s=>s.Reps));
        await Store(session with {Sets=old==null?session.Sets.Append(set).ToArray():session.Sets.Select(s=>s.Id==set.Id?set:s).ToArray(),Rest=rest});
        if(pr)status.Text=L("Personal best for this variation. Keep movement controlled and choose comfortable resistance.","Προσωπική βελτίωση σε αυτή την παραλλαγή. Ελεγχόμενη κίνηση και κατάλληλη αντίσταση.");
    }
    private async Task AddExercise()
    {
        await Navigation.PushAsync(new ExercisePickerPage(GymCatalogue.Starter.Concat(custom).Where(e=>!session!.Exercises.Any(x=>x.Id==e.Id)).ToArray(), async selected => {
        if(selected!=null && !selected.Custom && selected.Convention=="machine") {
            var machine=await DisplayPromptAsync(L("Identify this machine","Ταυτότητα μηχανήματος"),L("Enter gym + machine/model identifier. Records must use the same machine; select this saved variation next time.","Γράψε γυμναστήριο + ταυτότητα/μοντέλο μηχανήματος. Σύγκρινε μόνο το ίδιο μηχάνημα· διάλεξε αυτή την παραλλαγή την επόμενη φορά."));
            if(machine==null)return;DiaryValidation.Text(machine,70,"Machine identity");
            var equipment=selected.Equipment+" · "+machine.Trim();
            var saved=custom.FirstOrDefault(x=>x.English==selected.English && x.Equipment==equipment && x.Mode==selected.Mode && x.Convention==selected.Convention);
            selected=saved??selected with {Id=Guid.NewGuid().ToString(),Equipment=equipment,Custom=true};
            if(saved==null){await diary.SaveCustomExerciseAsync(selected);custom=await diary.GetCustomExercisesAsync();}
        }
        if(selected!=null && !session!.Exercises.Any(e=>e.Id==selected.Id))await Store(session! with {Exercises=session!.Exercises.Append(selected).ToArray()});
            }));
    }
    private async Task EditRoutine(GymRoutine routine)
    {
        var draft=routine;var page=new ContentPage {Title="Edit routine / Ρουτίνα"};var stack=new VerticalStackLayout {Padding=16,Spacing=10};var error=new Label();
        void RenderDraft() {
            stack.Clear();stack.Add(error);
            var title=new Entry {Text=draft.Title};title.TextChanged+=(_,_)=>draft=draft with {Title=title.Text??""};stack.Add(title);
            Button Local(string text,Func<Task> action) {var b=new Button {Text=text};b.Clicked+=async(_,_)=>{try{await action();}catch(ArgumentException e){error.Text=e.Message;}};return b;}
            foreach(var exercise in draft.Exercises) {
                stack.Add(new Label {Text=exercise.Display});
                stack.Add(Local("Move earlier / Νωρίτερα",()=>{var order=draft.Exercises.ToArray();var i=Array.FindIndex(order,e=>e.Id==exercise.Id);if(i>0)(order[i-1],order[i])=(order[i],order[i-1]);draft=draft with {Exercises=order};RenderDraft();return Task.CompletedTask;}));
                stack.Add(Local("Remove / Αφαίρεση",()=>{draft=draft with {Exercises=draft.Exercises.Where(e=>e.Id!=exercise.Id).ToArray(),Sets=draft.Sets.Where(s=>s.ExerciseId!=exercise.Id).ToArray()};RenderDraft();return Task.CompletedTask;}));
                stack.Add(Local("Add suggested set / Προτεινόμενο σετ",()=>{
                    var repMode=exercise.Mode is "load" or "body" or "added" or "assistance";var loadMode=exercise.Mode is "load" or "added" or "assistance";
                    draft=draft with {Sets=draft.Sets.Append(new GymSet(Guid.NewGuid(),exercise.Id,repMode?8:null,loadMode?0:null,exercise.Mode=="duration"?60:null,exercise.Mode=="distance"?500:null,false,false)).ToArray()};RenderDraft();return Task.CompletedTask;
                }));
                foreach(var set in draft.Sets.Where(s=>s.ExerciseId==exercise.Id)) {
                    stack.Add(Local(set.Summary(exercise)+" · Edit suggestion",async()=>{
                        var reps=set.Reps;var load=set.LoadKg;var seconds=set.Seconds;var distance=set.DistanceM;
                        if(reps!=null){var text=await page.DisplayPromptAsync("Suggested reps","Whole number",initialValue:reps.ToString(),keyboard:Keyboard.Numeric);if(text==null)return;reps=int.TryParse(text,out var r)?r:throw new ArgumentException("Whole reps required");}
                        if(load!=null){var text=await page.DisplayPromptAsync("Suggested kg",exercise.LoadLabel,initialValue:load.ToString(),keyboard:Keyboard.Numeric);if(text==null)return;load=Number(text);}
                        if(seconds!=null){var text=await page.DisplayPromptAsync("Suggested seconds","Duration",initialValue:seconds.ToString(),keyboard:Keyboard.Numeric);if(text==null)return;seconds=Number(text);}
                        if(distance!=null){var text=await page.DisplayPromptAsync("Suggested metres","Distance",initialValue:distance.ToString(),keyboard:Keyboard.Numeric);if(text==null)return;distance=Number(text);}
                        draft=draft with {Sets=draft.Sets.Select(s=>s.Id==set.Id?s with {Reps=reps,LoadKg=load,Seconds=seconds,DistanceM=distance}:s).ToArray()};draft.Validate();RenderDraft();
                    }));
                }
            }
            stack.Add(Local("Add exercise / Προσθήκη άσκησης",async()=>await Navigation.PushAsync(new ExercisePickerPage(GymCatalogue.Starter.Concat(custom).Where(e=>!draft.Exercises.Any(x=>x.Id==e.Id)).ToArray(),e=>{draft=draft with {Exercises=draft.Exercises.Append(e).ToArray()};RenderDraft();return Task.CompletedTask;}))));
            stack.Add(Local("Save routine / Αποθήκευση",async()=>{await diary.SaveGymRoutineAsync(draft);routines=await diary.GetGymRoutinesAsync();await Navigation.PopAsync();Render();}));
        }
        RenderDraft();page.Content=new ScrollView {Content=stack};await Navigation.PushAsync(page);
    }
    private async Task CustomExercise()
    {
        var en=await DisplayPromptAsync("Custom exercise","English name (include exact equipment/variation)");if(en==null)return;
        var el=await DisplayPromptAsync("Προσαρμοσμένη άσκηση","Ελληνικό όνομα");if(el==null)return;
        var muscle=await DisplayPromptAsync("Muscle / Μύες","Muscle group / Μυϊκή ομάδα");if(muscle==null)return;
        var equipment=await DisplayPromptAsync("Equipment / Εξοπλισμός","Exact machine/model or equipment / Ακριβές μηχάνημα ή εξοπλισμός");if(equipment==null)return;
        string[] fieldLabels=["Reps + load / Επαναλήψεις + φορτίο","Bodyweight reps / Επαναλήψεις σωματικού βάρους","Reps + added kg / Επαναλήψεις + πρόσθετα kg","Reps + assistance kg / Επαναλήψεις + υποβοήθηση kg","Duration (seconds) / Διάρκεια (δευτερόλεπτα)","Distance (metres) / Απόσταση (μέτρα)"];
        var field=await DisplayActionSheetAsync("Fields / Πεδία","Back / Πίσω",null,fieldLabels);var index=Array.IndexOf(fieldLabels,field);if(index<0)return;
        var mode=new[]{"load","body","added","assistance","duration","distance"}[index];
        var convention="none";
        if(mode is "load" or "added" or "assistance") {
            string[] loadLabels=["Total barbell including bar (kg) / Συνολική μπάρα (kg)","Per dumbbell, simultaneous pair (kg) / Ανά αλτήρα, ταυτόχρονο ζεύγος (kg)","Machine reading (kg) / Ένδειξη μηχανήματος (kg)","External total, excluding bodyweight (kg) / Εξωτερικό σύνολο χωρίς σωματικό βάρος (kg)"];
            var choice=await DisplayActionSheetAsync("Weight meaning / Σημασία βάρους","Back / Πίσω",null,loadLabels);var loadIndex=Array.IndexOf(loadLabels,choice);if(loadIndex<0)return;
            convention=new[]{"total-barbell","per-dumbbell","machine","external"}[loadIndex];
        }
        var e=new GymExercise(Guid.NewGuid().ToString(),en,el,muscle,equipment,mode,convention,Custom:true);await diary.SaveCustomExerciseAsync(e);custom=await diary.GetCustomExercisesAsync();await Store(session! with {Exercises=session!.Exercises.Append(e).ToArray()});
    }
    private async Task Finish()
    {
        if(!session!.Sets.Any(s=>s.Completed))throw new ArgumentException("Complete at least one set / Ολοκλήρωσε τουλάχιστον ένα σετ");
        var summary=string.Join("\n",session.Exercises.Select(e=>e.English+": "+session.Sets.Count(s=>s.ExerciseId==e.Id&&s.Completed)+" completed sets"));
        if(!await DisplayAlertAsync(L("Review session","Ανασκόπηση συνεδρίας"),summary+$"\n{session.Volume:g} kg·reps. Pending sets excluded. One completed workout entry.",L("Finish","Ολοκλήρωση"),L("Keep logging","Συνέχεια")))return;
        var effortText=await DisplayPromptAsync(L("Optional session effort 1–10","Προαιρετική προσπάθεια 1–10"),L("1 very easy · 3 moderate · 5 hard · 7 very hard · 10 maximal. Subjective, separate from sensor measurements. Leave blank to skip.","1 πολύ εύκολη · 3 μέτρια · 5 δύσκολη · 7 πολύ δύσκολη · 10 μέγιστη. Υποκειμενική, ανεξάρτητη από αισθητήρες. Κενό = παράλειψη."),keyboard:Keyboard.Numeric);
        int? effort=string.IsNullOrWhiteSpace(effortText)?null:int.TryParse(effortText,out var rating)&&rating>=1&&rating<=10?rating:throw new ArgumentException("Effort must be 1–10.");
        var now=DateTimeOffset.UtcNow;await Store(session with {Effort=effort,Status="finished",FinishedAt=now,Clock=session.Clock.Pause(now),Rest=null});
        status.Text=L("Session saved. Review sets below; you can undo completion.","Η συνεδρία αποθηκεύτηκε. Έλεγξε τα σετ· μπορείς να αναιρέσεις την ολοκλήρωση.");
    }
    private void RenderRest()
    {
        body.Add(restLabel);
        body.Add(Button(L("Start / reset rest","Έναρξη / επαναφορά διαλείμματος"),async()=>{alerted=false;await Store(session! with {Rest=new(true,prefs.DefaultRestSeconds,new(0,DateTimeOffset.UtcNow))});}));
        if(session!.Rest is {Active:true} r) {
            body.Add(Button(L("Pause / resume rest","Παύση / συνέχεια διαλείμματος"),async()=>await Store(session with {Rest=r with {Clock=r.Clock?.RunningSince==null?(r.Clock??new()).Resume(DateTimeOffset.UtcNow):r.Clock.Pause(DateTimeOffset.UtcNow)}})));
            body.Add(Button(L("Extend +30 s","Παράταση +30 δ"),async()=>{alerted=false;await Store(session with {Rest=r with {TargetSeconds=Math.Min(7200,r.TargetSeconds+30)}});}));
            body.Add(Button(L("Skip rest","Παράλειψη διαλείμματος"),async()=>await Store(session with {Rest=null})));
        }
    }
    private async Task PreferencesDialog()
    {
        var page=new ContentPage {Title=L("Gym preferences","Προτιμήσεις γυμναστηρίου")};var stack=new VerticalStackLayout {Padding=20,Spacing=12};
        Switch Toggle(string label,bool value) {var sw=new Switch {IsToggled=value};SemanticProperties.SetDescription(sw,label);stack.Add(new Label {Text=label});stack.Add(sw);return sw;}
        var enabled=Toggle(L("Enable rest timer","Ενεργό διάλειμμα"),prefs.RestEnabled);
        var auto=Toggle(L("Start after a new completed set","Έναρξη μετά από νέο ολοκληρωμένο σετ"),prefs.AutoRest);
        stack.Add(new Label {Text=L("Default seconds (5–3600)","Προεπιλογή δευτερολέπτων (5–3600)")});var duration=new Entry {Text=prefs.DefaultRestSeconds.ToString(),Keyboard=Keyboard.Numeric};stack.Add(duration);
        var vibrate=Toggle(L("Vibration while this page is open","Δόνηση με ανοιχτή σελίδα"),prefs.Vibration);
        var sound=Toggle(L("Sound while this page is open","Ήχος με ανοιχτή σελίδα"),prefs.Sound);
        var hide=Toggle(L("Hide technique during sessions","Απόκρυψη τεχνικής στις συνεδρίες"),prefs.HideTechnique);
        stack.Add(DesignComponents.Caption(L("No background alerts in this version. Timer timestamps remain accurate while locked; reopen to see remaining time. No notification permission is requested.","Χωρίς ειδοποιήσεις παρασκηνίου σε αυτή την έκδοση. Ο χρόνος διατηρείται στο κλείδωμα· άνοιξε για το υπόλοιπο. Δεν ζητείται άδεια ειδοποιήσεων.")));
        var save=DesignComponents.Secondary(L("Save & back","Αποθήκευση & πίσω"));save.Clicked+=async(_,_)=>{
            try {if(!int.TryParse(duration.Text,out var seconds))throw new ArgumentException("Whole seconds required.");var next=new GymPreferences(enabled.IsToggled,auto.IsToggled,seconds,vibrate.IsToggled,sound.IsToggled,hide.IsToggled);await diary.SaveGymPreferencesAsync(next);prefs=next;
                if(!prefs.RestEnabled && session?.Status=="active")await Store(session with {Rest=null});await Navigation.PopAsync();Render();}
            catch(ArgumentException e){await page.DisplayAlertAsync("Check value",e.Message,"OK");}
        };stack.Add(save);page.Content=new ScrollView {Content=stack};await Navigation.PushAsync(page);
    }
    private async Task Technique(GymExercise e)
    {
        var guide=GymTechnique.For(e);var stack=new VerticalStackLayout {Padding=20,Spacing=16};
        var page=new ContentPage {Title=L("Exercise technique","Τεχνική άσκησης")};
        stack.Add(new Label {Text=e.Display,FontSize=24});
        stack.Add(new Label {Text=L("Controlled movement and appropriate resistance. Stop if an exercise causes pain. Ask a qualified trainer to check your form.","Ελεγχόμενη κίνηση και κατάλληλη αντίσταση. Σταμάτησε αν η άσκηση προκαλεί πόνο. Ζήτησε έλεγχο τεχνικής από καταρτισμένο προπονητή."),FontSize=18});
        if(guide==null)stack.Add(new Label {Text=L("Reviewed guidance is unavailable for this exact exercise/equipment variation. No technique instructions supplied.","Δεν υπάρχει ελεγμένη καθοδήγηση για αυτή την ακριβή άσκηση/εξοπλισμό. Δεν παρέχονται οδηγίες τεχνικής."),FontSize=18});
        else {
            string[] labels=[L("1 · Setup","1 · Στήσιμο"),L("2 · Movement / hold","2 · Κίνηση / κράτημα"),L("3 · Return / finish","3 · Επιστροφή / τέλος")];
            for(var i=0;i<guide.Frames.Length;i++) {
                var frame=guide.Frames[i];var cue=AppearancePreferences.Language==1?frame.Greek:frame.English;
                stack.Add(new Label {Text=labels[i],FontSize=22,FontAttributes=FontAttributes.Bold});
                var image=new Image {Source=frame.Image,HeightRequest=240,Aspect=Aspect.AspectFit};SemanticProperties.SetDescription(image,e.Display+". "+labels[i]+". "+cue);stack.Add(image);stack.Add(new Label {Text=cue,FontSize=18});
            }
            stack.Add(DesignComponents.Caption(L("Source-checked photo sequence, 2026-10-16; not a clinical or trainer assessment. Public-domain/Unlicense: Free Exercise DB (Yuhonas / original dataset Ollie Jennings). Return frames reuse the setup pose; plank uses the hold photo. Machine geometry varies: use only the shown variation. Breathe continuously; stop if painful.","Φωτογραφίες ελεγμένες ως προς την πηγή, 16/10/2026· όχι κλινική αξιολόγηση ή αξιολόγηση προπονητή. Public domain/Unlicense: Free Exercise DB (Yuhonas / αρχικό σύνολο Ollie Jennings). Η επιστροφή επαναχρησιμοποιεί την αρχική θέση· η σανίδα τη στάση κρατήματος. Τα μηχανήματα διαφέρουν: μόνο η εικονιζόμενη παραλλαγή. Συνέχισε να αναπνέεις· σταμάτησε αν πονάς.")));
            var link=DesignComponents.Secondary(L("Source (online)","Πηγή (σύνδεση)"));link.Clicked+=async(_,_)=>await Launcher.Default.OpenAsync(guide.Source);stack.Add(link);
            if(guide.Video!=null) {var video=DesignComponents.Secondary(L("NASM video (online, optional)","Βίντεο NASM (προαιρετικό, σύνδεση)"));video.Clicked+=async(_,_)=>await Launcher.Default.OpenAsync(guide.Video);stack.Add(video);}
        }
        var back=DesignComponents.Secondary(L("Back to session","Πίσω στη συνεδρία"));back.Clicked+=async(_,_)=>await Navigation.PopAsync();stack.Add(back);page.Content=new ScrollView {Content=stack};await Navigation.PushAsync(page);
    }
    private async Task SensorDialog()
    {
        var imported=(await diary.GetWorkoutsAsync()).FirstOrDefault(w=>w.Id==session!.ImportedWorkoutId)??throw new ArgumentException("Linked import unavailable.");
        var page=new ContentPage {Title="Post-sync measurements / Μετρήσεις"};var stack=new VerticalStackLayout {Padding=16,Spacing=12};
        stack.Add(new Label {Text=$"Record on band → sync Mi Fitness → import Exercise session → explicitly link → read. Exact source: {imported.SourcePackage}. Only overlapping times from this source; no competing sources merged. Missing values are unavailable. Food allowance never increases."});
        var heart=new Switch();SemanticProperties.SetDescription(heart,"Enable heart-rate read / Σφυγμοί");stack.Add(new Label {Text="Heart rate / Σφυγμοί (optional)"});stack.Add(heart);
        var energy=new Switch();SemanticProperties.SetDescription(energy,"Enable active-calorie read / Ενεργές θερμίδες");stack.Add(new Label {Text="Active energy / Ενεργές θερμίδες (optional; excludes resting energy)"});stack.Add(energy);
        var message=new Label();var read=new Button {Text="Read enabled measurements / Ανάγνωση επιλεγμένων"};stack.Add(read);stack.Add(message);
        read.Clicked+=async(_,_)=>{read.IsEnabled=false;try {
#if ANDROID
            var snapshot=await AndroidGymEnrichmentReader.ReadAsync(session!,imported,heart.IsToggled,energy.IsToggled);await Store(session! with {Enrichment=snapshot});message.Text=snapshot.Summary;
#else
            message.Text="Health Connect requires Android.";
#endif
        }catch(Exception e){message.Text=e is ArgumentException?e.Message:"Measurements unavailable. Check permission and sync, then retry. Existing snapshot retained.";}finally{read.IsEnabled=true;}};
        var back=new Button {Text="Back to session / Πίσω"};back.Clicked+=async(_,_)=>await Navigation.PopAsync();stack.Add(back);page.Content=new ScrollView {Content=stack};await Navigation.PushAsync(page);
    }
    private async Task LinkImport()
    {
        var imports=(await diary.GetWorkoutsAsync()).Where(w=>!w.IsManual&&w.Kind=="gym"&&w.StartTime<session!.FinishedAt&&w.StartTime!.Value.AddSeconds(w.DurationSeconds)>session!.StartedAt).ToArray();
        if(imports.Length==0) {status.Text=L("No compatible overlapping imported strength session. Import completed sessions from Workouts first.","Δεν υπάρχει συμβατή εισαγόμενη προπόνηση δύναμης με χρονική επικάλυψη. Κάνε εισαγωγή ολοκληρωμένων συνεδριών από Προπονήσεις.");return;}
        string Label(WorkoutRecord w)=>$"{w.SourcePackage} · {w.StartTime!.Value.ToLocalTime():g} · {w.DurationSeconds/60:g} min · {w.ExternalId}";
        var choice=await DisplayActionSheetAsync(L("Explicitly link import","Ρητή σύνδεση εισαγωγής"),L("Back","Πίσω"),null,imports.Select(Label).ToArray());var selected=imports.FirstOrDefault(w=>Label(w)==choice);if(selected==null)return;
        if(await DisplayAlertAsync(L("Link this completed import?","Σύνδεση ολοκληρωμένης εισαγωγής;"),Label(selected)+L("\nIts workout total will be counted through this manual session once. Optional heart rate and active energy can be read after linking; no live monitoring.","\nΜετρά μία φορά μέσω της χειροκίνητης συνεδρίας. Προαιρετική ανάγνωση σφυγμών και ενεργών θερμίδων μετά τη σύνδεση, χωρίς ζωντανή παρακολούθηση."),L("Link","Σύνδεση"),L("Back","Πίσω"))) {await Store(session! with {ImportedWorkoutId=selected.Id,Enrichment=null});status.Text="Linked: "+Label(selected);}
    }
}
