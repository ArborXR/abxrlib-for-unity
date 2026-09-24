using System;
using System.Globalization;
using System.Text;
using AbxrLib.Runtime.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace AbxrLib.Runtime.Services.Pairing
{
    /// <summary>
    /// Sent with every redeem (RFC D9). The Portal accepts it and stores nothing yet; it rides the request now so
    /// adding it later doesn't cost an SDK release. Deliberately no Android ID: it's per-app, so not a device identity.
    /// </summary>
    internal sealed class PairingDeviceMetadata
    {
        [JsonProperty("model")] public string Model = "";
        [JsonProperty("manufacturer")] public string Manufacturer = "";
        [JsonProperty("osVersion")] public string OsVersion = "";
        [JsonProperty("appVersion")] public string AppVersion = "";
        [JsonProperty("sdkVersion")] public string SdkVersion = "";

        /// <summary>This device's values. Main thread only: it reads SystemInfo and Application.</summary>
        internal static PairingDeviceMetadata Current() => new PairingDeviceMetadata
        {
            Model = DeviceModel.deviceModel ?? "",
            Manufacturer = DeviceModel.manufacturer ?? "",
            OsVersion = SystemInfo.operatingSystem ?? "",
            AppVersion = Application.version ?? "",
            SdkVersion = AbxrLibVersion.Version
        };
    }

    /// <summary>
    /// The redeem wire contract (RFC §07): the request, and how each response maps to one of the four outcomes a
    /// person can act on. Plain C#, so EditMode tests cover every row.
    /// </summary>
    internal static class PairingOutcomes
    {
        internal const string RedeemPath = "api/insights-pairing/redeem";
        internal const int PasscodeLength = 6;
        /// <summary>The Portal's failure budget is per minute, so a 429 without a usable Retry-After waits a minute.</summary>
        internal const int DefaultRetryAfterSeconds = 60;
        internal const int MaxRetryAfterSeconds = 3600;

        // Default copy (RFC D13). Product owns the wording; ISVs replace it by branching on the error.
        internal const string InvalidPasscodeMessage = "That passcode wasn't recognized. Check it and try again.";
        internal const string BuildRejectedMessage = "This app can't pair right now. Contact the app's developer.";
        internal const string UnavailableMessage = "Can't reach the pairing service. Check the connection and try again.";

        private sealed class RedeemRequest
        {
            [JsonProperty("app_token")] public string AppToken;
            [JsonProperty("passcode")] public string Passcode;
            [JsonProperty("device_metadata", NullValueHandling = NullValueHandling.Ignore)] public PairingDeviceMetadata DeviceMetadata;
        }

        private sealed class RedeemResponse
        {
            [JsonProperty("app_instance_token")] public string AppInstanceToken;
            [JsonProperty("app_instance_id")] public string AppInstanceId;
        }

        private sealed class ErrorResponse
        {
            [JsonProperty("error")] public string Error;
        }

        /// <summary>Keeps a path prefix on pairingUrl, unlike Uri's relative resolution, which drops the last segment.</summary>
        internal static string RedeemUrl(string pairingUrl) => pairingUrl.TrimEnd('/') + "/" + RedeemPath;

        internal static string RedeemBody(string appToken, string passcode, PairingDeviceMetadata metadata) =>
            JsonConvert.SerializeObject(new RedeemRequest { AppToken = appToken, Passcode = passcode, DeviceMetadata = metadata });

        /// <summary>Drops whitespace and hyphens, so "483 921" and "483-921" from a custom keyboard still pair.</summary>
        internal static string NormalizePasscode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var normalized = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (!char.IsWhiteSpace(c) && c != '-') normalized.Append(c);
            }
            return normalized.ToString();
        }

        /// <summary>Six ASCII digits. Checked before sending, so a typo never spends the app's failure budget.</summary>
        internal static bool IsWellFormedPasscode(string passcode)
        {
            if (passcode == null || passcode.Length != PasscodeLength) return false;
            foreach (char c in passcode)
            {
                // Not char.IsDigit, which also accepts other scripts' digits.
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        /// <summary>
        /// Maps a redeem response to its outcome, and on success hands back the credential. 400 is every kind of
        /// miss (wrong, expired, revoked, un-shared): the Portal answers them all alike so a guesser learns nothing.
        /// </summary>
        internal static Abxr.PairingRedeemResult Classify(PairingHttpResponse response, DateTime nowUtc, out string token, out string instanceId)
        {
            token = null;
            instanceId = null;
            long status = response.StatusCode;

            if (response.NetworkError || status == 0) return Failure(Abxr.PairingRedeemError.Unavailable);
            if (status >= 200 && status < 300)
            {
                // A 2xx without a usable credential may still have created an instance; the person decides whether to retry.
                return TryReadCredential(response.Body, out token, out instanceId)
                    ? new Abxr.PairingRedeemResult(true, Abxr.PairingRedeemError.None, 0, null)
                    : Failure(Abxr.PairingRedeemError.Unavailable);
            }
            if (status == 400) return Failure(Abxr.PairingRedeemError.InvalidPasscode);
            if (status == 429) return RateLimited(ParseRetryAfter(response.RetryAfter, nowUtc));
            if (status == 408 || status >= 500) return Failure(Abxr.PairingRedeemError.Unavailable);
            // 401 and 422 per the contract. Anything else, like a 404 from a wrong pairingUrl, is a build problem too.
            return Failure(Abxr.PairingRedeemError.BuildRejected);
        }

        internal static Abxr.PairingRedeemResult Failure(Abxr.PairingRedeemError error) => error switch
        {
            Abxr.PairingRedeemError.InvalidPasscode => new Abxr.PairingRedeemResult(false, error, 0, InvalidPasscodeMessage),
            Abxr.PairingRedeemError.BuildRejected => new Abxr.PairingRedeemResult(false, error, 0, BuildRejectedMessage),
            Abxr.PairingRedeemError.Unavailable => new Abxr.PairingRedeemResult(false, error, 0, UnavailableMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Use RateLimited or Refused.")
        };

        internal static Abxr.PairingRedeemResult RateLimited(int retryAfterSeconds) =>
            new Abxr.PairingRedeemResult(false, Abxr.PairingRedeemError.RateLimited, retryAfterSeconds,
                retryAfterSeconds == 1 ? "Too many attempts. Try again in 1 second." : $"Too many attempts. Try again in {retryAfterSeconds} seconds.");

        /// <summary>A call the SDK didn't send. The message is for the app's developer, not the person pairing.</summary>
        internal static Abxr.PairingRedeemResult Refused(string reason) =>
            new Abxr.PairingRedeemResult(false, Abxr.PairingRedeemError.InvalidState, 0, reason);

        /// <summary>Retry-After as delta-seconds or an HTTP date, clamped to [1, 3600].</summary>
        internal static int ParseRetryAfter(string header, DateTime nowUtc)
        {
            if (!string.IsNullOrWhiteSpace(header))
            {
                string value = header.Trim();
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds))
                    return Clamp(seconds);
                if (DateTime.TryParseExact(value, "r", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime retryAt))
                    return Clamp((int)Math.Ceiling((retryAt - nowUtc).TotalSeconds));
            }
            return DefaultRetryAfterSeconds;
        }

        /// <summary>The server's error text, for logs only. The person sees the default copy.</summary>
        internal static string ServerError(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try { return JsonConvert.DeserializeObject<ErrorResponse>(body)?.Error; }
            catch (JsonException) { return null; }
        }

        private static bool TryReadCredential(string body, out string token, out string instanceId)
        {
            token = null;
            instanceId = null;
            if (string.IsNullOrEmpty(body)) return false;
            RedeemResponse parsed;
            try { parsed = JsonConvert.DeserializeObject<RedeemResponse>(body); }
            catch (JsonException) { return false; }
            if (string.IsNullOrWhiteSpace(parsed?.AppInstanceToken) || string.IsNullOrWhiteSpace(parsed.AppInstanceId)) return false;

            token = parsed.AppInstanceToken.Trim();
            instanceId = parsed.AppInstanceId.Trim();
            return true;
        }

        private static int Clamp(int seconds) => Math.Min(Math.Max(seconds, 1), MaxRetryAfterSeconds);
    }
}
