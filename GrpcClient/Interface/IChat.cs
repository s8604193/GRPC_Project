public interface IChat
{
    Task SendChatMessage(string targetUserId, string text);

    void CreateChatStream();
}