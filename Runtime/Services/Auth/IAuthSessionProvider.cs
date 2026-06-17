using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>Minimal authenticated-session view needed by services that send signed REST requests.</summary>
    internal interface IAuthSessionProvider
    {
        bool Authenticated { get; }
        AuthResponse ResponseData { get; }
    }
}
