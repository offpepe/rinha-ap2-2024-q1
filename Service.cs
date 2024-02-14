using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using Rinha2024.Dotnet.DTOs;
using Rinha2024.Dotnet.Exceptions;

namespace Rinha2024.Dotnet;

public sealed class Service(NpgsqlConnection conn)
{
    public async Task<ExtractDto?> GetExtract(int id)
    {
        await conn.OpenAsync();
        await using var batch = new NpgsqlBatch(conn);
        var parameter = new NpgsqlParameter<int>("id", id);
        batch.BatchCommands.Add(new NpgsqlBatchCommand(QUERY_EXTRACT)
        {
            Parameters = { parameter }
        });
        batch.BatchCommands.Add(new NpgsqlBatchCommand(QUERY_TRANSACTIONS)
        {
            Parameters = { parameter.Clone() }
        });
        await using var result = await batch.ExecuteReaderAsync();
        if (!result.HasRows) return null;
        await result.ReadAsync();
        var total = result.GetInt32(0);
        var limite = result.GetInt32(1);
        if (!await result.NextResultAsync()) return new ExtractDto()
            {
                Saldo = new SaldoDto
                {
                    total = total,
                    limite = limite,
                    data_extrato = DateTime.Now
                },
                ultimas_transacoes = []
            };
        var transactions = await ReadTransactions(result);
        await conn.CloseAsync();
        return new ExtractDto()
        {
            Saldo = new SaldoDto
            {
                total = total,
                limite = limite,
                data_extrato = DateTime.Now
            },
            ultimas_transacoes = transactions 
        };
    }

    private static async Task<TransactionDto[]> ReadTransactions(NpgsqlDataReader reader)
    {
        if (!reader.HasRows) return [];
        var transactions = new List<TransactionDto>();
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
        return transactions.ToArray();
    }


    public async Task<int[]?> ValidateTransactionAsync(CreateTransactionDto dto)
    {
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(UPDATE_BALANCE, conn);
        cmd.Parameters.Add(new NpgsqlParameter<int>("id", dto.Id));
        cmd.Parameters.Add(new NpgsqlParameter<int>("value", dto.Valor));
        cmd.Parameters.Add(new NpgsqlParameter<char>("type", dto.Tipo));
        cmd.Parameters.Add(new NpgsqlParameter<string>("desc", dto.Descricao));
        await using var result = await cmd.ExecuteReaderAsync();
        if (!result.HasRows) return null;
        await result.ReadAsync();
        var values = result.GetValue(0) as int[] ?? [];
        await conn.CloseAsync();
        if (values.Length == 0 || values[1] == 0) return null;
        return values;
    }

    private async Task UpdateBalance(int id, int newBalance)
    {
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(UPDATE_BALANCE, conn);
        cmd.Parameters.Add(new NpgsqlParameter<int>("id", id));
        cmd.Parameters.Add(new NpgsqlParameter<int>("newBalance", newBalance));
        await cmd.ExecuteNonQueryAsync();
    }
    
    #region QUERIES

    private const string QUERY_EXTRACT = @"
    SELECT
        c.saldo total,
        c.limite
    FROM clientes c
    WHERE c.id = @id;";

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

    private const string QUERY_VERIFY_VALID_TRANSACTION = @"
    SELECT
        c.saldo,
        c.limite
    FROM clientes c
    WHERE c.id = @id;
";

    private const string UPDATE_BALANCE = @"SELECT UPDATE_BALANCE(@id, @value, @type, @desc);";

    #endregion
}