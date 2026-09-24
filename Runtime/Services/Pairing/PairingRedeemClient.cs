using System;
using System.Collections;
using AbxrLib.Runtime.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.Pairing
{
    /// <summary>What came back from one redeem request. StatusCode is 0 when no response arrived.</summary>
    internal readonly struct PairingHttpResponse
    {
        internal readonly long StatusCode;
        internal readonly string Body;
        internal readonly string RetryAfter;
        /// <summary>No HTTP response: offline, DNS, TLS, a timeout, or on WebGL a CORS failure.</summary>
        internal readonly bool NetworkError;
        /// <summary>The transport's own description of a failure, for logs only.</summary>
        internal readonly string ErrorDetail;

        internal PairingHttpResponse(long statusCode, string body, string retryAfter = null, bool networkError = false, string errorDetail = null)
        {
            StatusCode = statusCode;
            Body = body;
            RetryAfter = retryAfter;
            NetworkError = networkError;
            ErrorDetail = errorDetail;
        }
    }

    /// <summary>Sends the redeem request. Tests replace it so no request leaves the Editor.</summary>
    internal interface IPairingRedeemClient
    {
        /// <summary>
        /// POSTs the JSON body once and reports what came back. Never retries: every successful redeem creates an
        /// app instance, so a retry after a lost response would leave an orphan in the Portal.
        /// </summary>
        void Send(string url, string json, Action<PairingHttpResponse> onComplete);
    }

    internal sealed class UnityWebRequestPairingClient : IPairingRedeemClient
    {
        private readonly MonoBehaviour _runner;

        internal UnityWebRequestPairingClient(MonoBehaviour runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public void Send(string url, string json, Action<PairingHttpResponse> onComplete) =>
            _runner.StartCoroutine(SendCoroutine(url, json, onComplete));

        private static IEnumerator SendCoroutine(string url, string json, Action<PairingHttpResponse> onComplete)
        {
            PairingHttpResponse response;
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                Utils.BuildRequest(request, json);
                // Laravel answers a validation failure with a 422 only when the request asks for JSON; otherwise it redirects.
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                yield return request.SendWebRequest();

                response = new PairingHttpResponse(
                    request.responseCode,
                    request.downloadHandler?.text,
                    request.GetResponseHeader("Retry-After"),
                    request.result == UnityWebRequest.Result.ConnectionError,
                    request.error);
            }
            onComplete?.Invoke(response);
        }
    }
}
