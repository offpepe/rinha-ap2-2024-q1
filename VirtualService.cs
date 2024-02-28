using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Rinha2024.Dotnet.IO;
using Rinha2024.VirtualDb;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly int MainPort =
        int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 40000;
    private static readonly int TotalEntryPoints = int.TryParse(Environment.GetEnvironmentVariable("TOTAL_ENTRY_POINTS"), out var basePort) ? basePort : 10;
    private readonly ConcurrentQueue<int> _ports = new();

    private readonly string _ipAddress;

    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _ipAddress = entries[0].ToString();
        for (var i = 0; i < TotalEntryPoints; i++) _ports.Enqueue(MainPort + i);
    }

    private int GetPort()
    {
        _ports.TryDequeue(out var port);
        _ports.Enqueue(port);
        return port;
    }

    public async Task<(int[], List<TransactionDto>)> GetClient(int idx)
    {
        using var tcp = new TcpClient(_ipAddress, GetPort());
        var buffer = await PacketBuilder.WriteMessage([0, idx]);
        await tcp.Client.SendAsync(buffer);
        using var stream = tcp.GetStream();
        return await stream.ReadMessageWithTransactionAsync();
    }

    public async Task<int[]> DoTransaction(int idx, char type, int value, string description)
    {
        using var tcp = new TcpClient(_ipAddress, GetPort());
        if (type == 'd') value *= -1;
        var buffer = await PacketBuilder.WriteMessage([idx, value], description);
        await tcp.Client.SendAsync(buffer);
        await Task.Delay(TimeSpan.FromTicks(50));
        using var stream = tcp.GetStream();
        return await stream.ReadMessageAsync();
    }
}