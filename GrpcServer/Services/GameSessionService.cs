using System;
using System.Threading.Tasks;
using Grpc.Core;
using GameServer.Network.Grpc; // 引用 Proto 產生的命名空間

public class GameSessionService : IGameSessionService.IGameSessionServiceBase
{
    /// <summary>
    /// 實作 1: 一問一答的登入驗證
    /// </summary>
    public override Task<LoginResponse> VerifyLogin(LoginRequest request, ServerCallContext context)
    {
        return Task.FromResult(new LoginResponse
        {
            Success = true,
            Message = "Login Success",
            Token = Guid.NewGuid().ToString(),
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    /// <summary>
    /// 實作 2: 雙向串流心跳 (Stream Request -> Stream Response)
    /// </summary>
    public override async Task KeepAliveStream(
        IAsyncStreamReader<HeartbeatPing> requestStream, 
        IServerStreamWriter<HeartbeatPong> responseStream, 
        ServerCallContext context)
    {
        var peer = context.Peer; // 獲取玩家的 IP 位址
        Console.WriteLine($"🔌 [Server] 玩家已建立心跳串流連線: {peer}");

        // 設定伺服器端的接收逾時偵測 (例如: 30 秒沒收到 Ping 就視為斷線)
        TimeSpan clientTimeout = TimeSpan.FromSeconds(30);

        try
        {
            // 只要玩家沒有中斷連線 (context.CancellationToken 沒被觸發)
            while (!context.CancellationToken.IsCancellationRequested)
            {
                // 使用具有逾時機制的 Task 等待玩家下一個 Ping 包
                var readTask = requestStream.MoveNext(context.CancellationToken);
                var timeoutTask = Task.Delay(clientTimeout);

                var completedTask = await Task.WhenAny(readTask, timeoutTask);

                // 情境 A：逾時了 (玩家在 30 秒內完全沒發心跳過來)
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine($"⏱️ [Server] 玩家 {peer} 心跳逾時！中斷連線。");
                    throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Heartbeat timeout"));
                }

                // 情境 B：順利收到玩家的訊息
                if (await readTask)
                {
                    var ping = requestStream.Current;
                    Console.WriteLine($"💓 [Server] 收到玩家 Ping #{ping.SequenceId}，Token: {ping.Token}");

                    // 回應 Pong 給玩家
                    await responseStream.WriteAsync(new HeartbeatPong
                    {
                        AckSequenceId = ping.SequenceId,
                        ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }
                else
                {
                    // 玩家主動關閉了串流 (例如正常關閉遊戲)
                    Console.WriteLine($"👋 [Server] 玩家 {peer} 已主動關閉心跳連線。");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [Server] 玩家 {peer} 連線異常中斷: {ex.Message}");
        }
        finally
        {
            // 執行清理玩家在線狀態、儲存存檔等邏輯
            Console.WriteLine($"🧹 [Server] 清理玩家 {peer} 的連線資源。");
        }
    }
}