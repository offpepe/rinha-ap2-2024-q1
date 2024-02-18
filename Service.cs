using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Rinha2024.Dotnet;

public sealed class Service(Database db, NpgsqlDataSource dataSource)
{
    public async Task<ExtractDto?> GetExtract(int id)
    {
        var balance = await db.GetBalance(id);
        if (!balance.HasValue) return null;
        var transactions = await db.GetTransactions(id);
        return new ExtractDto(balance.Value, transactions);
    }
    
    // public async Task<int[]?> DoTrasaction(int id, CreateTransactionDto dto)
    // {
        // var found = cache.TryGetValue<int[]>($"c:{id}", out var client);
        // if (!found) return null;
        // var newBalance = dto.Tipo == 'd' ? client![0] - dto.Valor : client![0] + dto.Valor; 
        // var newClientVal = new int[] {newBalance, client[1]};
        // if (dto.Tipo == 'd' && -newBalance > client[1])
        // {
        //     return [0, -1];
        // }
        // cache.Set($"c:{id}", newClientVal);
        // await SendSyncPacket([id, ..newClientVal]);
        // return newClientVal;
    // }


    // private async Task SendSyncPacket(int[] content)
    // {
    //     var twinAddr = Environment.GetEnvironmentVariable("TWIN_ADDRESS");
    //     var hostEntry = await Dns.GetHostEntryAsync(twinAddr!);
    //     var endPoint = new IPEndPoint(hostEntry.AddressList[0], 3000);
    //     var buffer = new byte[content.Length * sizeof(int)];
    //     Buffer.BlockCopy(content, 0, buffer, 0, buffer.Length);
    //     await socket.SendToAsync(buffer, endPoint);
    //     Console.WriteLine("packet sent from {0}", Environment.GetEnvironmentVariable("LABEL"));
    // }
    //
    // public async Task VirtualizeClients()
    // {
    //     await using var conn = dataSource.CreateConnection();
    //     var canConnect = false;
    //     var escapeCounter = 300;
    //     while (!canConnect)
    //     {
    //         try
    //         {
    //             await conn.OpenAsync();
    //             canConnect = true;
    //         }
    //         catch
    //         {
    //             if (escapeCounter == 0)
    //             {
    //                 throw new ApplicationException("Can't connect to Database");
    //             }
    //             await Task.Delay(100);
    //             escapeCounter--;
    //             //ignore;
    //         }
    //     }
    //     await using var cmd = new NpgsqlCommand("SELECT * FROM clientes", conn);
    //     await using var reader = await cmd.ExecuteReaderAsync();
    //     while (await reader.ReadAsync())
    //     {
    //         var id = reader.GetInt32(0);
    //         var balance = reader.GetInt32(1);
    //         var limit = reader.GetInt32(2);
    //         cache.Set<int[]>($"c:{id}", [balance, limit]);
    //     }
    //     await conn.CloseAsync();
    // }

    
    
    
    #region QUERIES
    

    private const string QUERY_TRANSACTIONS = @"
    SELECT
        valor,
        tipo,
        descricao,
        realizada_em::text
    FROM transacoes
    WHERE cliente_id = @id
    ORDER BY id DESC;
";
    
    private const string DO_TRANSACTION = @"CALL CREATE_TRANSACTION(@id, @value, @type, @desc, @newBalance);";

    #endregion
}