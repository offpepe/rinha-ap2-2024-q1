using System.Collections.Concurrent;
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
        app.MapPost("/clientes/{id:int}/transacoes", async (int id,
            [FromServices] Service service,
            [FromBody] CreateTransactionDto dto) =>
        {
            if (string.IsNullOrEmpty(dto.Descricao)) return Results.UnprocessableEntity();
            dto.Id = id;
            var tuple = await service.ValidateTransactionAsync(dto);
            if (!tuple.HasValue) return Results.NotFound();
            var (limit, balance) = tuple.Value;
            return limit < 0 ? Results.UnprocessableEntity() : Results.Ok(new ValidateTransactionDto(limit, balance));
        });
    }
}