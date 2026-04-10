using WebREPL.Core;
using CommandLine;

return await Parser.Default.ParseArguments<WebREPL.ProgramOptions>(args)
    .MapResult(
        async options => await RunFtpClientAsync(options),
        _ => Task.FromResult(1));

static async Task<int> RunFtpClientAsync(WebREPL.ProgramOptions options)
{
    string host;
    int port = 8266;

    if (options.Host.Contains(':'))
    {
        var parts = options.Host.Split(':');
        host = parts[0];
        port = int.Parse(parts[1]);
    }
    else
    {
        host = options.Host;
    }

    if (options.LocalDir != null)
    {
        try
        {
            Directory.SetCurrentDirectory(options.LocalDir);
            Console.WriteLine($"Changed local directory to: {Directory.GetCurrentDirectory()}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: Could not change to local directory '{options.LocalDir}': {e.Message}");
            return 1;
        }
    }

    var password = options.Password;
    if (string.IsNullOrEmpty(password))
    {
        Console.Write("Password: ");
        password = WebREPL.FtpCommandLoop.ReadPassword();
        Console.WriteLine();
    }

    Console.WriteLine($"Connecting to {host}:{port} ...");
    using var client = new WebReplClient();

    try
    {
        await client.ConnectAsync(host, port, password);
        Console.WriteLine($"Remote WebREPL version: {client.RemoteVersion}");

        await client.InterruptAsync();

        if (options.RemoteDir != null)
        {
            try
            {
                await RemoteCommands.RemoteCdAsync(client.GetWebSocket(), options.RemoteDir);
                var actualDir = await RemoteCommands.RemotePwdAsync(client.GetWebSocket());
                Console.WriteLine($"Changed remote directory to: {actualDir}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: Could not change to remote directory '{options.RemoteDir}': {e.Message}");
            }
        }

        var commandLoop = new WebREPL.FtpCommandLoop(client);
        await commandLoop.RunAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error: {e.Message}");
        return 1;
    }

    return 0;
}
