using Microsoft.AspNetCore.Mvc;
using Rinha2024.Dotnet.DTOs;
using Rinha2024.Dotnet.Exceptions;

namespace Rinha2024.Dotnet.Extensions;

public static class ControllerSetup
{
    public static void SetControllers(this WebApplication app)
    {
        app.MapGet("/ping", () => "pong");
        app.MapGet("/clientes/{id:int}/extrato",
            async (int id, [FromServices] Service service) =>
            {
                var res = await service.GetExtract(id);
                return res.HasValue ? Results.Ok(res.Value) : Results.NotFound();
            });
        app.MapPost("/clientes/{id:int}/transacoes", async (int id, [FromServices] Service service, [FromBody] CreateTransactionDto dto) =>
        {
            if (string.IsNullOrEmpty(dto.Descricao)) return Results.UnprocessableEntity();
            var res = await service.CreateTransaction(id, dto);
            if (res[0] == 1) return Results.NotFound();
            return res[1] == 1
                ? Results.UnprocessableEntity()
                : Results.Ok(new ValidateTransactionDto(res[2], res[3]));
        });
    }
}