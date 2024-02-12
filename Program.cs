using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Npgsql;
using Rinha2024.Dotnet;
using Rinha2024.Dotnet.DTOs;
using Rinha2024.Dotnet.Extensions;

var dbHost = Environment.GetEnvironmentVariable("DB_HOSTNAME");
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseKestrel();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddTransient<NpgsqlConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("DB")));
builder.Services.AddSingleton<ExceptionMiddleware>();
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