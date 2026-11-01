using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace TabletDeck;
internal static class LiveScenes
{
    public const string Prefix="live-scene:";
    internal sealed record Scene(string Id,string Name,string? Uuid,int Index);
    public static Scene[] Read(JsonNode? node)=>node is JsonArray array?array.OfType<JsonObject>().Select((scene,index)=>{
        var name=scene["sceneName"]?.GetValue<string>()??"";var raw=scene["sceneUuid"]?.GetValue<string>();var uuid=Guid.TryParse(raw,out var id)?id.ToString("D"):null;
        var token=uuid??"n"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
        return new Scene(Prefix+token,name,uuid,scene["sceneIndex"]?.GetValue<int>()??index);
    }).Where(scene=>scene.Name.Length>0).OrderBy(scene=>scene.Index).DistinctBy(scene=>scene.Id).ToArray():[];
    public static string Revision(JsonNode? node)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Read(node).Select(s=>new{s.Id,s.Name,s.Index}))));
    public static DeckAction[] Buttons(JsonNode? node)=>Read(node).Select(s=>new DeckAction{Id=s.Id,Label=s.Name.Length>60?s.Name[..60]:s.Name,Kind="scene",Target=s.Name,Icon="clapper",Color="violet"}).ToArray();
    public static DeckAction Action(string id){var token=id[Prefix.Length..];if(!(Guid.TryParseExact(token,"D",out _)||token.Length==65&&token[0]=='n'&&token[1..].All(Uri.IsHexDigit)))throw new ArgumentException("Invalid live OBS scene identity.");return new(){Id=id,Label="Switch OBS scene",Kind="liveScene",Target=id};}
}
