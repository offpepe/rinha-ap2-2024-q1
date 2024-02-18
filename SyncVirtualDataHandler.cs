using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;

namespace Rinha2024.Dotnet;

public class SyncVirtualDataHandler(IMemoryCache cache, Socket socket) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        socket.ReceiveTimeout = 30;
        socket.Bind(new IPEndPoint(IPAddress.Any, 3000)); 
        while (!stoppingToken.IsCancellationRequested)
        {
            var buffer = new byte[1024];
            try
            {
                var bytes = socket.Receive(buffer);
                if (bytes == 0)
                {
                    await Task.Delay(20, stoppingToken);
                    continue;
                }

                var data = new int[bytes / sizeof(int)];
                Buffer.BlockCopy(buffer, 0, data, 0, bytes);
                Console.WriteLine("packet received on {0}, data={1}", Environment.GetEnvironmentVariable("LABEL"),
                    string.Join(',', data));
                cache.Set<int[]>($"c:{data[0]}", [data[1], data[2]]);
            }
            catch
            {
                await Task.Delay(20, stoppingToken);
            }

        }
    }
}