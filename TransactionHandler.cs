using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Routing;
using Npgsql;
using Rinha2024.Dotnet.DTOs;

namespace Rinha2024.Dotnet;

public class TransactionHandler(NpgsqlConnection conn, ConcurrentQueue<CreateTransactionDto> queue) : BackgroundService
{
    private static readonly int TransactionsPerBatch = int.Parse(Environment.GetEnvironmentVariable("TRANSACTIONS_PER_BATCH") ?? "30");
    private static readonly int IdleTime = int.Parse(Environment.GetEnvironmentVariable("BATCH_SERVICE_IDLENESS") ?? "200");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {

            if (queue.IsEmpty)
            {
                await Task.Delay(IdleTime,stoppingToken);
                continue;
            }

            var itens = new HashSet<CreateTransactionDto>(TransactionsPerBatch);
            for (var i = 0; i < TransactionsPerBatch; i++)
            {
                var hasItem = queue.TryDequeue(out var item);
                if (!hasItem)
                {
                    break;
                }
                itens.Add(item!);
            }
            if (itens.Count == 0)
            {
                continue;
            }

            try
            {
                await conn.OpenAsync(stoppingToken);
                await using var batch = new NpgsqlBatch(conn);
                foreach (var transaction in itens)
                {
                    batch.BatchCommands.Add(new NpgsqlBatchCommand(CREATE_TRANSACTION)
                    {
                        Parameters =
                        {
                            new NpgsqlParameter<int>("id", transaction.Id),
                            new NpgsqlParameter<int>("valor", transaction.Valor),
                            new NpgsqlParameter<string>("desc", transaction.Descricao!),
                            new NpgsqlParameter<char>("tipo", transaction.Tipo),
                        }
                    });
                }

                await batch.ExecuteNonQueryAsync(stoppingToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            await conn.CloseAsync();
        }
    }

    private const string CREATE_TRANSACTION = @"INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (@id, @valor, @tipo, @desc);";
}