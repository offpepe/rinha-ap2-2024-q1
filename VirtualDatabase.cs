using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;

namespace Rinha2024.Dotnet;

public class VirtualDatabase(Socket socket)
{
    private int[] _unprocessable = [0, -1];
    private readonly IPEndPoint _twin = new(Dns.GetHostEntry(Environment.GetEnvironmentVariable("TWIN_ADDRESS")!).AddressList[0], 3000);
    private readonly int[][] _clients =
    [
        [0, 0],
        [0, 100000],
        [0, 80000],
        [0, 1000000],
        [0, 10000000],
        [0, 500000],
    ];

    public int Size => _clients.Length;

    public ref int[] GetClient(ref int idx) => ref _clients[idx];

    public async Task<int[]> DoTransaction(int idx,char type, int value)
    {
        var client = GetClient(ref idx);
        var isDebit = type == 'd';
        var newBalance = isDebit ? client[0] - value : client[0] + value;
        if (isDebit && -newBalance > client[1]) return _unprocessable;
        client[0] = newBalance;
        await SendPacketAsync([idx, newBalance]);
        return client;
    }
   
    
    private async Task SendPacketAsync(int[] content)
    {
        var buffer = new byte[content.Length * sizeof(int)]; 
        Buffer.BlockCopy(content, 0, buffer, 0, buffer.Length);
        await socket.SendToAsync(buffer, _twin); 
        Console.WriteLine("packet sent");
    }
    
}

