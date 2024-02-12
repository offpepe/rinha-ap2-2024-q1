using System.Data;
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
        var cmd = new NpgsqlCommand(QUERY_EXTRACT, conn)
        {
            Parameters =
            {
                new NpgsqlParameter<int>("id", id)
            }
        };
        await using var queryResult = await cmd.ExecuteReaderAsync();
        if (!queryResult.HasRows) return null;
        await queryResult.ReadAsync();
        var total = queryResult.GetInt32(0);
        var limite = queryResult.GetInt32(1);
        var jsonTrans = queryResult.GetString(2);
        await conn.CloseAsync();
        return new ExtractDto()
        {
            Saldo = new SaldoDto
            {
                total = total,
                limite = limite,
                data_extrato = DateTime.Now
            },
            ultimas_transacoes = JsonSerializer.Deserialize(jsonTrans, AppJsonSerializerContext.Default.TransactionDtoArray) ?? []
        };
    }


    public async Task<(int, int)?> ValidateTransactionAsync(int id, int value, char type)
    {
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(QUERY_VERIFY_VALID_TRANSACTION, conn);
        cmd.Parameters.Add(new NpgsqlParameter<int>("id", id));
        await using var result = await cmd.ExecuteReaderAsync();
        if (!result.HasRows) return null;
        await result.ReadAsync();
        var balance = result.GetInt32(0);
        var limit = result.GetInt32(1);
        await conn.CloseAsync();
        var newBalance = type == 'd' ? balance - value : balance + value;
        if (type == 'd' && -newBalance > limit) return (-1, 0);
        return (limit, newBalance);
    }

    public async Task<int[]> CreateTransaction(int id, CreateTransactionDto dto)
    {
        await conn.OpenAsync();
        var cmd = new NpgsqlCommand(CREATE_TRANSACTION, conn)
        {
            Parameters =
            {
                new NpgsqlParameter<int>("id", id),
                new NpgsqlParameter<int>("valor", dto.Valor),
                new NpgsqlParameter<string>("desc", dto.Descricao!),
                new NpgsqlParameter<char>("tipo", dto.Tipo),
            }
        };
        await using var result = await cmd.ExecuteReaderAsync();
        if (!await result.ReadAsync()) return [0,1,0,0];
        var data = (result.GetValue(0) as int[]) ?? [1,0,0,0];
        return data;
    }
    
    #region QUERIES

    private const string QUERY_EXTRACT = @"
    SELECT
        c.saldo total,
        c.limite,
        CASE WHEN COUNT(t) > 0 THEN JSON_AGG(t.*)
        ELSE '[]'::json
        END ultimas_transacoes
    FROM clientes c
    LEFT JOIN transacoes t ON t.cliente_id = c.id
    WHERE c.id = @id
    GROUP BY c.id;
";

    private const string QUERY_VERIFY_VALID_TRANSACTION = @"
    SELECT
        c.valor,
        c.limite
    FROM clientes c
    WHERE c.id = @id;
";

    private const string CREATE_TRANSACTION = @"
    SELECT CREATE_TRANSACTION(@id, @valor, @tipo, @desc);
";

    #endregion
}