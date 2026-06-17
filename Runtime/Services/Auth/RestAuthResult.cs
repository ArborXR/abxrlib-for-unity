using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    internal sealed class RestAuthResult
    {
        internal bool Success;
        internal string Body;
        internal long StatusCode;
        internal bool Retryable;
        internal bool AuthRejected;
        internal AuthResponse Response;
    }
}
