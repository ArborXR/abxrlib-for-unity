using System;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Services.Auth
{
    internal sealed class AuthSessionProvider : IAuthSessionProvider
    {
        private readonly AbxrAuthService _authService;

        internal AuthSessionProvider(AbxrAuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public bool Authenticated => _authService.Authenticated;
        public AuthResponse ResponseData => _authService.ResponseData;
    }
}
