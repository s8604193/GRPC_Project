using Grpc.HealthCheck;
using GrpcServer.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

// 2. 註冊 Google 官方的 gRPC 健康檢查服務
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("ImageService 運作正常"));
    // 💡 您也可以在這裡加入自訂檢查，例如：去 Ping 看看 Redis 有沒有斷線，
    // 如果 Redis 斷線了，就回傳 Unhealthy，讓負載平衡器自動不把流量送過來。

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GameSessionService>();
app.MapGrpcService<ImageService>();
app.MapGrpcService<HealthServiceImpl>();
app.MapGrpcService<ChatService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
