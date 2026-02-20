using System.IO;
using System.Text.Json;

namespace ClaudeUsageWin.Services;

public record Profile
{
    public string Id          { get; init; } = Guid.NewGuid().ToString();
    public string Name        { get; init; } = "Default";
    public string SessionKey  { get; init; } = "";
    public string OrgId       { get; init; } = "";
    // OAuth is always read from ~/.claude/.credentials.json for the active profile
}

public static class ProfileService
{
    private static readonly string ProfilesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "profiles");

    private static readonly string ActiveFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "active-profile.txt");

    public static List<Profile> LoadAll()
    {
        if (!Directory.Exists(ProfilesDir)) return [new Profile()];
        var profiles = Directory.GetFiles(ProfilesDir, "*.json")
            .Select(f =>
            {
                try { return JsonSerializer.Deserialize<Profile>(File.ReadAllText(f)); }
                catch { return null; }
            })
            .Where(p => p is not null)
            .Cast<Profile>()
            .ToList();
        return profiles.Count > 0 ? profiles : [new Profile()];
    }

    public static string GetActiveId()
    {
        try { return File.Exists(ActiveFile) ? File.ReadAllText(ActiveFile).Trim() : ""; }
        catch { return ""; }
    }

    public static Profile? GetActive()
    {
        var id  = GetActiveId();
        var all = LoadAll();
        return all.FirstOrDefault(p => p.Id == id) ?? all.FirstOrDefault();
    }

    public static void Save(Profile profile)
    {
        Directory.CreateDirectory(ProfilesDir);
        File.WriteAllText(
            Path.Combine(ProfilesDir, $"{profile.Id}.json"),
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetActive(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ActiveFile)!);
        File.WriteAllText(ActiveFile, id);
    }

    public static void Delete(string id)
    {
        var path = Path.Combine(ProfilesDir, $"{id}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}
