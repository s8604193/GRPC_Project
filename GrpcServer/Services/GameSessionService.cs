using System;
using System.Threading.Tasks;
using Grpc.Core;
using GameServer.Network.Grpc; // 引用 Proto 產生的命名空間

public class GameSessionService : IGameSessionService.IGameSessionServiceBase
{
    // 模擬驗證 Token 的方法 (實際操作可能會查 Redis 或資料庫)
    private bool ValidateToken(string token)
    {
        // 這裡寫您的驗證邏輯，例如檢查 JWT 是否過期
        return !string.IsNullOrEmpty(token) && token.StartsWith("PLAYER_TOKEN");
    }

    /// <summary>
    /// 實作 1: 一問一答的登入驗證
    /// </summary>
    public override Task<LoginResponse> VerifyLogin(LoginRequest request, ServerCallContext context)
    {
        bool isValid = ValidateToken(request.Token);
        
        return Task.FromResult(new LoginResponse
        {
            Success = isValid,
            Message = isValid ? "Login Success" : "Invalid Token",
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
                    break; // 跳出迴圈，中斷串流，玩家端會收到連線錯誤並觸發登出重連
                }

                // 情境 B：順利收到玩家的訊息
                if (await readTask)
                {
                    var ping = requestStream.Current;
                    Console.WriteLine($"💓 [Server] 收到玩家 Ping #{ping.SequenceId}，Token: {ping.Token}");

                    // 每次收到心跳，再次動態驗證 Token 是否依然有效 (防止後端主動註銷 Token 卻沒踢人)
                    bool isSessionValid = ValidateToken(ping.Token);

                    // 回應 Pong 給玩家
                    await responseStream.WriteAsync(new HeartbeatPong
                    {
                        AckSequenceId = ping.SequenceId,
                        IsValidSession = isSessionValid, // 如果變 false，玩家端收到會自動登出
                        ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    if (!isSessionValid)
                    {
                        Console.WriteLine($"🚨 [Server] 玩家 {peer} 的 Token 已失效，拒絕此 Session。");
                        break; 
                    }
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