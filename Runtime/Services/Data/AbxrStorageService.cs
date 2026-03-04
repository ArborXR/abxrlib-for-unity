using System;
using System.Collections;
using System.Collections.Generic;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Transport;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Data
{
    public class AbxrStorageService
    {
        private readonly AbxrAuthService _authService;
        private readonly Func<IAbxrTransport> _getTransport;

        internal AbxrStorageService(AbxrAuthService authService, MonoBehaviour runner, Func<IAbxrTransport> getTransport)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _ = runner ?? throw new ArgumentNullException(nameof(runner));
            _getTransport = getTransport ?? throw new ArgumentNullException(nameof(getTransport));
        }

        // AbxrTransportRest manages its own send schedule via its internal tick coroutine.
        public void Start() { }
        public void Stop() { }

        public void ForceSend() => _getTransport()?.ForceSend();

        public void ClearAllPending() => _getTransport()?.ClearAllPending();

        public void Add(string name, Dictionary<string, string> entry, Abxr.StorageScope scope, Abxr.StoragePolicy policy)
        {
            if (!_authService.Authenticated) return;
            if (scope == Abxr.StorageScope.User && _authService.ResponseData?.UserId == null) return;
            _getTransport()?.StorageAdd(name ?? "", entry ?? new Dictionary<string, string>(), scope, policy);
        }

        public IEnumerator Get(string name, Abxr.StorageScope scope, Action<List<Dictionary<string, string>>> callback)
        {
            if (!_authService.Authenticated) { callback?.Invoke(null); yield break; }
            var transport = _getTransport();
            if (transport == null) { callback?.Invoke(null); yield break; }
            yield return transport.StorageGetCoroutine(name ?? "", scope, callback);
        }

        public IEnumerator Delete(Abxr.StorageScope scope, string name = "")
        {
            if (!_authService.Authenticated) yield break;
            var transport = _getTransport();
            if (transport == null) yield break;
            yield return transport.StorageDeleteCoroutine(scope, name ?? "", _ => { });
        }
    }
}
