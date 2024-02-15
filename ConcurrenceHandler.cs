using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Rinha2024.Dotnet;

public class ConcurrenceHandler(IMemoryCache cache, ConcurrentQueue<int[]> queue) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var hasItem = queue.TryDequeue(out var client);
            if (!hasItem)
            {
                await Task.Delay(1, stoppingToken);
                continue;
            }
            cache.Set<int[]>($"c:{client![0]}", [client[1], client[2]]);
            
        }
    }
}