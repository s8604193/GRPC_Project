using GameServer.Network.Grpc;
using R3;

public interface ILogin
{
    Observable<Exception> OnCriticalNetworkError { get; }
    Observable<Unit> OnReconnected { get; }
    Observable<Unit> OnLogOut { get; }

    Task<bool> Login();

    void CloseConnection();

    void KeepAliveStream();
}