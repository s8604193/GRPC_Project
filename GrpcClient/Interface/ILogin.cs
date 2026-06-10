using GameServer.Network.Grpc;
using R3;

public interface ILogin
{
    Observable<Exception> OnCriticalNetworkError { get; }
    Observable<Unit> OnReconnected { get; }

    Task<LoginResponse> Login();

    void DisconnectAsync();

    void KeepAliveStream();
}