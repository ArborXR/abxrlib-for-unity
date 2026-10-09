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
        /// POSTs the JSON body once and reports what came back, exactly once, even when the request can't start or
        /// never gets an answer. Never retries: every successful redeem creates an app instance, so a retry after a
        /// lost response would leave an orphan in the Portal.
        /// </summary>
        void Send(string url, string json, Action<PairingHttpResponse> onComplete);
    }

    internal sealed class UnityWebRequestPairingClient : IPairingRedeemClient
    {
        /// <summary>How long past requestTimeoutSeconds the client gives up on its own, where UnityWebRequest.timeout isn't honoured.</summary>
        internal const double TimeoutGraceSeconds = 5;

        private readonly MonoBehaviour _runner;
        private readonly double? _answerDeadlineSeconds;

        /// <param name="answerDeadlineSeconds">Tests shorten the client's own deadline; otherwise it's the request timeout plus <see cref="TimeoutGraceSeconds"/>.</param>
        internal UnityWebRequestPairingClient(MonoBehaviour runner, double? answerDeadlineSeconds = null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _answerDeadlineSeconds = answerDeadlineSeconds;
        }

        public void Send(string url, string json, Action<PairingHttpResponse> onComplete)
        {
            // StartCoroutine on an inactive GameObject logs an error and runs nothing, so no answer would ever come.
            if (!_runner || !_runner.gameObject.activeInHierarchy)
            {
                onComplete?.Invoke(NoResponse("the SDK's GameObject is inactive, so the request never started."));
                return;
            }
            _runner.StartCoroutine(SendCoroutine(url, json, _answerDeadlineSeconds, onComplete));
        }

        private static IEnumerator SendCoroutine(string url, string json, double? answerDeadlineSeconds, Action<PairingHttpResponse> onComplete)
        {
            UnityWebRequest request = null;
            double waitSeconds = 0;
            string failure = null;
            try
            {
                int timeout = Configuration.Instance.requestTimeoutSeconds;
                request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                Utils.BuildRequest(request, json);
                // Laravel answers a validation failure with a 422 only when the request asks for JSON; otherwise it redirects.
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = timeout;
                request.SendWebRequest();
                waitSeconds = answerDeadlineSeconds ?? timeout + TimeoutGraceSeconds;
            }
            catch (Exception ex)
            {
                failure = $"the request couldn't be sent ({ex.Message}).";
            }
            if (failure != null)
            {
                request?.Dispose();
                onComplete?.Invoke(NoResponse(failure));
                yield break;
            }

            PairingHttpResponse response;
            using (request)
            {
                double deadline = Time.realtimeSinceStartupAsDouble + waitSeconds;
                while (!request.isDone && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;

                if (!request.isDone)
                {
                    request.Abort();
                    response = NoResponse($"no answer after {waitSeconds:0.#}s.");
                }
                else
                {
                    response = new PairingHttpResponse(
                        request.responseCode,
                        request.downloadHandler?.text,
                        request.GetResponseHeader("Retry-After"),
                        request.result == UnityWebRequest.Result.ConnectionError,
                        request.error);
                }
            }
            onComplete?.Invoke(response);
        }

        private static PairingHttpResponse NoResponse(string detail) => new PairingHttpResponse(0, null, networkError: true, errorDetail: detail);
    }
}
