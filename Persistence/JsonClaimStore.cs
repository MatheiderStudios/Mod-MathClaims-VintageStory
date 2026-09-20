using System.Text.Json;
using MathClaims.Models;

namespace MathClaims.Persistence;

public sealed class JsonClaimStore(string directory)
{
    private readonly string path = Path.Combine(directory, "claims-v1.json");
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    public ClaimState Load()
    {
        if (!File.Exists(path)) return new ClaimState();
        return JsonSerializer.Deserialize<ClaimState>(File.ReadAllText(path), json) ?? new ClaimState();
    }
    public void Save(ClaimState state)
    {
        Directory.CreateDirectory(directory); var temporary = path + ".tmp"; var backup = path + ".bak";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, json));
        if (File.Exists(path)) File.Replace(temporary, path, backup, true); else File.Move(temporary, path);
    }
}
