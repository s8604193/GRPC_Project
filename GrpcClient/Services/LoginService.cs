
using GameServer.Network.Grpc;
using R3;
using Grpc.Core;
using System.Text;
using System.Runtime.CompilerServices;

public class LoginService : ILogin
{
    private readonly Subject<Exception> handleCriticalNetworkError = new();
    private readonly Subject<Unit> onReconnected = new();
    private readonly Subject<Unit> onLogOut = new();

    Observable<Exception> ILogin.OnCriticalNetworkError => handleCriticalNetworkError;
    Observable<Unit> ILogin.OnReconnected => onReconnected;
    
    Observable<Unit> ILogin.OnLogOut => onLogOut;

    private IGameSessionService.IGameSessionServiceClient service;
    private LoginRequest request = new()
    {
        Account = "",
        Password = "",
        ClientVersion = "0.0"
    };
    private CancellationTokenSource? _cts;

    private IDisposable ListenerOnCriticalNetworkError;
    private IDisposable ListenerOnReconnected;
    private AsyncDuplexStreamingCall<HeartbeatPing, HeartbeatPong>? _streaming;
    private IChannel _channel;

    private readonly Dictionary<long, DateTimeOffset> _pingHistory = new();
    private long _currentSequenceId = 0;

    private double Ping;

    private float _pingInterval = 5;

    private UserSession _userSession;

    public LoginService(IChannel _channelService,UserSession userSession)
    {
        _userSession = userSession;
        _channel = _channelService;
        service = new IGameSessionService.IGameSessionServiceClient(_channelService.GetChannel());
        ListenerOnCriticalNetworkError = _channelService.OnCriticalNetworkError.Subscribe(OnCriticalNetworkError);
        ListenerOnReconnected = _channelService.OnReconnected.Subscribe(OnReconnected);
    }

    private void OnCriticalNetworkError(Exception ex)
    {
        CloseConnection();
        handleCriticalNetworkError.OnNext(ex);
    }

    private void OnReconnected(Unit _)
    {
        onReconnected.OnNext(_);
    }

    public async Task<bool> Login()
    {
        var response = await service.VerifyLoginAsync(request);
        _userSession.Token = response.Token;
        return response.Success;
    } 

    public void KeepAliveStream()
    {
        _cts = new();
        _streaming = service.KeepAliveStream();
        _ = ListenToDisconnectAsync(_streaming.ResponseStream,_cts.Token);
        _ = StartSendingPingTimerAsync(_cts.Token);
    }

    private async Task ListenToDisconnectAsync(IAsyncStreamReader<HeartbeatPong> responseStream, CancellationToken token)
    {
        try
        {
            await foreach (var srEvent in responseStream.ReadAllAsync(cancellationToken: token))
            {
                var sequenceId = srEvent.AckSequenceId;

                if(_pingHistory.TryGetValue(sequenceId, out var pingTime))
                {
                    Ping = (DateTime.Now - pingTime).TotalMilliseconds / 1000;
                    _pingHistory.Remove(sequenceId);
                }
            }
            Console.WriteLine("伺服器中斷連線");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine("客戶端或伺服器取消了連線");
            handleCriticalNetworkError.OnNext(ex);
        }
        catch (RpcException ex)
        {
            Console.WriteLine(ex.Message);
            handleCriticalNetworkError.OnNext(ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"網路連線異常中斷: {ex.Message}");
            handleCriticalNetworkError.OnNext(ex);
        }
    }

    public async Task StartSendingPingTimerAsync(CancellationToken token)
    {
        var timeInterval = TimeSpan.FromSeconds(_pingInterval);
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(timeInterval, token);
                
                if (!_channel.IsConnect()) continue;

                await SendPingMessage();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] 發送心跳失敗: {ex.Message}");
            }
        }
    }

    private  async Task SendPingMessage()
    {
        if (_streaming == null) throw new InvalidOperationException("串流尚未建立或已關閉");

        var ping = new HeartbeatPing
        {
            Token = _userSession.Token,
            SequenceId = _currentSequenceId
        };

        _pingHistory.Add(_currentSequenceId,DateTime.Now);

        _currentSequenceId += 1;

        await _streaming.RequestStream.WriteAsync(ping);
    }

    public void CloseConnection()
    {
        Console.WriteLine("[gRPC] Client 開始執行主動斷線...");
        if(_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose(); 
        }

        if(_streaming != null)
        {
            _streaming.RequestStream.CompleteAsync();
            _streaming.Dispose();
        }

        _userSession.Clear();
    }
}