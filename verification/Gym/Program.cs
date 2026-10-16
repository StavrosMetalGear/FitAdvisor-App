using FitAdvisor.Core;
using FitAdvisor.Storage;
using Microsoft.Data.Sqlite;

var checks=0;
void Check(bool condition,string name) {if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
async Task Reject(Func<Task> action,string name) {try {await action();throw new Exception("Accepted: "+name);}catch(ArgumentException){checks++;Console.WriteLine("PASS rejects "+name);}}
var path=Path.Combine(Path.GetTempPath(),"fitadvisor-gym-"+Guid.NewGuid()+".db");
var restoredPath=path+".restore";
try {
 var diary=new SqlitePersonalDiary(path);
 var now=new DateTimeOffset(2026,10,14,9,0,0,TimeSpan.Zero);
 var bb=GymCatalogue.Starter.Single(e=>e.Id=="bench");var db=GymCatalogue.Starter.Single(e=>e.Id=="db-bench");
 var body=GymCatalogue.Starter.Single(e=>e.Id=="push-up");var assist=GymCatalogue.Starter.Single(e=>e.Id=="assisted-pull");
 var set=new GymSet(Guid.NewGuid(),bb.Id,10,50,null,null,false);
 Check(set.Volume(bb)==500,"barbell total-load volume");
 var ds=set with {ExerciseId=db.Id,LoadKg=20};Check(ds.Volume(db)==400,"paired dumbbells per-hand convention");
 Check((set with {Warmup=true}).Volume(bb)==null,"warm-ups excluded from volume");
 Check((set with {Completed=false}).Volume(bb)==null,"pending sets excluded from volume");
 Check((set with {ExerciseId=assist.Id}).Volume(assist)==null,"assistance excluded from external volume");
 var bw=set with {ExerciseId=body.Id,LoadKg=null};bw.Validate(body);Check(bw.Volume(body)==null,"bodyweight reps without invented volume");
 var added=GymCatalogue.Starter.Single(e=>e.Id=="weighted-pull");var addedSet=set with {ExerciseId=added.Id,LoadKg=10};addedSet.Validate(added);Check(addedSet.Volume(added)==null,"added load does not invent bodyweight volume");
 var timed=GymCatalogue.Starter.Single(e=>e.Id=="plank");var timedSet=new GymSet(Guid.NewGuid(),timed.Id,null,null,60,null,false);timedSet.Validate(timed);Check(timedSet.Volume(timed)==null,"duration fields without irrelevant reps or kg");
 var distance=GymCatalogue.Starter.Single(e=>e.Id=="walk");var distanceSet=new GymSet(Guid.NewGuid(),distance.Id,null,null,null,500,false);distanceSet.Validate(distance);Check(distanceSet.Volume(distance)==null,"distance in metres without irrelevant fields");
 await Reject(()=>{(timedSet with {Reps=10}).Validate(timed);return Task.CompletedTask;},"duration with reps");
 await Reject(()=>{(set with {ExerciseId=body.Id}).Validate(body);return Task.CompletedTask;},"irrelevant bodyweight load");
 await Reject(()=>{(set with {LoadKg=double.NaN}).Validate(bb);return Task.CompletedTask;},"nonfinite load");
 var clock=new GymClock(0,now);Check(clock.Elapsed(now.AddMinutes(2))==120,"timestamp elapsed across lock");
 var paused=clock.Pause(now.AddMinutes(2));Check(paused.Elapsed(now.AddHours(1))==120,"pause excludes locked time");
 var resumed=paused.Resume(now.AddHours(1));Check(resumed.Elapsed(now.AddHours(1).AddSeconds(30))==150,"resume adds only running time");
 var rest=new GymRest(true,90,new(0,now));Check(rest.Remaining(now.AddSeconds(100))==0,"expired rest clamps at zero");
 var restPaused=rest with {Clock=rest.Clock!.Pause(now.AddSeconds(20))};Check(restPaused.Remaining(now.AddHours(1))==70,"rest pause persists");
 Check((restPaused with {TargetSeconds=120}).Remaining(now.AddHours(1))==100,"extend paused rest");
 var session=new GymSession(Guid.NewGuid(),"Test gym",now,resumed,[bb],[set],Rest:restPaused);
 await diary.SaveGymSessionAsync(session);diary=new(path);
 var recovered=(await diary.GetGymSessionsAsync()).Single();Check(recovered.Sets.Single()==set,"reopen recovers completed set");
 Check(recovered.Clock.Elapsed(now.AddHours(1).AddSeconds(30))==150 && recovered.Rest!.Remaining(now.AddHours(2))==70,"reopen recovers both timer states");
 await Reject(()=>diary.SaveGymSessionAsync(session with {Id=Guid.NewGuid()}),"two active sessions");
 await diary.SaveGymSessionAsync(session with {Sets=[set with {Completed=false}]});Check(!(await diary.GetGymSessionsAsync()).Single().Sets.Single().Completed,"undo set completion saved");
 session=session with {Status="finished",FinishedAt=now.AddHours(1).AddMinutes(2),Clock=resumed.Pause(now.AddHours(1).AddMinutes(2)),Rest=null};
 await diary.SaveGymSessionAsync(session);await diary.SaveGymSessionAsync(session);
 Check((await diary.GetWorkoutTotalsAsync()).Count==1 && (await diary.GetWorkoutsAsync()).Count==1,"idempotent finish one workout");
 Check(GymRecords.Comparable([session],bb).Length==1 && GymRecords.Comparable([session],bb with {Equipment="Different machine"}).Length==0,"equipment-exact record comparisons");
 Check(GymRecords.Comparable([session],bb with {Convention="per-dumbbell"}).Length==0,"convention-exact record comparisons");
 var repHistory=session with {Sets=[set,set with {Id=Guid.NewGuid(),LoadKg=40,Reps=12},set with {Id=Guid.NewGuid(),LoadKg=40,Reps=15}]};
 Check(GymRecords.Describe([repHistory],bb).Contains("40 kg: 15 reps"),"most reps compared separately at each load");
 var assistanceSession=session with {Id=Guid.NewGuid(),Exercises=[assist],Sets=[set with {ExerciseId=assist.Id,LoadKg=40},set with {Id=Guid.NewGuid(),ExerciseId=assist.Id,LoadKg=30}]};
 Check(GymRecords.Describe([assistanceSession],assist).Contains("30 kg"),"lower assistance record separate");
 Check(GymRecords.Comparable([assistanceSession],GymCatalogue.Starter.Single(e=>e.Id=="weighted-pull")).Length==0,"assistance never added weight");
 Check(GymCatalogue.Matches(GymCatalogue.Starter[0],"καθισματα") && GymCatalogue.Matches(bb,"bench"),"accent-insensitive Greek and English search");
 Check(GymTechnique.For(body)!=null && GymTechnique.For(body with {Equipment="Bench"})==null && GymTechnique.For(body with {Custom=true})==null,"technique exact variation and unavailable fallback");
 var imported=new WorkoutRecord(Guid.NewGuid(),DateOnly.FromDateTime(now.DateTime),0,"gym","Mi Band import",1800,null,null,[],now,now,"health-connect","com.xiaomi.wearable","import-1",now,80);
 await diary.ImportWorkoutsAsync([imported]);Check((await diary.GetWorkoutTotalsAsync()).Count==2,"unlinked import remains distinct");
 var mismatch=imported with {Id=Guid.NewGuid(),ExternalId="nonoverlap",StartTime=now.AddDays(-2)};await diary.ImportWorkoutsAsync([mismatch]);
 await Reject(()=>diary.SaveGymSessionAsync(session with {ImportedWorkoutId=mismatch.Id}),"nonoverlapping import link");
 await diary.DeleteWorkoutAsync(mismatch.Id);
 session=session with {ImportedWorkoutId=imported.Id};await diary.SaveGymSessionAsync(session);
 var invalid=await diary.CreateBackupAsync();await Reject(()=>diary.RestoreBackupAsync(invalid with {GymSessions=[session,session with {Id=Guid.NewGuid()}]}),"duplicate workout references in backup");
 Check((await diary.GetWorkoutTotalsAsync()).Count==1 && WorkoutTotals.Completed(await diary.GetWorkoutsAsync()).Count==1,"explicit link single-entry totals in both APIs");
 var updated=imported with {UpdatedAt=now.AddDays(1)};await diary.ImportWorkoutsAsync([updated]);Check((await diary.GetWorkoutTotalsAsync()).Count==1,"repeat import preserves linking");
 await Reject(()=>diary.ImportWorkoutsAsync([updated with {UpdatedAt=now.AddDays(2),StartTime=now.AddDays(-2)}]),"source edit that invalidates explicit link");
 Check((await diary.CreateBackupAsync()).GymSessions.Single().ImportedWorkoutId==imported.Id,"rejected source edit leaves backup and link valid");
 await Reject(()=>diary.DeleteWorkoutAsync(imported.Id),"deleting linked import");
 await Reject(()=>diary.SaveWorkoutAsync(session.Workout()),"editing detailed workout via legacy form");
 await diary.SaveGymSessionAsync(session with {ImportedWorkoutId=null});Check((await diary.GetWorkoutTotalsAsync()).Count==2,"unlink restores independent import total");
 session=session with {ImportedWorkoutId=null,Status="active",FinishedAt=null,Clock=session.Clock.Resume(now.AddHours(2))};await diary.SaveGymSessionAsync(session);
 Check((await diary.GetWorkoutTotalsAsync()).Count==1,"undo finish removes manual workout atomically");
 var custom=bb with {Id=Guid.NewGuid().ToString(),Custom=true,English="My bench",Greek="Δικός μου πάγκος"};await diary.SaveCustomExerciseAsync(custom);
 var pref=new GymPreferences(true,true,120,true,false,true);await diary.SaveGymPreferencesAsync(pref);
 var backup=await diary.CreateBackupAsync();var encrypted=BackupCodec.Encrypt(backup,"gym-test-passphrase-2026");
 var decrypted=BackupCodec.Decrypt(encrypted,"gym-test-passphrase-2026");var restored=new SqlitePersonalDiary(restoredPath);await restored.RestoreBackupAsync(decrypted);
 Check((await restored.GetGymSessionsAsync()).Single().Sets.Single()==set && (await restored.GetGymPreferencesAsync())==pref && (await restored.GetCustomExercisesAsync()).Single()==custom,"encrypted backup exercises sets active timers preferences");
 Check((await restored.GetGymSessionsAsync()).Single().Clock.RunningSince==session.Clock.RunningSince,"backup running timestamp unchanged");
 var plan=new WorkoutRecord(Guid.NewGuid(),DateOnly.FromDateTime(DateTime.Today),0,"gym","Gym plan",3600,null,null,[],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,Status:"planned");
 await diary.SaveGymSessionAsync(session with {Status="cancelled",Clock=session.Clock.Pause(now.AddHours(2))});
 await diary.SaveWorkoutAsync(plan);var plannedSession=new GymSession(Guid.NewGuid(),plan.Title,now,new(120),[bb],[set],"finished",plan.Id,FinishedAt:now.AddMinutes(2));
 await diary.SaveGymSessionAsync(plannedSession);Check((await diary.GetWorkoutsAsync()).Single(w=>w.Id==plan.Id).IsCompleted,"gym finish replaces plan with same ID");
 await diary.SaveGymSessionAsync(plannedSession with {Status="active",FinishedAt=null});Check((await diary.GetWorkoutsAsync()).Single(w=>w.Id==plan.Id).IsPlanned,"undo gym finish restores planned status");
 // Simulate a pre-gym schema-v4 database, retaining a real workout payload.
 using(var connection=new SqliteConnection("Data Source="+restoredPath+";Pooling=False")) {connection.Open();using var cmd=connection.CreateCommand();cmd.CommandText="DROP TABLE saved_foods; DROP TABLE gym_routines; DROP TABLE gym_sessions; DROP TABLE gym_exercises; DROP TABLE gym_preferences; PRAGMA user_version=4;";cmd.ExecuteNonQuery();}
 restored=new(restoredPath);Check((await restored.GetWorkoutsAsync()).Count==1 && (await restored.GetGymSessionsAsync()).Length==0,"v4 additive migration preserves existing workout");
 var oldBackup=new DiaryBackup(1,now,[],[],[],[],[]);await restored.RestoreBackupAsync(BackupCodec.Decrypt(BackupCodec.Encrypt(oldBackup,"gym-test-passphrase-2026"),"gym-test-passphrase-2026"));
 Check((await restored.GetGymSessionsAsync()).Length==0 && await restored.GetGymPreferencesAsync()==new GymPreferences(),"old v1 encrypted backup restores with empty gym defaults");
 foreach(var version in new[]{2,3,4}) {await restored.RestoreBackupAsync(oldBackup with {FormatVersion=version});Check((await restored.GetGymSessionsAsync()).Length==0,$"legacy backup format {version} remains supported");}
 Console.WriteLine($"{checks} gym checks passed.");
} finally {SqliteConnection.ClearAllPools();foreach(var p in new[]{path,restoredPath})if(File.Exists(p))File.Delete(p);}
