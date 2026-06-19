using System;
using System.Collections;
using System.Text;
using AbxrLib.Runtime.Core;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace AbxrLib.Runtime.Services.Auth
{
    internal interface IAuthApiClient
    {
        IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<RestAuthResult> onComplete);
        IEnumerator GetConfigCoroutine(AuthResponse authResponse, Action<bool, string> onComplete);
    }

    /// <summary>Sends authentication-only HTTP requests so the auth flow does not depend on the data/storage REST queue.</summary>
    internal sealed class AuthApiClient : IAuthApiClient
    {
        private const string AuthPath = "/v1/auth/token";
        private const string ConfigPath = "/v1/storage/config";
        private static readonly JsonSerializerSettings AuthPayloadSerializeSettings = new() { NullValueHandling = NullValueHandling.Ignore };

        private static Uri RestUri(string path) => new(new Uri(Configuration.Instance.restUrl), path);

        public IEnumerator AuthRequestCoroutine(AuthPayload payload, Action<RestAuthResult> onComplete)
        {
            string url = RestUri(AuthPath).ToString();
            string json = JsonConvert.SerializeObject(payload, AuthPayloadSerializeSettings);
            using var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Configuration.Instance.requestTimeoutSeconds;
            yield return request.SendWebRequest();

            string responseBody = request.downloadHandler?.text ?? "";
            long statusCode = request.responseCode;

            bool responseShapeIsValid = AuthResponseParser.TryParseSuccess(responseBody, out AuthResponse parsedResponse, out _);

            bool httpSuccess = request.result == UnityWebRequest.Result.Success;
            bool success = httpSuccess && responseShapeIsValid;
            bool authRejected = !success && (statusCode == 401 || statusCode == 403);
            bool retryable = !success && IsRetryableAuthFailure(request, responseBody);

            if (!success)
            {
                string detail = string.IsNullOrEmpty(responseBody)
                    ? $"HTTP {statusCode}: {request.error ?? "No response body."}"
                    : responseBody;
                Logcat.Warning($"AuthRequest failed: {detail}");
            }

            onComplete?.Invoke(new RestAuthResult
            {
                Success = success,
                Body = responseBody,
                StatusCode = statusCode,
                Retryable = retryable,
                AuthRejected = authRejected,
                Response = success ? parsedResponse : null
            });
        }

        public IEnumerator GetConfigCoroutine(AuthResponse authResponse, Action<bool, string> onComplete)
        {
            string url = RestUri(ConfigPath).ToString();
            UnityWebRequest request = null;
            try
            {
                request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Configuration.Instance.requestTimeoutSeconds;
                AuthHeaderSigner.TrySetAuthHeaders(request, authResponse);
            }
            catch (Exception ex)
            {
                Logcat.Error($"GetConfig request creation failed: {ex.Message}");
                request?.Dispose();
                onComplete?.Invoke(false, ex.Message);
                yield break;
            }

            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string err = request.result switch
                    {
                        UnityWebRequest.Result.ConnectionError => $"Connection error: {request.error}",
                        UnityWebRequest.Result.DataProcessingError => $"Data processing error: {request.error}",
                        UnityWebRequest.Result.ProtocolError => $"Protocol error ({request.responseCode}): {request.error}",
                        _ => $"Unknown error: {request.error}"
                    };
                    onComplete?.Invoke(false, err);
                    yield break;
                }
                onComplete?.Invoke(true, request.downloadHandler?.text);
            }
            finally
            {
                request?.Dispose();
            }
        }

        private static bool IsRetryableAuthFailure(UnityWebRequest request, string responseBody)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;

            long code = request.responseCode;
            if (code == 408 || code == 429) return true;
            if (code < 500 || code > 599) return false;

            // Backend-provided detail/message/error bodies on 5xx responses are intentional
            // failures; surface them instead of retrying the same rejected request.
            return !HasExplicitBackendError(responseBody);
        }

        private static bool HasExplicitBackendError(string responseBody) =>
            AuthResponseParser.HasExplicitBackendError(responseBody);
    }

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
