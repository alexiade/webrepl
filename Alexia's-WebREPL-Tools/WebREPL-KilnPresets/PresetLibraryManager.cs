using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WebREPL_KilnPresets;

public class PresetLibraryManager
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FirePresetLibrary");

    public PresetLibraryManager()
    {
        EnsureLibraryExists();
    }

    private void EnsureLibraryExists()
    {
        if (!Directory.Exists(LibraryPath))
        {
            Directory.CreateDirectory(LibraryPath);
        }
    }

    public List<FirePreset> LoadAllPresets()
    {
        var presets = new List<FirePreset>();
        
        foreach (var categoryDir in Directory.GetDirectories(LibraryPath))
        {
            var category = Path.GetFileName(categoryDir);
            foreach (var file in Directory.GetFiles(categoryDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize<FirePreset>(json);
                    if (preset != null)
                    {
                        preset.Category = category;
                        presets.Add(preset);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading preset {file}: {ex.Message}");
                }
            }
        }

        return presets;
    }

    public void SavePreset(FirePreset preset)
    {
        var categoryPath = Path.Combine(LibraryPath, preset.Category);
        if (!Directory.Exists(categoryPath))
        {
            Directory.CreateDirectory(categoryPath);
        }

        var filePath = Path.Combine(categoryPath, preset.FileName);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(preset, options);
        File.WriteAllText(filePath, json);
    }

    public void DeletePreset(FirePreset preset)
    {
        var categoryPath = Path.Combine(LibraryPath, preset.Category);
        var filePath = Path.Combine(categoryPath, preset.FileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public void MovePreset(FirePreset preset, string oldCategory)
    {
        var oldPath = Path.Combine(LibraryPath, oldCategory, preset.FileName);
        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }
        SavePreset(preset);
    }

    public string GetLibraryPath() => LibraryPath;

    public List<string> GetCategories()
    {
        return Directory.GetDirectories(LibraryPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
    }
}
