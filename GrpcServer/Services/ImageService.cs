using Grpc.Core;
using GrpcServer.Protos; // 剛才自動生成的命名空間

namespace GrpcServer.Services
{
    // 繼承自動生成的 Base 類別
    public class ImageService : IImageService.IImageServiceBase
    {
        private readonly ILogger<ImageService> _logger;

        public ImageService(ILogger<ImageService> logger)
        {
            _logger = logger;
        }

        public override async Task DownloadProductImage(
            ImageRequest request, 
            IServerStreamWriter<ImageChunk> responseStream, // 這是用來推播的串流通道
            ServerCallContext context)
        {
            // 1. 指定本機圖片路徑（請改成你電腦中真實存在的圖片路徑）
            string imagePath = Path.Combine("C:\\Users\\leoyan\\Desktop\\EMOJI", "iSQhO.png"); 
            
            if (!File.Exists(imagePath))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "找不到該圖片"));
            }

            // 2. 開啟檔案資料流
            using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4096]; // 每次唯獨 4KB，避免佔用過多記憶體
            int bytesRead;

            // 3. 邊讀取邊發送
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                // 如果讀取到的長度小於 4KB（最後一塊），要裁切陣列
                var chunkData = bytesRead == buffer.Length ? buffer : buffer.Take(bytesRead).ToArray();

                // 透過 responseStream.WriteAsync 將這一塊二進位送出去
                await responseStream.WriteAsync(new ImageChunk
                {
                    Data = Google.Protobuf.ByteString.CopyFrom(chunkData)
                });
            }
        }
    }
}