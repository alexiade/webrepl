using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebREPL_KilnPresets;

public class FirePreset
{
    [JsonPropertyName("Key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("Category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Phases")]
    public List<FireInstruction> Phases { get; set; } = new();

    // Fields this version doesn't know about are kept and written back unchanged.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string FileName => $"{Key}.json";
}

public class FireInstruction
{
    // Orton cones the controller knows (cones.py on the device), coolest first.
    public static readonly string[] ConeNames =
    {
        "10", "09", "08", "07", "06", "05.5", "05", "04", "03", "02", "01",
        "1", "2", "3", "4", "5", "5.5", "6", "7", "8", "9"
    };

    [JsonPropertyName("Type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("Duration")]
    public float? Duration { get; set; }

    [JsonPropertyName("Target")]
    public int? Target { get; set; }

    // Preset version 3: a Ramp can name an Orton cone ("6"). The controller then also ends the
    // ramp once that cone has matured (heat-work), never later or hotter than Target.
    [JsonPropertyName("Cone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cone { get; set; }

    // Fields this version doesn't know about are kept and written back unchanged.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public FireInstruction Clone() => new()
    {
        Type = Type,
        Duration = Duration,
        Target = Target,
        Cone = Cone,
        Extra = Extra?.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())
    };

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            return Type switch
            {
                "H" => "Heat",
                "P" => "Preheat",
                "R" => "Ramp Up",
                "D" => "Drop",
                "S" => "Soak",
                "C" => "Cool (Down Ramp)",
                _ => Type
            };
        }
    }

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var parts = new List<string> { DisplayName };
            if (Target.HasValue)
                parts.Add($"{Target}°C");
            if (Duration.HasValue)
                parts.Add($"({FormatDuration(Duration.Value)})");
            if (!string.IsNullOrEmpty(Cone))
                parts.Add($"cone {Cone}");
            return string.Join(" ", parts);
        }
    }

    private static string FormatDuration(float seconds)
    {
        var totalSeconds = (int)seconds;
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}h {minutes}m {secs}s";
        if (minutes > 0)
            return $"{minutes}m {secs}s";
        return $"{secs}s";
    }
}
