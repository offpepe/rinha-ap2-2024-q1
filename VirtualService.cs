using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Rinha2024.Dotnet;

public class VirtualService
{
    private static readonly byte[] ZeroInBytes = [0, 0, 0, 0];
    private static readonly int Range = int.TryParse(Environment.GetEnvironmentVariable("CONNECTION_RANGE"), out var connectionRange) ? connectionRange : 1500;
    private static readonly int Port = int.TryParse(Environment.GetEnvironmentVariable("BASE_PORT"), out var basePort) ? basePort : 6000;
    private readonly VirtualConnectionPool _pool;
    public VirtualService()
    {
        var host = Environment.GetEnvironmentVariable("VIRTUAL_DB") ?? "localhost";
        var entries = Dns.GetHostAddresses(host);
        _pool = new VirtualConnectionPool(entries[1].ToString(), Range, Port);

    }
    public async Task<int[]> GetClient(int idx)
    {
        try
        {
            var result = new int[2];
            await using var poolItem = await _pool.RentAsync();
            using var tcpClient = poolItem.Conn;
            var stream = tcpClient.GetStream();
            var sendBuffer = BitConverter.GetBytes(idx);
            stream.Write(ZeroInBytes);
            stream.Write(sendBuffer);
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
            return await GetClient(idx);
        }
    }

    public async Task<int[]> DoTransaction(int idx, char type, int value)
    {
        try
        {
            var result = new int[2];
            await using var poolItem = await _pool.RentAsync();
            using var tcpClient = poolItem.Conn;
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

    private sealed class VirtualConnectionPool : IAsyncDisposable
    {
        private readonly Channel<VirtualDbConnection> _queue = Channel.CreateUnbounded<VirtualDbConnection>(new UnboundedChannelOptions()
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });

        public VirtualConnectionPool(string address, int range, int initialPort)
        {
            for (var i = 0; i < range; i++)
            {
                _queue.Writer.TryWrite(new VirtualDbConnection(address, initialPort + i));
            }
        }

        public async ValueTask<PoolItem> RentAsync()
        {
            VirtualDbConnection? conn = null;
            try
            {
                conn = await _queue.Reader.ReadAsync();
                return new PoolItem(conn.Value, ReturningItemAsync);
            }
            catch (Exception e)
            {
                if (conn.HasValue) await _queue.Writer.WriteAsync(conn.Value);
                throw;
            }
        }

        private async ValueTask<List<VirtualDbConnection>> ReturnAll()
        {
            var conns = new List<VirtualDbConnection>();
            await foreach (var conn in _queue.Reader.ReadAllAsync())
                conns.Add(conn);
            return conns;
        }

        private async ValueTask ReturningItemAsync(PoolItem item)
        {
            await _queue.Writer.WriteAsync(item.Conn);
        }

        public async ValueTask DisposeAsync()
        {
            var connections = await ReturnAll();
            Parallel.ForEach(connections, (conn, _) => conn.Dispose());
        }
        public readonly struct PoolItem(VirtualDbConnection conn, Func<PoolItem, ValueTask> returning)
        {
            public VirtualDbConnection Conn { get; } = conn;
            public ValueTask DisposeAsync() => returning(this);
        }
        public struct VirtualDbConnection(string address, int port) : IDisposable
        {
            private TcpClient _client = new(address, port);
            private bool _available = true;

            public TcpClient GetClient()
            {
                while (!_available)
                {
                    Thread.Sleep(TimeSpan.FromTicks(50));
                }
                _available = false;
                return _client;
            }

            public NetworkStream GetStream()
            {
                while (!_available)
                {
                    Console.WriteLine("Connection unavailable on {0}", port);
                    Thread.Sleep(TimeSpan.FromTicks(50));
                }
                NetworkStream stream;
                try
                {
                    stream = _client.GetStream();
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    Console.WriteLine("Lost connection on {0}", port);
                    Thread.Sleep(TimeSpan.FromTicks(100));
                    RecoverConnection();
                    return GetStream();
                }
                _available = false;
                return stream;
            }
            
            public void RecoverConnection() => _client = new TcpClient(address, port); 
            public void Dispose()
            {
                if (!_client.Connected) RecoverConnection();
                _available = true;
            }
        }

  
    } 


}

