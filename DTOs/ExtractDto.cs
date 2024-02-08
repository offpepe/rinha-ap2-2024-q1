
namespace Rinha2024.Dotnet.DTOs;

public readonly struct ExtractDto
{
    public ExtractDto()
    {
    }

    public SaldoDto Saldo { get; init; } 
    public TransactionDto[] ultimas_transacoes { get; init; } = [];
}

public readonly struct SaldoDto
{
    public SaldoDto(int balance, int limit)
    {
        total = balance;
        limite = limit;
    }
    public int total { get; init; }
    public int limite { get; init; }
    public DateTime data_extrato { get; } = DateTime.Now;
}

public readonly struct TransactionDto
{
    public int valor { get; init; }
    public char tipo { get; init; }
    public string? descricao { get; init; }
    public string realizada_em { get; init; }
}
