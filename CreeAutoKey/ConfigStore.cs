using System.IO;
using System.Text.Json;
using InputAutomator.Models;

namespace InputAutomator.Services;

/// <summary>
/// Persists AutomationConfig as JSON in %APPDATA%/InputAutomator.
/// </summary>
public static class ConfigStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InputAutomator");

    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static AutomationConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AutomationConfig>(json, JsonOpts) ?? new();
            }
        }
        catch
        {
            // Corrupted config → reset
        }
        return new AutomationConfig();
    }

    public static void Save(AutomationConfig config)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best effort
        }
    }
}
