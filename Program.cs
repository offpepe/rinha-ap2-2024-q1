using System.Net.Sockets;
using System.Text.Json.Serialization;
using Npgsql;
using Rinha2024.Dotnet;
using Rinha2024.Dotnet.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(opt =>
{
    opt.Limits.MaxConcurrentConnections = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_CONNECTIONS"), out var maxConcurrentConnections) ? maxConcurrentConnections : 500;
    opt.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddSingleton<Socket>(_ => new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) 
{ 
    ReceiveTimeout = int.TryParse(Environment.GetEnvironmentVariable("UDP_SOCKET_RECEIVE_TIMEOUT"), out var udpSocketReceiveTimeout) ? udpSocketReceiveTimeout : 30, 
    SendTimeout = int.TryParse(Environment.GetEnvironmentVariable("UDP_SOCKET_SEND_TIMEOUT"), out var udpSocketSendTimeout) ? udpSocketSendTimeout : 30, 
}); 
builder.Services.AddSingleton<NpgsqlDataSource>(_ => new NpgsqlSlimDataSourceBuilder(builder.Configuration.GetConnectionString("DB")!).Build());
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<VirtualDatabase>();
builder.Services.AddSingleton<ExceptionMiddleware>();
builder.Services.AddHostedService<SyncVirtualDatabases>();
builder.Services.AddLogging(l => l.AddSimpleConsole());
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.SetControllers();
app.Run();

[JsonSerializable(typeof(SaldoDto))]
[JsonSerializable(typeof(TransactionDto))]
[JsonSerializable(typeof(CreateTransactionDto))]
[JsonSerializable(typeof(ExtractDto))]
[JsonSerializable(typeof(ValidateTransactionDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
    //empty
}
