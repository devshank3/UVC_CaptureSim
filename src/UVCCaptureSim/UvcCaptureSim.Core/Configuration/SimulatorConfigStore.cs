using System.Text.Json;
using System.Text.Json.Serialization;

namespace UvcCaptureSim.Core.Configuration;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SimulatorConfig))]
internal partial class SimulatorConfigJsonContext : JsonSerializerContext;

/// <summary>Loads and saves <see cref="SimulatorConfig"/> with a source-generated JSON context.</summary>
public static class SimulatorConfigStore
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "uvcsim.config.json");

    public static SimulatorConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return new SimulatorConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, SimulatorConfigJsonContext.Default.SimulatorConfig)
                   ?? new SimulatorConfig();
        }
        catch
        {
            return new SimulatorConfig();
        }
    }

    public static void Save(SimulatorConfig config, string? path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(config, SimulatorConfigJsonContext.Default.SimulatorConfig);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, json);
    }
}
