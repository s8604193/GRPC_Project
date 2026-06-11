public readonly struct GlobalCoreContext
{
    public readonly ILogin _loginService;
    public readonly IChannel _channelService;
    public readonly IPicture _pictureService;
    public readonly IChat _chatService;
    public readonly NetworkLossInterceptorService _networkLossInterceptorService;

    public readonly UserSession _userSession;

    public GlobalCoreContext(UserSession userSession, IChannel channelService, ILogin loginService, IPicture pictureService, IChat chatService)
    {
        _userSession = userSession;
        _loginService = loginService;
        _channelService = channelService;
        _pictureService = pictureService;
        _chatService = chatService;

        _networkLossInterceptorService = new(channelService);
    }
}

public static class GeneratedCompositionRoot
{
    public static GlobalCoreContext CreateCore()
    {
        UserSession userSession = new();
        ChannelService channelService = new();
        LoginService loginService = new(channelService,userSession);
        PictureService pictureService = new(channelService);
        ChatService chatService = new(userSession, channelService,loginService);

        return new GlobalCoreContext(userSession,channelService,loginService,pictureService,chatService);
    }
}