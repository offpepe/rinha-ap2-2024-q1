using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using Npgsql;
using Rinha2024.Dotnet.DTOs;
using Rinha2024.Dotnet.Exceptions;
using Rinha2024.Dotnet.Extensions;

namespace Rinha2024.Dotnet;

public class Service(string connectionString)
{
    private readonly IDbConnection _db = new NpgsqlConnection(connectionString);

    public async Task<ExtractDto> GetExtract(int id)
    {
        var parametes = new DynamicParameters();
        parametes.Add("id", id);
        var res = await _db.QueryUnmapped(QUERY_EXTRACT, parametes) ?? throw new NotFoundException();
        var transactions = JsonSerializer.Deserialize<TransactionDto[]>((string) res[2].Value,
            AppJsonSerializerContext.Default.TransactionDtoArray);
        return new ExtractDto()
        {
            Saldo = new SaldoDto((int) res[0].Value, (int) res[1].Value),
            ultimas_transacoes = transactions ?? []
        };
    }


    public async Task<ValidateTransactionDto> ValidateTransactionAsync(int id, int value, char type)
    {
        var parameter = new DynamicParameters();
        parameter.Add("id", id);
        parameter.Add("value", value);
        var keyValuePairs =
            await _db.QueryUnmapped(QUERY_VERIFY_VALID_TRANSACTION, parameter) ??
            throw new NotFoundException();
        var balance = (int) keyValuePairs[0].Value;
        var limit = (int) keyValuePairs[1].Value;
        if (type == 'd' && (balance - value) * -1 > limit) throw new UnprocessableContentException();
        return new ValidateTransactionDto(limit, type == 'd' ? balance - value : balance + value);
    }

    public async Task CreateTransaction(int id, CreateTransactionDto dto)
    {
        var parameters = new DynamicParameters();
        parameters.Add("id", id);
        parameters.Add("Valor", dto.Valor);
        parameters.Add("Descricao", dto.Descricao);
        if (dto.Tipo == 'c')
        {
            await _db.ExecuteAsync(CREATE_CREDIT_TRANSACTION_TRANS, parameters);
            return;
        }

        await _db.ExecuteAsync(CREATE_DEBIT_TRANSACTION_TRANS, parameters);
    }


    #region QUERIES

    private const string QUERY_EXTRACT = @"
    SELECT
        s.valor total,
        c.limite,
        CASE WHEN COUNT(t) > 0 THEN JSON_AGG(t.*)
        ELSE '[]'::json
        END ultimas_transacoes
    FROM clientes c
    LEFT JOIN transacoes t ON t.cliente_id = c.id
    INNER JOIN saldos s ON s.cliente_id = c.id
    WHERE c.id = @id
    GROUP BY s.id, c.id;
";

    private const string QUERY_VERIFY_VALID_TRANSACTION = @"
    SELECT
        s.valor,
        c.limite
    FROM clientes c
    INNER JOIN saldos s ON s.cliente_id = c.id
    WHERE c.id = @id;
";

    private const string CREATE_DEBIT_TRANSACTION_TRANS = @"
    CALL CREATE_DEBIT_TRANSACTION(@id, @Valor, @Descricao);
";

    private const string CREATE_CREDIT_TRANSACTION_TRANS = @"
    CALL CREATE_CREDIT_TRANSACTION(@id, @Valor, @Descricao);
    ";

    #endregion
}