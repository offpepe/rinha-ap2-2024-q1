using System.Text.Json.Serialization;

namespace Rinha2024.Dotnet.DTOs;

public readonly struct CreateTransactionDto
{
    public CreateTransactionDto()
    {
        
    }
    public int Valor { get; init; }
    public char Tipo { get; init; }
    public string? Descricao { get; init; }
}