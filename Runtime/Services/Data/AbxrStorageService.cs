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
        private readonly IAuthSessionProvider _authSession;
        private readonly AbxrRestService _restService;

        internal AbxrStorageService(IAuthSessionProvider authSession, AbxrRestService restService)
        {
            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
            _restService = restService ?? throw new ArgumentNullException(nameof(restService));
        }

        // AbxrRestService manages its own send schedule via its internal tick coroutine.
        public void Start() { }
        public void Stop() { }

        public void ForceSend() => _restService.ForceSend();

        public void ClearAllPending() => _restService.ClearAllPending();

        public void Add(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
        {
            if (!_authSession.Authenticated) return;
            if (scope == Abxr.StorageScope.User && _authSession.ResponseData?.UserId == null) return;
            _restService.StorageAdd(name ?? "", entry ?? new Dictionary<string, string>(), scope, policy);
        }

        public IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
        {
            if (!_authSession.Authenticated) { callback?.Invoke(null); yield break; }
            yield return _restService.StorageGetCoroutine(name ?? "", scope, callback);
        }

        public IEnumerator Delete(Abxr.StorageScope scope, string name = "")
        {
            if (!_authSession.Authenticated) yield break;
            yield return _restService.StorageDeleteCoroutine(scope, name ?? "", _ => { });
        }
    }
}
