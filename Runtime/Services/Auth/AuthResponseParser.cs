using System;
using AbxrLib.Runtime.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbxrLib.Runtime.Services.Auth
{
    /// <summary>Pure helpers for parsing auth response bodies and extracting user-facing auth errors</summary>
    internal static class AuthResponseParser
    {
        /// <summary>
        /// Parses a successful auth response. A success response must deserialize and contain both token and secret.
        /// </summary>
        internal static bool TryParseSuccess(string responseBody, out AuthResponse response) =>
            TryParseSuccess(responseBody, out response, out _);

        /// <summary>
        /// Parses a successful auth response and returns a diagnostic reason when the success shape is invalid.
        /// </summary>
        internal static bool TryParseSuccess(string responseBody, out AuthResponse response, out string parseError)
        {
            response = null;
            parseError = null;

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                parseError = "Authentication response body was empty.";
                return false;
            }

            AuthResponse parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<AuthResponse>(responseBody);
            }
            catch (Exception ex)
            {
                parseError = $"Authentication response could not be parsed: {ex.Message}";
                return false;
            }

            if (!AuthResponse.IsValidSuccess(parsed))
            {
                parseError = "Authentication response missing token or secret.";
                return false;
            }

            response = parsed;
            return true;
        }

        /// <summary>Extracts a user-facing error string from auth failure JSON or, optionally, plain text.</summary>
        internal static bool TryExtractAuthErrorMessage(string responseBody, out string message, bool includePlainTextFallback = true)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(responseBody)) return false;

            try
            {
                JToken root = JToken.Parse(responseBody);

                message = ExtractMessageToken(GetProperty(root, "message")) ??
                          ExtractMessageToken(GetProperty(root, "detail")) ??
                          ExtractMessageToken(GetProperty(root, "error"));

                if (string.IsNullOrWhiteSpace(message) && includePlainTextFallback && IsScalar(root))
                {
                    message = ExtractMessageToken(root);
                }

                message = NormalizeErrorMessage(message);
                return !string.IsNullOrEmpty(message);
            }
            catch (JsonReaderException)
            {
                if (!includePlainTextFallback) return false;

                message = NormalizeErrorMessage(responseBody);
                return !string.IsNullOrEmpty(message);
            }
            catch
            {
                message = null;
                return false;
            }
        }

        /// <summary>
        /// True when the backend returned an explicit structured auth error. Plain text is intentionally ignored
        /// so transient 5xx proxy/gateway bodies can still use the retry path.
        /// </summary>
        internal static bool HasExplicitBackendError(string responseBody) =>
            TryExtractAuthErrorMessage(responseBody, out _, includePlainTextFallback: false);

        /// <summary>True when <paramref name="parseError"/> describes malformed response JSON rather than a valid but unsuccessful auth response shape.</summary>
        internal static bool IsParseFailure(string parseError) =>
            !string.IsNullOrEmpty(parseError) &&
            parseError.StartsWith("Authentication response could not be parsed:", StringComparison.Ordinal);

        /// <summary>Builds the user-facing failure message from a response body and HTTP status code.</summary>
        internal static string DescribeFailure(string responseBody, long statusCode)
        {
            if (TryExtractAuthErrorMessage(responseBody, out string explicitError)) return explicitError;
            if (statusCode >= 200 && statusCode <= 299) return "Authentication request returned an invalid response.";
            if (statusCode > 0) return $"Authentication request failed (HTTP {statusCode}).";
            return "Authentication request failed.";
        }

        private static JToken GetProperty(JToken token, string propertyName)
        {
            if (token is JObject obj && obj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out JToken value)) return value;
            return null;
        }

        private static string ExtractMessageToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) return null;

            switch (token.Type)
            {
                case JTokenType.String:
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return token.ToString();

                case JTokenType.Object:
                    return ExtractMessageToken(GetProperty(token, "msg")) ??
                           ExtractMessageToken(GetProperty(token, "message")) ??
                           ExtractMessageToken(GetProperty(token, "detail")) ??
                           ExtractMessageToken(GetProperty(token, "error"));

                case JTokenType.Array:
                    foreach (JToken child in token.Children())
                    {
                        string childMessage = ExtractMessageToken(child);
                        if (!string.IsNullOrWhiteSpace(childMessage))
                            return childMessage;
                    }
                    return null;

                default:
                    return null;
            }
        }

        private static bool IsScalar(JToken token) =>
            token?.Type is JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean;

        private static string NormalizeErrorMessage(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
