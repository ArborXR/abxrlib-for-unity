using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services;
using AbxrLib.Runtime.Services.Auth;

namespace AbxrLib.Runtime.Services.Data
{
    public class AbxrStorageService
    {
        private readonly AbxrAuthService _authService;
        private readonly AbxrRestService _restService;

        internal AbxrStorageService(AbxrAuthService authService, AbxrRestService restService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _restService = restService ?? throw new ArgumentNullException(nameof(restService));
        }

        // AbxrRestService manages its own send schedule via its internal tick coroutine.
        public void Start() { }
        public void Stop() { }

        public void ForceSend() => _restService.ForceSend();

        public void ClearAllPending() => _restService.ClearAllPending();

        public void Add(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
        {
            if (!_authService.Authenticated) return;
            if (scope == Abxr.StorageScope.User && _authService.ResponseData?.UserId == null) return;
            _restService.StorageAdd(name ?? "", entry ?? new Dictionary<string, string>(), scope, policy);
        }

        public IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
        {
            if (!_authService.Authenticated) { callback?.Invoke(null); yield break; }
            yield return _restService.StorageGetCoroutine(name ?? "", scope, callback);
        }

        public IEnumerator Delete(Abxr.StorageScope scope, string name = "")
        {
            if (!_authService.Authenticated) yield break;
            yield return _restService.StorageDeleteCoroutine(scope, name ?? "", _ => { });
        }
    }
}
