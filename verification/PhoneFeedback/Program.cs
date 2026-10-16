using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.Services;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.Net;

var checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
async Task Reject(Func<Task> action,string label){try{await action();throw new Exception("Accepted "+label);}catch(ArgumentException){checks++;Console.WriteLine("PASS rejects "+label);}}
FoodNutritionSnapshot Parse(string json){using var d=JsonDocument.Parse(json);return OpenFoodFactsClient.Parse(d.RootElement);}
using(var staging=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/off-staging-water.json")))) {
 var water=OpenFoodFactsClient.Parse(staging.RootElement.GetProperty("product"));
 Check(water.ProductCode=="3274080005003"&&water.Unit=="ml"&&!water.Usable,"real v3.6 staging product: ml preserved, absent energy blocked");
}
var yogurt=Parse("""{"code":"5201234567890","brands":"ΦΑΓΕ","product_name":"Total 2%","product_quantity_unit":"g","quantity":"170g","nutriments":{"energy-kcal_100g":73,"proteins_100g":9.9}}""");
Check(yogurt.Kind=="off"&&yogurt.ProductCode=="5201234567890"&&yogurt.Description.Contains("ΦΑΓΕ")&&yogurt.Usable,"exact brand variant and barcode retained");
Check((yogurt with {Grams=150}).Calories==109.5&&(yogurt with {Grams=50}).Protein==4.95,"immediate portion scaling from frozen basis");
Check(yogurt.Carbs==null&&yogurt.Fat==null,"missing nutrients remain null");
var liquid=Parse("""{"code":"1234567890123","product_name":"Milk","quantity":"1l","nutriments":{"energy-kj_100g":209.2}}""");
Check(liquid.Unit=="ml"&&Math.Abs(liquid.CaloriesPer100G-50)<.0001&&liquid.Meal(new(2026,10,16)).Unit=="ml","kJ converted to kcal with per-100-ml basis; no density assumption");
var serving=Parse("""{"code":"1234567890123","product_name":"Snack","serving_size":"1 pot (150 g)","nutriments":{"energy-kcal_serving":120,"proteins_serving":15}}""");
Check(serving.Unit=="g"&&serving.CaloriesPer100G==80&&serving.ProteinPer100G==10,"explicit gram serving converted to per 100 g");
var liquidServing=Parse("""{"code":"1234567890123","product_name":"Drink","serving_size":"250 ml","nutriments":{"energy-kj_serving":418.4}}""");
Check(liquidServing.Unit=="ml"&&Math.Abs(liquidServing.CaloriesPer100G-40)<.0001,"explicit ml serving converted without grams");
var unknown=Parse("""{"code":"1234567890123","product_name":"Unknown basis","nutriments":{"energy-kcal_100g":80}}""");
Check(!unknown.Usable,"ambiguous gram/ml basis blocked");
var noEnergy=Parse("""{"code":"1234567890123","product_name":"Missing energy","quantity":"100g","nutriments":{"proteins_100g":2}}""");
Check(!noEnergy.Usable,"missing energy blocked instead of logging zero");
await Reject(()=>{noEnergy.Meal(new(2026,10,16)).Validate();return Task.CompletedTask;},"missing energy meal");
var conflict=Parse("""{"code":"1234567890123","product_name":"Conflict","quantity":"100g","nutriments":{"energy-kcal_100g":80,"energy-kj_100g":2000}}""");
Check(!conflict.Usable,"conflicting kcal/kJ blocked");
var warning=Parse("""{"code":"1234567890123","product_name":"Warning","quantity":"100g","data_quality_warnings_tags":["warning"],"nutriments":{"energy-kcal_100g":80}}""");
Check(!warning.Usable,"source quality warning requires label correction");
Check(FoodCatalogue.Normalize("ΦΑΓΕ γιαούρτι")==FoodCatalogue.Normalize("φαγε γιαουρτι"),"Greek brand accent/case normalization");
Check(new SavedFood("off:1",yogurt).Matches("φαγε total"),"combined local brand/product matching");
Check(SearchSuggestions.Near("Dumbbell curl / Κάμψεις αλτήρων","dumbbel"),"exercise typo suggestions");
var bb=GymCatalogue.Starter.Single(e=>e.Id=="bench");
Check(GymCatalogue.Matches(bb,"chest barbell")&&!GymCatalogue.Matches(bb,"chest cable"),"combined muscle/equipment tokens");
Check(GymCatalogue.Starter.Count(e=>GymTechnique.For(e)?.Frames.Length==3)==18,"18 exact starter visual sequences available offline");
Check(GymTechnique.For(bb with {Equipment="Smith machine"})==null&&GymTechnique.For(bb with {Custom=true})==null,"no visual substitution for different equipment");
var now=new DateTimeOffset(2026,10,16,8,0,0,TimeSpan.Zero);
var set=new GymSet(Guid.NewGuid(),bb.Id,8,40,null,null,false);
var import=new WorkoutRecord(Guid.NewGuid(),new(2026,10,16),0,"gym","Mi band",1200,null,null,[],now,now,"health-connect","com.xiaomi.wearable","band-1",now,56);
var finished=new GymSession(Guid.NewGuid(),"Training",now,new(1200),[bb],[set],"finished",ImportedWorkoutId:import.Id,FinishedAt:now.AddMinutes(20),Rest:new(true,90,new(10,now))) {Effort=7};
var routine=GymRoutine.From(finished,"My routine");routine.Validate();var repeated=routine.Start(now.AddDays(1));repeated.Validate();
Check(repeated.Id!=finished.Id&&repeated.Sets[0].Id!=set.Id&&!repeated.Sets[0].Completed,"routine copy has new identities and pending sets");
Check(repeated.Sets[0].Reps==8&&repeated.Sets[0].LoadKg==40&&repeated.Exercises[0]==bb,"copy preserves order/conventions/suggestions");
Check(repeated.Clock.Accumulated==0&&repeated.Clock.RunningSince==now.AddDays(1)&&repeated.Rest==null&&repeated.ImportedWorkoutId==null&&repeated.PlanId==null&&repeated.FinishedAt==null&&repeated.Effort==null&&repeated.Enrichment==null,"all lifecycle sensor and subjective states reset");
var samples=new[]{new HeartSample(now.AddMinutes(-1),88),new HeartSample(now.AddMinutes(2),100),new HeartSample(now.AddMinutes(18),140),new HeartSample(now.AddMinutes(21),90)};
var intervals=new[]{new EnergyInterval("a",now.AddMinutes(1),now.AddMinutes(10),70),new EnergyInterval("a",now.AddMinutes(1),now.AddMinutes(10),70),new EnergyInterval("partial",now.AddMinutes(-5),now.AddMinutes(1),30)};
var sensor=GymEnrichment.Create(import,now,now.AddMinutes(20),samples,intervals,"Test source snapshot");
Check(sensor.Samples.Length==2&&sensor.ActiveKcal==70&&sensor.EnergyCoverageSeconds==540,"sensor boundaries, duplicate IDs and partial energy intervals");
Check(GymEnrichment.Create(import,now,now.AddMinutes(20),[],[],"missing").ActiveKcal==null,"missing energy unavailable, not zero");
Check(GymEnrichment.Create(import,now,now.AddMinutes(20),[],[new("zero",now,now.AddMinutes(20),0)],"reported zero").ActiveKcal==0,"explicit source zero distinguishable from missing");
Check(GymEnrichment.Create(import,now,now.AddMinutes(20),[],[intervals[0],new("b",now.AddMinutes(5),now.AddMinutes(15),100)],"competing intervals").ActiveKcal==null,"overlapping energy intervals never summed");
var path=Path.Combine(Path.GetTempPath(),"feedback-"+Guid.NewGuid()+".db");var restoredPath=path+".restore";
try {
 var diary=new SqlitePersonalDiary(path);var day=new DateOnly(2026,10,16);
 var oldMeal=FoodCatalogue.Search("oats").First(f=>f.CaloriesPer100G!=null).Portion(75).Meal(day);await diary.SaveMealAsync(oldMeal);
 var oldMeals=await diary.GetMealsAsync(day);
 await diary.ImportWorkoutsAsync([import]);await diary.SaveGymSessionAsync(finished with {Rest=null});
 var legacy=await diary.CreateBackupAsync();legacy=legacy with {FormatVersion=5};
 var bytes=BackupCodec.Encrypt(legacy,"feedback-backup-passphrase");
 using(var db=new SqliteConnection("Data Source="+path+";Pooling=False")){db.Open();using var cmd=db.CreateCommand();cmd.CommandText="DROP TABLE saved_foods; DROP TABLE gym_routines; PRAGMA user_version=5;";cmd.ExecuteNonQuery();}
 diary=new(path);Check((await diary.GetSavedFoodsAsync()).Length==0,"real v5 additive migration creates products without changing meals");
 Check((await diary.GetMealsAsync(day)).Single()==oldMeals.Single(),"historical meal totals, identity, profile and frozen snapshot unchanged");
 Check((await diary.GetGymSessionsAsync()).Single().Sets.SequenceEqual(finished.Sets),"historical workout sets unchanged on upgrade");
 await diary.SaveFoodAsync(new("off:"+yogurt.ProductCode,yogurt,true,now));await diary.SaveGymRoutineAsync(routine);
 await diary.SaveGymSessionAsync(finished with {Rest=null,Enrichment=sensor});
 Check((await diary.GetWorkoutTotalsAsync()).Count==1,"linked sensor enrichment leaves one workout total");
 await Reject(()=>diary.SaveGymSessionAsync(finished with {Rest=null,Enrichment=sensor with {SourcePackage="com.google.pixel"}}),"unrelated Pixel source");
 await Reject(()=>diary.ImportWorkoutsAsync([import with {UpdatedAt=now.AddDays(1),DurationSeconds=600}]),"source edit invalidating sensor coverage");
 var backup=await diary.CreateBackupAsync();Check(backup.FormatVersion==6&&backup.SavedFoods.Length==1&&backup.GymRoutines.Length==1,"new backup includes reusable data");
 var restored=new SqlitePersonalDiary(restoredPath);await restored.RestoreBackupAsync(BackupCodec.Decrypt(BackupCodec.Encrypt(backup,"feedback-backup-passphrase"),"feedback-backup-passphrase"));
 Check((await restored.GetSavedFoodsAsync()).Single().Nutrition==yogurt&&(await restored.GetSavedFoodsAsync()).Single().Favorite,"encrypted saved product/favorite roundtrip");
 Check((await restored.GetGymRoutinesAsync()).Single().Sets.SequenceEqual(routine.Sets),"encrypted routine suggestions roundtrip");
 var recovered=(await restored.GetGymSessionsAsync()).Single();Check(recovered.Enrichment!.Samples.SequenceEqual(sensor.Samples)&&recovered.Enrichment.ActiveKcal==70&&recovered.Effort==7,"encrypted physiological samples and separate effort roundtrip");
 Check((await restored.GetMealsAsync(day)).Single()==oldMeals.Single(),"historical meal unchanged after new backup restore");
 await restored.RestoreBackupAsync(BackupCodec.Decrypt(bytes,"feedback-backup-passphrase"));
 Check((await restored.GetSavedFoodsAsync()).Length==0&&(await restored.GetGymRoutinesAsync()).Length==0&&(await restored.GetGymSessionsAsync()).Single().Enrichment==null,"old v5 encrypted backup remains readable with defaults");
 Check((await restored.GetMealsAsync(day)).Single()==oldMeals.Single(),"legacy backup meal snapshot unchanged");
 var custom=yogurt with {Kind="custom",Source="User-created package label",LibraryId="custom-label",CaloriesPer100G=90};await diary.SaveFoodAsync(new("custom-label",custom));
 await diary.SaveMealAsync((custom with {Grams=25}).Meal(day));await diary.SaveFoodAsync(new("custom-label",custom with {CaloriesPer100G=100}));
 Check((await diary.GetMealsAsync(day)).Single(m=>m.NutritionSnapshot?.Kind=="custom").CaloriesKcal==22.5,"editing reusable label never changes historical meal");
 var mlMeal=(liquid with {Grams=250}).Meal(day);await diary.SaveMealAsync(mlMeal);Check((await diary.GetMealsAsync(day)).Single(m=>m.Id==mlMeal.Id).Unit=="ml","liquid basis survives SQLite roundtrip");
 await Reject(()=>diary.SaveGymSessionAsync(repeated with {Effort=11}),"effort above 10");
 await diary.SaveGymSessionAsync(repeated);var reopened=new SqlitePersonalDiary(path);Check((await reopened.GetGymSessionsAsync()).Single(s=>s.Status=="active").Sets.All(s=>!s.Completed),"repeated session recovers pending suggestions after process reopen");
 await Reject(()=>diary.SaveGymSessionAsync(routine.Start(now.AddDays(2))),"duplicate active start");
 var reordered=routine with {Title="Edited independently",Exercises=[] ,Sets=[]};await diary.SaveGymRoutineAsync(reordered);
 Check((await diary.GetGymSessionsAsync()).Single(s=>s.Id==finished.Id).Exercises.Single()==bb,"routine editing preserves historical session");
 await Reject(()=>diary.RestoreBackupAsync(backup with {SavedFoods=[backup.SavedFoods[0],backup.SavedFoods[0]]}),"duplicate saved product backup");
 await Reject(()=>Task.Run(()=>BackupCodec.Decrypt(bytes,"incorrect-passphrase")),"old encrypted wrong passphrase");
 var corrupt=BackupCodec.Encrypt(backup,"feedback-backup-passphrase");corrupt[^1]^=1;await Reject(()=>Task.Run(()=>BackupCodec.Decrypt(corrupt,"feedback-backup-passphrase")),"new encrypted tamper");
}finally{SqliteConnection.ClearAllPools();foreach(var file in new[]{path,restoredPath})if(File.Exists(file))File.Delete(file);}
var handler=new RateHandler();var client=new OpenFoodFactsClient(new HttpClient(handler));await Reject(()=>client.LookupAsync("ΦΑΓΕ 2%"),"online rate-limit response");await Reject(()=>client.LookupAsync("ΦΑΓΕ 2%"),"rate-limit cooldown");Check(handler.Count==1,"cooldown avoids a second network request");
Console.WriteLine($"{checks} phone-feedback checks passed.");
sealed class RateHandler : HttpMessageHandler {public int Count; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Count++;if(request.Method!=HttpMethod.Get||request.Content!=null||!request.Headers.UserAgent.ToString().Contains("FitAdvisor"))throw new Exception("Request privacy/identity");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));}}
