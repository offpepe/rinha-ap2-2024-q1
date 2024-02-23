using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly byte[] ZeroInBytes = [0, 0, 0, 0];

    private static readonly int Range =
        int.TryParse(Environment.GetEnvironmentVariable("CONNECTION_RANGE"), out var connectionRange)
            ? connectionRange
            : 2;

    private static readonly int Port = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort)
        ? basePort
        : 10000;

    private readonly VirtualConnectionPool _pool;

    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _pool = new VirtualConnectionPool(entries[1].ToString(), Range, Port);
    }

    public async Task<int[]> GetClient(int idx)
    {
        var result = new int[2];
        await using var poolItem = await _pool.RentAsync();
        var tcpClient = poolItem.Conn;
        Console.WriteLine("stablishing connection with process: {0}", tcpClient.GetHost());
        try
        {
            var client = tcpClient.GetClient();
            var stream = client.GetStream();
            var sendBuffer = BitConverter.GetBytes(idx);
            stream.Write(ZeroInBytes);
            stream.Write(sendBuffer);
            await Task.Delay(TimeSpan.FromTicks(50));
            var responseBuffer = new byte[sizeof(int)];
            _ = await stream.ReadAsync(responseBuffer);
            result[0] = BitConverter.ToInt32(responseBuffer);
            _ = await stream.ReadAsync(responseBuffer);
            result[1] = BitConverter.ToInt32(responseBuffer);
        }
        catch (IOException _)
        {
            tcpClient.Client.Close();
            tcpClient.Reconnect();
            return await GetClient(idx);
        }

        return result;
    }
    
    public async Task<int[]> DoTransaction(int idx, char type, int value)
    {
        try
        {
            var result = new int[2];
            await using var poolItem = await _pool.RentAsync();
            var tcpClient = poolItem.Conn;
            Console.WriteLine("stablishing connection with process: {0}", tcpClient.GetHost());
            var stream = tcpClient.GetStream();
            await stream.WriteAsync(BitConverter.GetBytes(idx));
            await stream.WriteAsync(type == 'd' ? BitConverter.GetBytes(-value) : BitConverter.GetBytes(value));
            await Task.Delay(TimeSpan.FromTicks(50));
            var responseBuffer = new byte[sizeof(int)];
            _ = await stream.ReadAsync(responseBuffer);
            result[0] = BitConverter.ToInt32(responseBuffer);
            _ = await stream.ReadAsync(responseBuffer);
            result[1] = BitConverter.ToInt32(responseBuffer);
            return result;
        }
        catch (IOException _)
        {
            Console.WriteLine("got IOException");
            return await DoTransaction(idx, type, value);
        }
    }
    
}