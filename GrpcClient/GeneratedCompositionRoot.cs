public readonly struct GlobalCoreContext
{
    public readonly ILogin _loginService;
    public readonly IChannel _channelService;
    public readonly IPicture _pictureService;
    public readonly NetworkLossInterceptorService _networkLossInterceptorService;

    public GlobalCoreContext(IChannel channelService, ILogin loginService, IPicture pictureService)
    {
        _loginService = loginService;
        _channelService = channelService;
        _pictureService = pictureService;

        _networkLossInterceptorService = new(channelService);
    }
}

public static class GeneratedCompositionRoot
{
    public static GlobalCoreContext CreateCore()
    {
        ChannelService channelService = new();
        LoginService loginService = new(channelService);
        PictureService pictureService = new(channelService);

        return new GlobalCoreContext(channelService,loginService,pictureService);
    }
}