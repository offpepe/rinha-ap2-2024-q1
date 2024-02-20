using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;

namespace Rinha2024.Dotnet;

public class VirtualDatabase
{
    private readonly string _label = Environment.GetEnvironmentVariable("LABEL");
    private int[] _unprocessable = [0, -1];
    private readonly Socket _socket;
    private readonly IPEndPoint _twin;
    private readonly HttpClient _httpClient;
    private readonly int[][] _clients =
    [
        [0, 0, 0],
        [0, 100000, 0],
        [0, 80000, 0],
        [0, 1000000, 0],
        [0, 10000000, 0],
        [0, 500000, 0],
    ];

    public int Size => _clients.Length;
    public VirtualDatabase(Socket socket)
    {
        _socket = socket;
        var twinAddr = new Uri($"{Environment.GetEnvironmentVariable("TWIN_ADDRESS")!}:3000/balance"); 
        var hostEntry = Dns.GetHostEntry(twinAddr!); 
        _twin = new IPEndPoint(hostEntry.AddressList[0], 3000);
    }
    
    public ref int[] GetClient(ref int idx) => ref _clients[idx];

    public async Task<int[]> DoTransaction(int idx,char type, int value)
    {
        var client = GetClient(ref idx);
        Console.WriteLine("client {0} locked by process {1}", idx, _label);
        var isDebit = type == 'd';
        var newBalance = isDebit ? client[0] - value : client[0] + value;
        if (isDebit && -newBalance > client[1]) return _unprocessable;
        client[0] = newBalance;
        return client;
    }

    private async Task SyncBalance(int id, int balance)
    {

    }
    
    
    private async Task SendPacketAsync(int[] content)
    {
        var buffer = new byte[content.Length * sizeof(int)]; 
        Buffer.BlockCopy(content, 0, buffer, 0, buffer.Length); 
        await _socket.SendToAsync(buffer, _twin); 
    }
    
}

