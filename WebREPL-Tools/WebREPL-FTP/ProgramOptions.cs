using CommandLine;

namespace WebREPL;

public class ProgramOptions
{
    [Value(0, Required = true, MetaName = "host", HelpText = "Host address (optionally with :port, default is :8266)")]
    public required string Host { get; set; }

    [Option('p', "password", Required = false, HelpText = "WebREPL password (will prompt if not provided)")]
    public string? Password { get; set; }

    [Option('l', "local-dir", Required = false, HelpText = "Initial local working directory")]
    public string? LocalDir { get; set; }

    [Option('r', "remote-dir", Required = false, HelpText = "Initial remote working directory")]
    public string? RemoteDir { get; set; }
}
