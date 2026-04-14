using System.Collections.Generic;
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

    [JsonIgnore]
    public string FileName => $"{Key}.json";
}

public class FireInstruction
{
    [JsonPropertyName("Type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("Duration")]
    public float? Duration { get; set; }

    [JsonPropertyName("Target")]
    public int? Target { get; set; }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            return Type switch
            {
                "H" => "Heat",
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
