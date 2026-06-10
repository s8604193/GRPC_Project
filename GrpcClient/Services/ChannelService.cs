using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using R3;

public class ChannelService : IChannel
{
    private readonly int port = 5278;
    private GrpcChannel _channel;
    private int _reconnectCount;

    private CancellationTokenSource _cts = new();

    private readonly Subject<Exception> handleCriticalNetworkError = new();
    private readonly Subject<Unit> onReconnected = new();

    // 暴露成唯讀的 Observable 給外面看
    Observable<Exception> IChannel.OnCriticalNetworkError => handleCriticalNetworkError;
    Observable<Unit> IChannel.OnReconnected => onReconnected;

    public ChannelService()
    {
        _channel = GrpcChannel.ForAddress($"http://localhost:{port}");
        _cts = new();
        StartConnectivityMonitoring(_cts.Token);
    }

    private void StartConnectivityMonitoring(CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var currentState = _channel.State;
                Console.WriteLine($"[gRPC 狀態機] 當前狀態: {currentState}");

                if (currentState == ConnectivityState.TransientFailure)
                {
                    Console.WriteLine($"[gRPC 狀態機] 偵測到實體網路斷開！");
                    
                    _ = HandleNetworkLossFlowAsync(); 
                }

                try
                {
                    await _channel.WaitForStateChangedAsync(currentState, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[gRPC 狀態機] 監聽發生異常: {ex.Message}");
                    await Task.Delay(2000, token); // 發生極罕見異常時，稍微等一下再繼續
                }
            }
        }, token);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GrpcChannel GetChannel()
    {
        return _channel;
    }

    public async Task HandleNetworkLossFlowAsync()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        _reconnectCount = 0;
        bool isConnected = false;

        while (!isConnected && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                _reconnectCount++;
                
                int delaySeconds = Math.Min((int)Math.Pow(2, _reconnectCount - 1), 8);
                Console.WriteLine($"等待 {delaySeconds} 秒後嘗試第 {_reconnectCount} 次物理重連...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _cts.Token);

                using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await _channel.ConnectAsync(timeoutCts.Token);
                }

                Console.WriteLine($"[gRPC] 手機新網路連線成功！ [{_channel.State}]");
                isConnected = true;
                onReconnected.OnNext(Unit.Default);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[gRPC] 當前網路仍不穩定，重連失敗: {ex.Message}");
                
                if (_reconnectCount > 5)
                {
                    handleCriticalNetworkError.OnNext(ex);
                    return;
                }
            }
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnect()
    {
        return _channel != null && _channel.State == ConnectivityState.Ready;
    }
}