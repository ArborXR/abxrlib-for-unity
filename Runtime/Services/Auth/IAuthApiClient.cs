using System;
using System.Collections;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    internal interface IAuthApiClient
    {
        IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<RestAuthResult> onComplete);
        IEnumerator GetConfigCoroutine(AuthResponse authResponse, Action<bool, string> onComplete);
    }
}
