using System.Runtime.CompilerServices;

public class UserSession
{
    public string Token { get; set; } = string.Empty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLoggedIn()
    {
        return !string.IsNullOrEmpty(Token);
    }

    public void Clear()
    {
        Token = string.Empty;
    }
}