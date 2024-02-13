using System.Collections.Concurrent;
using Npgsql;
using Rinha2024.Dotnet.DTOs;

namespace Rinha2024.Dotnet;

public class TransactionHandler(NpgsqlConnection conn, ConcurrentQueue<CreateTransactionDto> queue) : BackgroundService
{
    private static readonly int TransactionsPerBatch = int.Parse(Environment.GetEnvironmentVariable("TRANSACTIONS_PER_BATCH") ?? "30");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var itens = Dequeue(queue).ToArray();
            if (itens.Length == 0)
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
                            new NpgsqlParameter<int>("id", transaction.Id!.Value),
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

    private static IEnumerable<CreateTransactionDto> Dequeue(ConcurrentQueue<CreateTransactionDto> queue)
    {
        var itemsReleased = 0;
        while (TransactionsPerBatch > itemsReleased && queue.TryDequeue(out var response))
        {
            itemsReleased++;
            yield return response;
        }
    }

    private const string CREATE_TRANSACTION = @"SELECT CREATE_TRANSACTION(@id, @valor, @tipo, @desc);";
}