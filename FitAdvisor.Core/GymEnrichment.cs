namespace FitAdvisor.Core;
public sealed record HeartSample(DateTimeOffset Time, double Bpm);
public sealed record EnergyInterval(string Id, DateTimeOffset Start, DateTimeOffset End, double ActiveKcal);
public sealed record GymEnrichment(Guid ImportId, string SourcePackage, DateTimeOffset From, DateTimeOffset Through,
    HeartSample[] Samples, double? ActiveKcal, double EnergyCoverageSeconds, string Diagnostics)
{
    public void Validate() {
        if(ImportId==Guid.Empty||Through<=From||Samples==null||Samples.Length>100000)throw new ArgumentException("Invalid sensor snapshot.");
        DiaryValidation.Text(SourcePackage,200,"Sensor source");DiaryValidation.Text(Diagnostics,2000,"Sensor diagnostics");
        if(Samples.Any(s=>s.Time<From||s.Time>=Through||!double.IsFinite(s.Bpm)||s.Bpm<1||s.Bpm>300))throw new ArgumentException("Invalid heart-rate sample.");
        if(ActiveKcal is {} kcal)DiaryValidation.Number(kcal,0,100000,"Active kcal");
        DiaryValidation.Number(EnergyCoverageSeconds,0,(Through-From).TotalSeconds,"Energy coverage");
    }
    public static GymEnrichment Create(WorkoutRecord import, DateTimeOffset from,DateTimeOffset through,IEnumerable<HeartSample> samples,IEnumerable<EnergyInterval> intervals,string diagnostics)
    {
        var energy=intervals.DistinctBy(e=>e.Id).Where(e=>e.Start>=from&&e.End<=through&&e.End>e.Start).OrderBy(e=>e.Start).ToArray();
        // Overlapping same-origin records can represent competing estimates. Never add them together.
        var overlap=energy.Zip(energy.Skip(1)).Any(pair=>pair.First.End>pair.Second.Start);
        var result=new GymEnrichment(import.Id,import.SourcePackage!,from,through,samples.Where(s=>s.Time>=from&&s.Time<through).Distinct().OrderBy(s=>s.Time).ToArray(),energy.Length==0||overlap?null:energy.Sum(e=>e.ActiveKcal),overlap?0:energy.Sum(e=>(e.End-e.Start).TotalSeconds),diagnostics+(overlap?" Overlapping energy records: estimate unavailable to avoid double counting.":" Only fully contained energy intervals are counted; partial boundary records are excluded, not prorated."));
        result.Validate();return result;
    }
    public string Summary => $"{SourcePackage} · {From.ToLocalTime():g} – {Through.ToLocalTime():g}\n"+
        (Samples.Length==0?"Heart rate unavailable":$"Heart rate: {Samples.Length} samples · min {Samples.Min(s=>s.Bpm):0} / mean {Samples.Average(s=>s.Bpm):0} / max {Samples.Max(s=>s.Bpm):0} bpm (sample mean, not time weighted); first {Samples[0].Time.ToLocalTime():T}, last {Samples[^1].Time.ToLocalTime():T}; gaps may exist")+
        $"\nActive energy (source estimate): {(ActiveKcal is {} kcal?$"{kcal:0.##} kcal":"unavailable")} · energy interval coverage {EnergyCoverageSeconds/60:0.#} of {(Through-From).TotalMinutes:0.#} min. Total energy not requested. Food allowance unchanged.\n{Diagnostics}";
}
