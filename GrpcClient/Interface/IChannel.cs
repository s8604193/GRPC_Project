using Grpc.Net.Client;
using R3;

public interface IChannel
{
    Observable<Exception> OnCriticalNetworkError { get; }
    Observable<Unit> OnReconnected { get; }
    GrpcChannel GetChannel();
    Task HandleNetworkLossFlowAsync();

    bool IsConnect();
}