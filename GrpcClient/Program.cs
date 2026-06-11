var _GlobalCoreContext = GeneratedCompositionRoot.CreateCore();

var loginService = _GlobalCoreContext._loginService;
try
{
    var loginSuccess = await loginService.Login(); 

    if(loginSuccess)
    {
        loginService.KeepAliveStream();

        Console.WriteLine($"\n登入成功");

        _GlobalCoreContext._chatService.CreateChatStream();
    }
    else
    {
        Console.WriteLine($"\n登入失敗");
    }
}
catch(Exception ex)
{
    Console.WriteLine($"\n登入失敗 {ex.Message}");
}

while (true)
{
    string? input = Console.ReadLine();
    if(input != null)
    {
        var span = input.Trim().ToLower().AsSpan();

        if (span == "image")
        {
            try
            {
                await _GlobalCoreContext._pictureService.DownloadPicture();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"\n下載失敗 {ex.Message}");
            }
        }
        else if (span == "exit")
        {
            break;
        }
        else if(span[..4].SequenceEqual("chat"))
        {
            var newMsg = span[5..];
            var spaceIndex = newMsg.IndexOf(" ");
            if(spaceIndex >= 0)
            {
                var id = newMsg[..spaceIndex];
                var msg = newMsg[spaceIndex..];
                await _GlobalCoreContext._chatService.SendChatMessage(id.ToString(), msg.ToString());
            } 
        }
    }
}
