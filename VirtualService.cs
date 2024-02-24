using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Rinha2024.Dotnet.IO;
using Rinha2024.VirtualDb;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly int Range = int.TryParse(Environment.GetEnvironmentVariable("CONNECTION_RANGE"), out var connectionRange) ? connectionRange : 3000;
    private static readonly int Port = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 7000;
    
    private readonly ConcurrentQueue<Socket> _ports = new();
    private readonly string _ipAddress;
    
    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _ipAddress = entries[1].ToString();
        for (var i = 0; i < Range; i++)
        {
            var port = Port + i;
            _ports.Enqueue(new Socket(new TcpClient(_ipAddress, port), port));
        }
    }
    
    public async Task<int[]> GetClient(int idx)
    {
        var queueItem = await TryGetClient();
        try
        {
#if !ON_CLUSTER
            Console.WriteLine("stablishing connection with process: {0}", queueItem.Tcp.Client.RemoteEndPoint);
#endif
            var buffer = await PacketBuilder.WriteMessage([0, idx]);
            await queueItem.Tcp.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            var stream = queueItem.Tcp.GetStream();
            return await stream.ReadMessageAsync();
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            await Task.Delay(TimeSpan.FromTicks(10));
            _ports.Enqueue(queueItem);
            return await GetClient(idx);
        }
        finally
        {
            _ports.Enqueue(queueItem);
        }
    }
    
    public async Task<int[]> DoTransaction(int idx, char type, int value)
    {
        var socket = await TryGetClient();
        try
        {
#if !ON_CLUSTER
            Console.WriteLine("stablishing connection with process: {0}", socket.Tcp.Client.RemoteEndPoint);
#endif
            if (type == 'd') value *= -1;
            var buffer = await PacketBuilder.WriteMessage([idx, value]);
            await socket.Tcp.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            var stream = socket.Tcp.GetStream();
            return await stream.ReadMessageAsync();
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            await Task.Delay(TimeSpan.FromTicks(10));
            _ports.Enqueue(socket);
            return await DoTransaction(idx, type, value);
        }
        finally
        {
            _ports.Enqueue(socket);

        }
    }

    private async Task<Socket> TryGetClient()
    {
        Socket item;
        while (!_ports.TryDequeue(out item))
        {
            await Task.Delay(TimeSpan.FromTicks(10));
        }
        if (!item.Tcp.Connected)
        {
            await item.Tcp.ConnectAsync(_ipAddress, item.Port);
        }
        return item;
    }

    private readonly record struct Socket(TcpClient Tcp, int Port);

}