using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WebREPL_KilnPresets;

public class HostManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".webrepl-commander");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "hosts.json");

    private List<HostConfiguration> _hosts = new();

    public IReadOnlyList<HostConfiguration> Hosts => _hosts.AsReadOnly();

    public HostManager()
    {
        EnsureConfigDirectoryExists();
        Load();
    }

    private void EnsureConfigDirectoryExists()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                _hosts = JsonSerializer.Deserialize<List<HostConfiguration>>(json) ?? new List<HostConfiguration>();
            }
            else
            {
                _hosts = new List<HostConfiguration>();
                
                _hosts.Add(new HostConfiguration("ESP8266 Default", "192.168.4.1", 8266, ""));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load host configurations: {ex.Message}", ex);
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_hosts, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save host configurations: {ex.Message}", ex);
        }
    }

    public void Add(HostConfiguration host)
    {
        if (string.IsNullOrWhiteSpace(host.Name))
            throw new ArgumentException("Host name cannot be empty");

        if (string.IsNullOrWhiteSpace(host.Host))
            throw new ArgumentException("Host address cannot be empty");

        if (_hosts.Any(h => h.Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A host with the name '{host.Name}' already exists");

        _hosts.Add(host);
        Save();
    }

    public void Update(HostConfiguration host, string? originalName = null)
    {
        var nameToFind = originalName ?? host.Name;
        var existing = _hosts.FirstOrDefault(h => h.Name.Equals(nameToFind, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            throw new InvalidOperationException($"Host '{nameToFind}' not found");

        if (!existing.Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (_hosts.Any(h => h.Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A host with the name '{host.Name}' already exists");
        }

        existing.Name = host.Name;
        existing.Host = host.Host;
        existing.Port = host.Port;
        existing.Password = host.Password;
        existing.LocalDefaultPath = host.LocalDefaultPath;
        existing.RemoteDefaultPath = host.RemoteDefaultPath;

        Save();
    }

    public void Delete(string name)
    {
        var host = _hosts.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (host == null)
            throw new InvalidOperationException($"Host '{name}' not found");

        _hosts.Remove(host);
        Save();
    }

    public void UpdateLastUsed(string name)
    {
        var host = _hosts.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (host != null)
        {
            host.LastUsed = DateTime.Now;
            Save();
        }
    }

    public HostConfiguration? GetMostRecentlyUsed()
    {
        return _hosts.OrderByDescending(h => h.LastUsed).FirstOrDefault();
    }
}
