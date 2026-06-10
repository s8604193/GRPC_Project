using Grpc.Core;

public static class GrpcNetworkExtensions
{
    /// <summary>
    /// 檢查這個異常是不是因為「實體網路斷開」或「超時」所引起的
    /// </summary>
    public static bool IsNetworkLoss(this Exception ex)
    {
        if (ex is RpcException rpcEx)
        {
            return rpcEx.StatusCode == StatusCode.Unavailable || 
                   rpcEx.StatusCode == StatusCode.DeadlineExceeded ||
                   rpcEx.StatusCode == StatusCode.Cancelled;
        }
        
        return ex is System.Net.Sockets.SocketException || ex is System.IO.IOException;
    }
}