using Grpc.Core;
using Grpc.Net.Client;
using GrpcServer.Protos;

using var channel = GrpcChannel.ForAddress("http://localhost:5278");
var client = new StockManager.StockManagerClient(channel);

Console.WriteLine("=== 開始下載本機圖片 (gRPC Stream) ===");

// 1. 發送下載請求
var request = new ImageRequest { ProductId = "PROD-100" };
using var streamingCall = client.DownloadProductImage(request);

// 2. 準備在 Client 端存檔的路徑
string outputPath = Path.Combine("C:\\Users\\leoyan\\Desktop\\Work", "downloaded_output.png");
using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

try
{
    int chunkCount = 0;
    // 3. 用 MoveNext() 持續等待並接收伺服器傳過來的下一塊數據
    while (await streamingCall.ResponseStream.MoveNext(CancellationToken.None))
    {
        var chunk = streamingCall.ResponseStream.Current;
        byte[] bytes = chunk.Data.ToByteArray();
        
        // 寫入檔案
        await fileStream.WriteAsync(bytes, 0, bytes.Length);
        chunkCount++;
        Console.WriteLine($"已接收第 {chunkCount} 個數據塊 ({bytes.Length} bytes)");
    }

    Console.WriteLine($"\n🎉 圖片下載成功！已存檔至: {outputPath}");
}
catch (RpcException ex)
{
    Console.WriteLine($"下載失敗: {ex.Status.Detail}");
}