using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Rinha2024.Dotnet.IO;
using Rinha2024.VirtualDb;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly int MainPort = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 20000;
    private readonly ConcurrentQueue<int> _socketQueue = new();
    private readonly ConcurrentQueue<TcpClient> _entryPoints = new();
    private readonly ConcurrentDictionary<int, byte> _busyPorts = [];

    private int _lastPort = MainPort;
    const int ENTRY_LIMIT = 1000;
    
    private readonly string _ipAddress;
    
    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _ipAddress = entries[0].ToString();
        _entryPoints.Enqueue(new TcpClient(_ipAddress, MainPort));
    }

    private async Task<int> GetPort(byte type)
    {
        var found = _entryPoints.TryDequeue(out var client);
        if (!found && _entryPoints.Count < ENTRY_LIMIT)
        {
            _lastPort++;
            client = new TcpClient(_ipAddress,_lastPort);
            Console.WriteLine("[{0}] Opened", _lastPort);
        }
        else
        {
            while (!found)
            {
                found =  _entryPoints.TryDequeue(out client);
                await Task.Delay(1);
            }
        }
        try
        {
            var opt = new[] {type};
            var response = new byte[4];
            await client!.Client.SendAsync(opt);
            await client.Client.ReceiveAsync(response);
            var processPort = BitConverter.ToInt32(response);
            return processPort;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            _entryPoints.Enqueue(client!);
        }
    } 
    
    public async Task<(int[], List<TransactionDto>)> GetClient(int idx)
    {
        var port = await GetPort(1);
        try
        {
            using var tcp = new TcpClient(_ipAddress, port);
            var buffer = await PacketBuilder.WriteMessage([0, idx]);
            await tcp.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            await using var stream = tcp.GetStream();
            return await stream.ReadMessageWithTransactionAsync();
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode != SocketError.AddressAlreadyInUse) throw;
            return await GetClient(idx);
        }
    }
    
    public async Task<int[]> DoTransaction(int idx, char type, int value, string description)
    {
        var port = await GetPort(2);
        try
        {
            using var tcp = new TcpClient(_ipAddress, port);
            if (type == 'd') value *= -1;
            var buffer = await PacketBuilder.WriteMessage([idx, value], description);
            await tcp.Client.SendAsync(buffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            await using var stream = tcp.GetStream();
            return await stream.ReadMessageAsync();
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode != SocketError.AddressAlreadyInUse) throw;
            return await DoTransaction(idx, type, value, description);
        }
    }
    
}