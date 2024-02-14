using System.Text.Json.Serialization;

namespace Rinha2024.Dotnet.DTOs;

public class CreateTransactionDto
{
    [JsonIgnore]
    public int Id { get; set; }
    public int Valor { get; init; }
    public char Tipo { get; init; }
    public string Descricao { get; init; } = string.Empty;
}