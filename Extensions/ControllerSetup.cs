using Microsoft.AspNetCore.Mvc;

namespace Rinha2024.Dotnet.Extensions;

public static class ControllerSetup
{
    public static void SetControllers(this WebApplication app)
    {
        app.MapGet("/ping", () => "pong");
        app.MapGet("/clientes/{id:int}/extrato",
            async (int id, [FromServices] VirtualDatabase vdb,[FromServices] Database db) =>
            {
                if (id < 1 || id >= vdb.Size) return Results.NotFound();
                var client = vdb.GetClient(ref id);
                var transactions = await db.GetTransactions(id);
                return Results.Ok(new ExtractDto(new SaldoDto(client[0], client[1]), transactions));
            });
        app.MapPost("/clientes/{id:int}/transacoes", async (int id,
            [FromServices] VirtualDatabase vdb,
            [FromServices] Database db,
            [FromBody] CreateTransactionDto dto) =>
        {
            if (id < 1 || id >= vdb.Size) return Results.NotFound();
            if (!ValidateTransaction(dto)) return Results.UnprocessableEntity();
            var result = await vdb.DoTransaction(id, dto.Tipo, dto.Valor);
            if (result[1] == -1) return Results.UnprocessableEntity();
            await db.InsertTransaction(id, dto);
            return Results.Ok(new ValidateTransactionDto(result[1], result[0]));
        });
    }

    private static bool ValidateTransaction(CreateTransactionDto dto)
    {
        if (string.IsNullOrEmpty(dto.Descricao)) return false;
        if (dto.Descricao.Length > 10) return false;
        if (dto.Tipo != 'd' && dto.Tipo != 'c') return false;
        return true;
    }
}