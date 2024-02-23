using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Rinha2024.Dotnet.IO;
using Rinha2024.VirtualDb;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly byte[] ZeroInBytes = [0, 0, 0, 0];
    private static readonly int Range = int.TryParse(Environment.GetEnvironmentVariable("CONNECTION_RANGE"), out var connectionRange) ? connectionRange : 1000;
    private static readonly int Port = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 7000;
    
    private readonly ConcurrentQueue<int> _ports = new();
    private string _ipAddress;
    
    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _ipAddress = entries[1].ToString();
        for (var i = 0; i < Range; i++)
        {
            _ports.Enqueue(Port + i);
        }
    }
    
    public async Task<int[]> GetClient(int idx)
    {
        var port = await TryGetPort();
        try
        {
            using var client = new TcpClient(_ipAddress, port);
#if !ON_CLUSTER
            Console.WriteLine("stablishing connection with process: {0}", client.Client.RemoteEndPoint);
#endif
            var buffer = await PacketBuilder.WriteMessage([0, idx]);
            await client.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            await using var stream = client.GetStream();
            return await stream.ReadMessageAsync();
        }
        catch (IOException _)
        {
            await Task.Delay(TimeSpan.FromTicks(10));
            return await GetClient(idx);
        }
        finally
        {
            _ports.Enqueue(port);
        }
    }
    
    public async Task<int[]> DoTransaction(int idx, char type, int value)
    {
        var port = await TryGetPort();
        try
        {
            using var client = new TcpClient(_ipAddress, port);
#if !ON_CLUSTER
            Console.WriteLine("stablishing connection with process: {0}", client.Client.RemoteEndPoint);
#endif
            if (type == 'd') value *= -1;
            var buffer = await PacketBuilder.WriteMessage([idx, value]);
            await client.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            await using var stream = client.GetStream();
            return await stream.ReadMessageAsync();
        }
        catch (IOException _)
        {
            await Task.Delay(TimeSpan.FromTicks(10));
            return await DoTransaction(idx, type, value);
        }
        finally
        {
            _ports.Enqueue(port);

        }
    }

    private async Task<int> TryGetPort()
    {
        int port = 0;
        while (!_ports.TryDequeue(out port))
        {
            await Task.Delay(TimeSpan.FromTicks(10));
        }
        return port;
    }

}