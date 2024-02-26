using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Rinha2024.Dotnet.IO;
using Rinha2024.VirtualDb;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly int MainPort = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 30000;


    private readonly ConcurrentQueue<int> _socketQueue = new();
    private readonly ConcurrentQueue<TcpClient> _entryPoints = new();

    
    private readonly string _ipAddress;
    
    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _ipAddress = entries[0].ToString();
        for (var i = 0; i < 5; i++)
        {
            _entryPoints.Enqueue(new TcpClient(_ipAddress,MainPort +  i));
        }
    }

    private async Task<int> GetPort(byte type)
    {
        var found = false;
        TcpClient? client = null;
        var times = 0;
        while (!found)
        {
            if(times == 100000) Environment.FailFast("Everyone is dead!!!");
            found = _entryPoints.TryDequeue(out client);
            await Task.Delay(TimeSpan.FromTicks(10));
            times++;
        }
        var opt = new []{type};
        var response = new byte[4];
        try
        {
            await client!.Client.SendAsync(opt);
            await client!.Client.ReceiveAsync(response);
        }
        catch (SocketException _)
        {
            if (client == null) return await GetPort(type);
            var port = ((IPEndPoint) client.Client.LocalEndPoint!).Port;
            client?.Dispose();
            _entryPoints.Enqueue(new TcpClient(_ipAddress, port));
            return await GetPort(type);
        }
        _entryPoints.Enqueue(client);
        return BitConverter.ToInt32(response);
    } 
    
    public async Task<(int[], List<TransactionDto>)> GetClient(int idx)
    {

        var port = await GetPort(1);
        using var tcp = new TcpClient(_ipAddress, port);
        var buffer = await PacketBuilder.WriteMessage([0, idx]);
        await tcp.Client.SendAsync(buffer);
        await Task.Delay(TimeSpan.FromTicks(50));
        await using var stream = tcp.GetStream();
        return await stream.ReadMessageWithTransactionAsync();
    }
    
    public async Task<int[]> DoTransaction(int idx, char type, int value, string description)
    {
        var port = await GetPort(2);
        using var tcp = new TcpClient(_ipAddress, port);
        if (type == 'd') value *= -1;
        var buffer = await PacketBuilder.WriteMessage([idx, value], description);
        await tcp.Client.SendAsync(buffer);
        await Task.Delay(TimeSpan.FromTicks(50));
        await using var stream = tcp.GetStream();
        return await stream.ReadMessageAsync();
    }

    private async Task<int> TryGetClient()
    {
        int port;
        while (!_socketQueue.TryDequeue(out port))
        {
            await Task.Delay(TimeSpan.FromTicks(10));
        }
        return port;
    }

    private class SocketDto(int port)
    {

        public int Port { get; } = port;


    };

}