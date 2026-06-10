using Grpc.Core;
using Grpc.Core.Interceptors;

public class NetworkLossInterceptorService : Interceptor
{
    private readonly IChannel _channelService;

    public NetworkLossInterceptorService(IChannel channelService)
    {
        _channelService = channelService;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);

        var responseTask = call.ResponseAsync.ContinueWith(task =>
        {
            if (task.IsFaulted && task.Exception != null)
            {
                var baseException = task.Exception.GetBaseException();
                
                if (baseException.IsNetworkLoss())
                {
                    Console.WriteLine($"[Interceptor] 攔截到 API 斷線: {context.Method.Name}");
                    _ = _channelService.HandleNetworkLossFlowAsync();
                }
                throw task.Exception.InnerException ?? task.Exception;
            }
            return task.Result;
        });

        return new AsyncUnaryCall<TResponse>(
            responseTask, 
            call.ResponseHeadersAsync, 
            call.GetStatus, 
            call.GetTrailers, 
            call.Dispose);
    }
}