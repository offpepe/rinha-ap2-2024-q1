using System.Data;
using System.Threading.Channels;
using Npgsql;

namespace Rinha2024.Dotnet;

public class Database(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private bool _disposed;

    private static readonly int ReadPoolSize = int.TryParse(Environment.GetEnvironmentVariable("READ_POOL_SIZE"), out var value) ? value : 1500;
    private static readonly int WritePoolSize = int.TryParse(Environment.GetEnvironmentVariable("WRITE_POOL_SIZE"), out var value) ? value : 3000;

    private readonly Pool _readPool =
        new(
            CreateCommands(
                new NpgsqlCommand(QUERY_TRANSACTION)
                    {Parameters = {new NpgsqlParameter<int> {NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer}}},
                ReadPoolSize),
            ReadPoolSize);
    
    private readonly Pool _writePool =
        new(
            CreateCommands(
                new NpgsqlCommand("INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES ($1,$2,$3,$4);")
                    {Parameters =
                    {
                        new NpgsqlParameter<int> {NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer},
                        new NpgsqlParameter<int> {NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer},
                        new NpgsqlParameter<char> {NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Char},
                        new NpgsqlParameter<string> {NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text},
                    }},
                WritePoolSize),
            WritePoolSize);
 
    
    public async Task<IEnumerable<TransactionDto>> GetTransactions(int id)
    {
        await using var poolItem = await _readPool.RentAsync();
        var cmd = poolItem.Value;
        cmd.Parameters[0].Value = id;
        await using var connection = await dataSource.OpenConnectionAsync();
        cmd.Connection = connection;
        await using var reader = await cmd.ExecuteReaderAsync();
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

        return transactions;
    }
    
   

    public async Task InsertTransaction(int id, CreateTransactionDto dto)
    {
        await using var poolItem = await _writePool.RentAsync();
        var cmd = poolItem.Value;
        cmd.Parameters[0].Value = id;
        cmd.Parameters[1].Value = dto.Valor;
        cmd.Parameters[2].Value = dto.Tipo;
        cmd.Parameters[3].Value = dto.Descricao;
        await using var conn = await dataSource.OpenConnectionAsync();
        cmd.Connection = conn;
        await cmd.ExecuteNonQueryAsync();
    }
    
   
    private static IEnumerable<NpgsqlCommand> CreateCommands(NpgsqlCommand cmd, int qtd)
    {
        for (var i = 0; i < qtd; i++)
        {
            yield return cmd.Clone();
        }

        yield return cmd;
    }

    private const string RESET_DB = @"
    BEGIN;
    DELETE FROM transacoes WHERE id IS NOT NULL;
    UPDATE clientes SET saldo = 0 WHERE id is not null;
    COMMIT;
";
    
    private const string QUERY_BALANCE = @"
    SELECT 
        c.saldo,
        c.limite
    FROM clientes c
    WHERE c.id = $1;
";

    private const string QUERY_EXTRACT = @"
    WITH trans AS (
        SELECT
            valor,
            tipo,
            descricao,
            realizada_em::text
        FROM transacoes
        WHERE cliente_id = $1
        ORDER BY id DESC
        LIMIT 10
    )
    SELECT
        t.*,
        c.saldo,
        c.limite
    FROM clientes c
    LEFT JOIN trans t ON true
    WHERE id = $1;";

    private const string QUERY_TRANSACTION = @"
    SELECT
        valor,
        tipo,
        descricao,
        realizada_em::text
    FROM transacoes
    WHERE cliente_id = $1
    ORDER BY id DESC
    LIMIT 10;
";

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _readPool.DisposeAsync();
    }

    private sealed class Pool : IAsyncDisposable
    {
        private readonly Channel<NpgsqlCommand> _queue = Channel.CreateUnbounded<NpgsqlCommand>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = false,
                SingleWriter = false
            });

        public Pool(IEnumerable<NpgsqlCommand> items, int poolSize)
        {
            for (var i = 0; i < poolSize; i++)
            {
                _ = _queue.Writer.TryWrite(items.ElementAt(i));
            }
        }

        public async ValueTask<PoolItem> RentAsync()
        {
            NpgsqlCommand? item = null;
            try
            {
                item = await _queue.Reader.ReadAsync();
                var poolItem = new PoolItem(item, ReturnPoolItemAsync);
                return poolItem;
            }
            catch
            {
                if (item != null)
                    await _queue.Writer.WriteAsync(item);
                throw;
            }
        }

        private async ValueTask<List<NpgsqlCommand>> ReturnAllAsync()
        {
            var items = new List<NpgsqlCommand>();
            await foreach (var item in _queue.Reader.ReadAllAsync())
                items.Add(item);
            return items;
        }

        private async ValueTask ReturnPoolItemAsync(PoolItem poolItem)
        {
            poolItem.Value.Connection = null;
            await _queue.Writer.WriteAsync(poolItem.Value);
        }

        public async ValueTask DisposeAsync()
        {
            var items = await ReturnAllAsync();
            await Parallel.ForEachAsync(items, (item, _) => item.DisposeAsync());
        }
    }

    public readonly struct PoolItem(NpgsqlCommand value, Func<PoolItem, ValueTask> returnPoolItemAsync)
    {
        public NpgsqlCommand Value { get; } = value;

        public ValueTask DisposeAsync() => returnPoolItemAsync(this);
    }
}