var _GlobalCoreContext = GeneratedCompositionRoot.CreateCore();

var loginService = _GlobalCoreContext._loginService;
try
{
    var loginResponse = await loginService.Login(); 

    if(loginResponse.Success)
    {
        loginService.KeepAliveStream();

        Console.WriteLine($"\n登入成功 {loginResponse.Message}");

    }
    else
    {
        Console.WriteLine($"\n登入失敗 {loginResponse.Message}");
    }
}
catch(Exception ex)
{
    Console.WriteLine($"\n登入失敗 {ex.Message}");
}

while (true)
{
    string? input = Console.ReadLine()?.Trim().ToLower();

    if (input == "image")
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
    else if (input == "exit")
    {
        break;
    }
}
