using System;
using System.Text.Json.Serialization;

namespace WebREPL_KilnPresets;

public class HostConfiguration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 8266;

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("localDefaultPath")]
    public string LocalDefaultPath { get; set; } = "";

    [JsonPropertyName("remoteDefaultPath")]
    public string RemoteDefaultPath { get; set; } = "/";

    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; } = DateTime.MinValue;

    public HostConfiguration()
    {
    }

    public HostConfiguration(string name, string host, int port, string password, string localDefaultPath = "", string remoteDefaultPath = "/")
    {
        Name = name;
        Host = host;
        Port = port;
        Password = password;
        LocalDefaultPath = localDefaultPath;
        RemoteDefaultPath = remoteDefaultPath;
        LastUsed = DateTime.Now;
    }

    public override string ToString() => $"{Name} ({Host}:{Port})";
}
