using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Rinha2024.Dotnet;
using Rinha2024.Dotnet.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);
var MAX_CONCURRENT_CONNECTIONS = builder.Configuration.GetValue<int>("MAX_CONCURRENT_CONNECTIONS");
builder.WebHost.UseKestrel(opt =>
{
    opt.Limits.MaxConcurrentConnections = MAX_CONCURRENT_CONNECTIONS;
    opt.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()
{
    ExpirationScanFrequency = TimeSpan.FromMinutes(5),
}));
builder.Services.AddTransient<NpgsqlConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("DB")!));
builder.Services.AddSingleton<ExceptionMiddleware>();
builder.Services.AddTransient<Service>();
builder.Services.AddLogging(l => l.AddSimpleConsole());
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
await app.Services.GetRequiredService<Service>().VirtualizeClients();
app.SetControllers();
app.Run();

[JsonSerializable(typeof(SaldoDto))]
[JsonSerializable(typeof(TransactionDto))]
[JsonSerializable(typeof(CreateTransactionDto))]
[JsonSerializable(typeof(ExtractDto))]
[JsonSerializable(typeof(ValidateTransactionDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}