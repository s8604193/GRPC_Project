using Grpc.Core;
using Grpc.Net.Client;
using GrpcServer.Protos;

public class PictureService : IPicture
{
    private IImageService.IImageServiceClient service;
    private ImageRequest request;
    public PictureService(IChannel channelService)
    {
        service = new IImageService.IImageServiceClient(channelService.GetChannel());
        request = new ImageRequest { ProductId = "PROD-100" };
    }

    public async Task DownloadPicture()
    {
        using var streamingCall = service.DownloadProductImage(request);
        string outputPath = Path.Combine("C:\\Users\\leoyan\\Desktop\\Work", "downloaded_output.png");
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        int chunkCount = 0;
        while (await streamingCall.ResponseStream.MoveNext(CancellationToken.None))
        {
            var chunk = streamingCall.ResponseStream.Current;
            byte[] bytes = chunk.Data.ToByteArray();
            
            await fileStream.WriteAsync(bytes, 0, bytes.Length);
            chunkCount++;
        }

        Console.WriteLine($"\n🎉 圖片下載成功！已存檔至: {outputPath}");
    }
}