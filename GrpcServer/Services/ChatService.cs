using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Grpc.Core;
using Chat.V1;

public class ChatService : IChatService.IChatServiceBase
{
    private static readonly ConcurrentDictionary<string, IServerStreamWriter<ChatServerEvent>> _onlinePlayers = new();

    public override async Task EstablishChatStream(
        IAsyncStreamReader<ChatClientEvent> requestStream, 
        IServerStreamWriter<ChatServerEvent> responseStream, 
        ServerCallContext context)
    {
        var playerId = context.RequestHeaders.GetValue("player-id");
        
        if(string.IsNullOrEmpty(playerId))
        {
            playerId = "";
        }

        _ =_onlinePlayers.TryAdd(playerId,responseStream);
        Console.WriteLine($"🟢 [ChatService] 玩家 {playerId} 已建立長連線通道！");

        try
        {
            // 3. 💡 底層核心：當這條 TCP 通道沒斷開前，持續異步監聽 (Non-blocking) 玩家傳上來的事件
            await foreach (var clientEvent in requestStream.ReadAllAsync())
            {
                if (clientEvent.EventCase == ChatClientEvent.EventOneofCase.SendMessage)
                {
                    var msg = clientEvent.SendMessage;
                    Console.WriteLine($"收到玩家 {playerId} 發送訊息給 {msg.TargetUserId}: {msg.TextContent}");

                    if (_onlinePlayers.TryGetValue(msg.TargetUserId, out var targetStream))
                    {
                        var serverEvent = new ChatServerEvent
                        {
                            NewMessage = new NewMessageResponse
                            {
                                Message = new ChatMessage
                                {
                                  MessageId = Guid.NewGuid().ToString(),
                                  RoomId = $"room_{playerId}_{msg.TargetUserId}",
                                  SenderId = playerId,
                                  TextContent = msg.TextContent,
                                  CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                },
                                IsNotification = true
                            }
                        };

                        Console.WriteLine($"發送訊息給 {msg.TargetUserId}: {msg.TextContent}");
                        await targetStream.WriteAsync(serverEvent);
                    }
                    else
                    {
                        Console.WriteLine($"查無 {msg.TargetUserId}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 玩家 {playerId} 連線發生異常: {ex.Message}");
        }
        finally
        {
            _onlinePlayers.TryRemove(playerId, out _);
            Console.WriteLine($"🔴 [ChatService] 玩家 {playerId} 斷開連線，已釋放網路資源。");
        }
    }
}