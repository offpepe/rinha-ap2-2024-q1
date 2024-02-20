using System.Net;
using System.Net.Sockets;

namespace Rinha2024.Dotnet;

public class SyncVirtualDatabases(VirtualDatabase vdb, Socket socket) : BackgroundService
{
    private readonly int _timeout =
        int.TryParse(Environment.GetEnvironmentVariable("UDP_SOCKET_RECEIVE_TIMEOUT"), out var udpSocketReceiveTimeout)
            ? udpSocketReceiveTimeout
            : 30;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        socket.Bind(new IPEndPoint(IPAddress.Any, 3000)); 
        while (!stoppingToken.IsCancellationRequested)
        {
            var buffer = new byte[1024];
            try
            {
                var bytes = await socket.ReceiveAsync(buffer);
                if (bytes == 0)
                {
                    continue;
                }
                var data = new int[bytes / sizeof(int)];
                Buffer.BlockCopy(buffer, 0, data, 0, bytes);
                SetClientValue(ref data);
            }
            catch
            {
                // await Task.Delay(_timeout, stoppingToken);
            }

        }
    }

    private void SetClientValue(ref int[] syncPacket)
    {
        ref var client = ref vdb.GetClient(ref syncPacket[0]);
        client[0] = syncPacket[1];
        Console.WriteLine("packet received");
    }
}