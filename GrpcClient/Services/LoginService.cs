
using GameServer.Network.Grpc;
using R3;
using Grpc.Core;
using System.Text;

public class LoginService : ILogin
{
    private readonly Subject<Exception> handleCriticalNetworkError = new();
    private readonly Subject<Unit> onReconnected = new();

    Observable<Exception> ILogin.OnCriticalNetworkError => handleCriticalNetworkError;
    Observable<Unit> ILogin.OnReconnected => onReconnected;

    private IGameSessionService.IGameSessionServiceClient service;
    private LoginRequest request = new()
    {
        PlayerId = "7",
        Token = "PLAYER_TOKEN7",
        ClientVersion = "0.0"
    };
    private CancellationTokenSource? _cts;

    private IDisposable ListenerOnCriticalNetworkError;
    private IDisposable ListenerOnReconnected;

    public LoginService(IChannel _channelService)
    {
        service = new IGameSessionService.IGameSessionServiceClient(_channelService.GetChannel());
        ListenerOnCriticalNetworkError = _channelService.OnCriticalNetworkError.Subscribe(OnCriticalNetworkError);
        ListenerOnReconnected = _channelService.OnReconnected.Subscribe(OnReconnected);
    }

    private void OnCriticalNetworkError(Exception ex)
    {
        
    }

    private void OnReconnected(Unit _)
    {
        
    }

    public async Task<LoginResponse> Login()
    {
        var response = await service.VerifyLoginAsync(request);
        
        return new LoginResponse 
        {
            Success = response.Success,
            Message = response.Message
        };
    } 

    public void KeepAliveStream()
    {
        _cts = new();
        var streamingcall = service.KeepAliveStream();
        _ = ListenToDisconnectAsync(streamingcall.ResponseStream,_cts.Token);
    }

    private async Task ListenToDisconnectAsync(IAsyncStreamReader<HeartbeatPong> responseStream, CancellationToken token)
    {
        try
        {
            // 只要連線正常，MoveNext() 就會一直卡著等伺服器的下一次心跳包
            // 當伺服器主動關閉 Stream，或者網路斷開時，迴圈會結束或拋出異常
            while (await responseStream.MoveNext(token))
            {
                var serverHeartbeat = responseStream.Current;
                // 這裡可以處理伺服器送過來的心跳回應（如果需要的話）
            }

            // 迴圈正常結束，代表伺服器優雅地關閉了連線
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

    public void DisconnectAsync()
    {
        Console.WriteLine("[gRPC] Client 開始執行主動斷線...");
        if(_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose(); 
        }
    }
}