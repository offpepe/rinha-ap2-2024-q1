namespace Rinha2024.Dotnet.DTOs;

public readonly struct ValidateTransactionDto
{
    public ValidateTransactionDto(int limit, int balance)
    {
        Limite = limit;
        Saldo = balance;
    }
    public int Limite { get; init; }
    public int Saldo { get; init; }
}