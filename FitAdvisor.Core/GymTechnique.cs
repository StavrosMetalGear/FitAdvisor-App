using System.Text.Json;
using System.Text.Json.Serialization;
namespace FitAdvisor.Core;
public sealed record TechniqueFrame(string Image,string English,string Greek);
public sealed record TechniqueVisual(string Id,string Source,TechniqueFrame[] Frames);
public sealed record TechniqueGuide(string English,string Greek,string Source,string? Video,string Illustration)
{
    public TechniqueFrame[] Frames {get;init;}=[];
}
[JsonSerializable(typeof(TechniqueVisual[]))]
internal partial class TechniqueJsonContext : JsonSerializerContext { }
public static class GymTechnique
{
    private static readonly Lazy<TechniqueVisual[]> guides=new(()=>{
        using var stream=typeof(GymTechnique).Assembly.GetManifestResourceStream("FitAdvisor.Core.technique-guides.json")!;
        return JsonSerializer.Deserialize(stream,TechniqueJsonContext.Default.TechniqueVisualArray)!;
    });
    public static TechniqueGuide? For(GymExercise e)
    {
        if(e.Custom||!GymCatalogue.Starter.Any(s=>s.Key==e.Key))return null;
        var guide=guides.Value.FirstOrDefault(g=>g.Id==e.Id);
        return guide==null?null:new(string.Join("\n",guide.Frames.Select(f=>f.English)),string.Join("\n",guide.Frames.Select(f=>f.Greek)),guide.Source,null,guide.Frames[0].Image){Frames=guide.Frames};
    }
}
