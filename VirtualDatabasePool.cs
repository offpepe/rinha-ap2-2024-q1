using System.Net.Sockets;
using System.Threading.Channels;

namespace Rinha2024.Dotnet;

 public sealed class VirtualConnectionPool : IAsyncDisposable
    {
        private readonly Channel<VirtualDbConnection> _queue = Channel.CreateUnbounded<VirtualDbConnection>(
            new UnboundedChannelOptions()
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
            public TcpClient Client { get; set; } = new(address, port)
            {
                ReceiveTimeout = 3000,
                SendTimeout = 3000
            };
            private bool _available = true;

            public VirtualDbConnection Clone() => new(address, port);

            public string? GetHost() => Client.Client.RemoteEndPoint?.ToString();

            public TcpClient GetClient()
            {
                while (!_available)
                {
                    Thread.Sleep(TimeSpan.FromTicks(50));
                }

                _available = false;
                return Client;
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
                    stream = Client.GetStream();
                    stream.ReadTimeout = 1000;
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    Console.WriteLine("Lost connection on {0}", port);
                    Thread.Sleep(TimeSpan.FromTicks(100));
                    return GetStream();
                }

                _available = false;
                return stream;
            }

            public void Reconnect() => Client = new TcpClient(address, port);

            public void Dispose()
            {
                _available = true;
            }
        }
    }