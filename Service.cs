using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualBasic;
using Npgsql;

namespace Rinha2024.Dotnet;

public sealed class Service(NpgsqlConnection conn, IMemoryCache cache)
{
    public async Task<ExtractDto?> GetExtract(int id)
    {
        var found = cache.TryGetValue<int[]>($"c:{id}", out var client);
        if (!found) return null;
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(QUERY_TRANSACTIONS, conn);
        cmd.Parameters.Add(new NpgsqlParameter<int>("id", id));
        await using var reader = await cmd.ExecuteReaderAsync();
        var balanceDto = new SaldoDto(client![0], client[1]);
        if (!reader.HasRows) return new ExtractDto(balanceDto, []);
        var transactions = await ReadTransactions(reader);
        await conn.CloseAsync();
        return new ExtractDto(balanceDto, transactions);
    }

    private static async Task<IEnumerable<TransactionDto>> ReadTransactions(NpgsqlDataReader reader)
    {
        if (!reader.HasRows) return [];
        var transactions = new Collection<TransactionDto>();
        while (await reader.ReadAsync())
        {
            transactions.Add(new TransactionDto()
            {
                valor = reader.GetInt32(0),
                tipo = reader.GetChar(1),
                descricao = reader.GetString(2),
                realizada_em = reader.GetString(3)
            });
        }
        return transactions;
    }


    public async Task<int[]?> ValidateTransactionAsync(int id, CreateTransactionDto dto)
    {
        var found = cache.TryGetValue<int[]>($"c:{id}", out var client);
        if (!found) return null;
        var newBalance = dto.Tipo == 'd' ? client![0] - dto.Valor : client![0] + dto.Valor; 
        if (dto.Tipo == 'd' && -newBalance > client[1])
        {
            return [0, -1];
        }
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(DO_TRANSACTION, conn);
        cmd.Parameters.Add(new NpgsqlParameter<int>("id", id));
        cmd.Parameters.Add(new NpgsqlParameter<int>("value", dto.Valor));
        cmd.Parameters.Add(new NpgsqlParameter<char>("type", dto.Tipo));
        cmd.Parameters.Add(new NpgsqlParameter<string>("desc", dto.Descricao));
        cmd.Parameters.Add(new NpgsqlParameter<int>("newBalance", newBalance));
        await cmd.ExecuteNonQueryAsync();
        await conn.CloseAsync();
        var newClientVal = new int[] {newBalance, client[1]};
        cache.Set($"c:{id}", newClientVal);
        return newClientVal;
    }

    public async Task VirtualizeClients()
    {
        var canConnect = false;
        var escapeCounter = 300;
        while (!canConnect)
        {
            try
            {
                await conn.OpenAsync();
                canConnect = true;
            }
            catch
            {
                if (escapeCounter == 0)
                {
                    throw new ApplicationException("Can't connect to Database");
                }
                await Task.Delay(100);
                escapeCounter--;
                //ignore;
            }
        }
        await using var cmd = new NpgsqlCommand("SELECT * FROM clientes", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var balance = reader.GetInt32(1);
            var limit = reader.GetInt32(2);
            cache.Set<int[]>($"c:{id}", [balance, limit]);
        }

        await conn.CloseAsync();
    }

    
    
    
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