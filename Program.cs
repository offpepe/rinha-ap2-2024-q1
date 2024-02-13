using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Rinha2024.Dotnet;
using Rinha2024.Dotnet.DTOs;
using Rinha2024.Dotnet.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseKestrel(opt =>
{
    opt.Limits.MaxConcurrentConnections = 100;
    opt.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddTransient<NpgsqlConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("DB")!));
builder.Services.AddSingleton<ConcurrentQueue<CreateTransactionDto>>();
builder.Services.AddHostedService<TransactionHandler>();
builder.Services.AddTransient<Service>();
builder.Services.AddLogging(l => l.AddSimpleConsole());
var app = builder.Build();
app.SetControllers();
app.Run();

[JsonSerializable(typeof(SaldoDto))]
[JsonSerializable(typeof(TransactionDto))]
[JsonSerializable(typeof(TransactionDto[]))]
[JsonSerializable(typeof(CreateTransactionDto))]
[JsonSerializable(typeof(ExtractDto))]
[JsonSerializable(typeof(ValidateTransactionDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}