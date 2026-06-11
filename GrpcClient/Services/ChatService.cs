using Chat.V1;
using Grpc.Core;
using Grpc.Net.Client;
using R3;

public class ChatService : IChat 
{
    IChatService.IChatServiceClient _service;

    ILogin _loginService;

    private CancellationTokenSource? _cts;
    private AsyncDuplexStreamingCall<ChatClientEvent, ChatServerEvent>? _stream;
    private Task? _listeningTask;

    private IDisposable ListenOnLogOut;
    private UserSession _userSession;
    public ChatService(UserSession userSession,ChannelService channelService,ILogin loginService)
    {
        _userSession = userSession;
        _loginService = loginService;
        _service = new IChatService.IChatServiceClient(channelService.GetChannel());
        ListenOnLogOut = _loginService.OnLogOut.Subscribe(OnLogOut);
    }

    private void OnLogOut(Unit _)
    {
        if(_stream != null)
        {
            _stream.RequestStream.CompleteAsync();
            _stream.Dispose();
        }
    }

    public void CreateChatStream()
    {
        _cts = new();
        Metadata header = new Metadata
        {
            { "player-id", _userSession.Token }
        };
        _stream = _service.EstablishChatStream(header,cancellationToken: _cts.Token);
        _listeningTask = Task.Run(() => ListenToServerAsync(_cts.Token));
    }

    private async Task ListenToServerAsync(CancellationToken token)
    {
        if(_stream == null)
            return;

        await foreach (var serverEvent in _stream.ResponseStream.ReadAllAsync(token))
        {
            // 💡 利用 oneof 的 EventCase 判定這是伺服器的哪一種封包
            switch (serverEvent.EventCase)
            {
                case ChatServerEvent.EventOneofCase.NewMessage:
                    var msg = serverEvent.NewMessage.Message;
                    Console.WriteLine($"\n[聊天室] 收到來自 {msg.SenderId} 的新訊息: {msg.TextContent}");
                    
                    // TODO: 這裡通知你的 UI 畫面渲染新聊天泡泡

                    // ⚡ 【核心 ACK 機制】：秒傳一個極小的封包回 SR 報平安！
                    await SendMessageAckAsync(msg.MessageId);
                    break;

                case ChatServerEvent.EventOneofCase.TypingStatus:
                    var typing = serverEvent.TypingStatus;
                    Console.WriteLine($"[聊天室] 使用者 {typing.UserId} 正在打字中: {typing.IsTyping}");
                    break;

                case ChatServerEvent.EventOneofCase.MessageRead:
                    var read = serverEvent.MessageRead;
                    Console.WriteLine($"[聊天室] {read.ReaderId} 已讀到了訊息 ID: {read.LastReadMsgId}");
                    break;

                case ChatServerEvent.EventOneofCase.UserPresence:
                    var presence = serverEvent.UserPresence;
                    Console.WriteLine($"[聊天室] 使用者 {presence.UserId} 狀態變更: " + (presence.IsOnline ? "上線" : "下線"));
                    break;
            }
        }
    }

    public  async Task SendChatMessage(string targetUserId, string text)
    {
        if (_stream == null) throw new InvalidOperationException("串流尚未建立或已關閉");

        var clientEvent = new ChatClientEvent
        {
            SendMessage = new SendMessageRequest
            {
                TargetUserId = targetUserId,
                TextContent = text
            }
        };

        await _stream.RequestStream.WriteAsync(clientEvent);
        Console.WriteLine($"[Chat] ✉️ 訊息已發送給 {targetUserId}: '{text}'");
    }

    private async Task SendMessageAckAsync(string messageId)
    {
        if (_stream == null) return;

        var ackEvent = new ChatClientEvent
        {
            MessageAck = new MessageAck
            {
                MessageId = messageId,
                AckTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        await _stream.RequestStream.WriteAsync(ackEvent);
        Console.WriteLine($"[ACK 專線] 已向 SR 回報收到訊息: {messageId}");
    }
}