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
    /// The redeem wire contract (RFC §07, plus INS-511's device name): the request, and how each response maps to an
    /// outcome a person can act on. Plain C#, so EditMode tests cover every row.
    /// </summary>
    internal static class PairingOutcomes
    {
        internal const string RedeemPath = "api/insights-pairing/redeem";
        internal const int PasscodeLength = 6;
        /// <summary>The Portal's limit, counted in code points like Laravel's max rule.</summary>
        internal const int MaxDeviceNameLength = 64;
        /// <summary>The Portal's failure budget is per minute, so a 429 without a usable Retry-After waits a minute.</summary>
        internal const int DefaultRetryAfterSeconds = 60;
        internal const int MaxRetryAfterSeconds = 3600;

        // The error codes that tell the name failures apart from a 409 or 422 about the build (INS-511).
        internal const string DeviceNameRequiredCode = "device_name_required";
        internal const string DeviceNameInvalidCode = "device_name_invalid";
        internal const string DeviceNameExistsCode = "device_name_exists";

        // Default copy (RFC D13). Product owns the wording; ISVs replace it by branching on the error.
        internal const string InvalidPasscodeMessage = "That passcode wasn't recognized. Check it and try again.";
        internal const string DeviceNameRequiredMessage = "Your organization requires a name for this headset.";
        internal const string DeviceNameInvalidMessage = "That name can't be used. Use 1 to 64 characters.";
        internal const string BuildRejectedMessage = "This app can't pair right now. Contact the app's developer.";
        internal const string UnavailableMessage = "Can't reach the pairing service. Check the connection and try again.";

        private sealed class RedeemRequest
        {
            [JsonProperty("app_token")] public string AppToken;
            [JsonProperty("passcode")] public string Passcode;
            [JsonProperty("device_name", NullValueHandling = NullValueHandling.Ignore)] public string DeviceName;
            [JsonProperty("join_existing", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool JoinExisting;
            [JsonProperty("device_metadata", NullValueHandling = NullValueHandling.Ignore)] public PairingDeviceMetadata DeviceMetadata;
        }

        private sealed class RedeemResponse
        {
            [JsonProperty("app_instance_token")] public string AppInstanceToken;
            [JsonProperty("app_instance_id")] public string AppInstanceId;
            [JsonProperty("device_name")] public string DeviceName;
        }

        private sealed class ErrorResponse
        {
            [JsonProperty("error")] public string Error;
            [JsonProperty("code")] public string Code;
            [JsonProperty("device_name")] public string DeviceName;
        }

        /// <summary>Keeps a path prefix on pairingUrl, unlike Uri's relative resolution, which drops the last segment.</summary>
        internal static string RedeemUrl(string pairingUrl) => pairingUrl.TrimEnd('/') + "/" + RedeemPath;

        /// <summary>No device_name means the name was skipped. join_existing goes only on the resend after a confirm.</summary>
        internal static string RedeemBody(string appToken, string passcode, PairingDeviceMetadata metadata, string deviceName = null, bool joinExisting = false) =>
            JsonConvert.SerializeObject(new RedeemRequest
            {
                AppToken = appToken, Passcode = passcode, DeviceName = deviceName, JoinExisting = joinExisting, DeviceMetadata = metadata
            });

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

        /// <summary>Trims the name. Null when nothing is left, which means the name was skipped.</summary>
        internal static string NormalizeDeviceName(string input)
        {
            string name = input?.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        /// <summary>1 to 64 code points with no control characters. Checked before sending, so a bad name costs no round trip.</summary>
        internal static bool IsWellFormedDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int length = 0;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsControl(name[i])) return false;
                if (char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1])) i++;
                length++;
            }
            return length <= MaxDeviceNameLength;
        }

        /// <summary>
        /// Maps a redeem response to its outcome, and on success hands back the credential. 400 is every kind of
        /// miss (wrong, expired, revoked, un-shared): the Portal answers them all alike so a guesser learns nothing.
        /// The name failures come only after a passcode matches, or before it's looked up, so they reveal nothing either.
        /// requestedDeviceName names the device in the confirm when a 409 doesn't.
        /// </summary>
        internal static Abxr.PairingRedeemResult Classify(PairingHttpResponse response, DateTime nowUtc, out string token, out string instanceId,
            string requestedDeviceName = null)
        {
            token = null;
            instanceId = null;
            long status = response.StatusCode;

            if (response.NetworkError || status == 0) return Failure(Abxr.PairingRedeemError.Unavailable);
            if (status >= 200 && status < 300)
            {
                // A 2xx without a usable credential may still have created an instance; the person decides whether to retry.
                return TryReadCredential(response.Body, out token, out instanceId, out string deviceName)
                    ? new Abxr.PairingRedeemResult(true, Abxr.PairingRedeemError.None, 0, null, deviceName)
                    : Failure(Abxr.PairingRedeemError.Unavailable);
            }
            if (status == 400) return Failure(Abxr.PairingRedeemError.InvalidPasscode);
            if (status == 429) return RateLimited(ParseRetryAfter(response.RetryAfter, nowUtc));
            if (status == 408 || status >= 500) return Failure(Abxr.PairingRedeemError.Unavailable);
            if (status == 409 || status == 422)
            {
                ErrorResponse error = ReadError(response.Body);
                switch (error?.Code)
                {
                    case DeviceNameRequiredCode: return Failure(Abxr.PairingRedeemError.DeviceNameRequired);
                    case DeviceNameInvalidCode: return Failure(Abxr.PairingRedeemError.DeviceNameInvalid);
                    case DeviceNameExistsCode: return DeviceNameExists(NormalizeDeviceName(error.DeviceName) ?? NormalizeDeviceName(requestedDeviceName));
                }
            }
            // 401, and a 409 or 422 without a name code, per the contract. Anything else, like a 404 from a wrong
            // pairingUrl, is a build problem too.
            return Failure(Abxr.PairingRedeemError.BuildRejected);
        }

        internal static Abxr.PairingRedeemResult Failure(Abxr.PairingRedeemError error) => error switch
        {
            Abxr.PairingRedeemError.InvalidPasscode => new Abxr.PairingRedeemResult(false, error, 0, InvalidPasscodeMessage),
            Abxr.PairingRedeemError.DeviceNameRequired => new Abxr.PairingRedeemResult(false, error, 0, DeviceNameRequiredMessage),
            Abxr.PairingRedeemError.DeviceNameInvalid => new Abxr.PairingRedeemResult(false, error, 0, DeviceNameInvalidMessage),
            Abxr.PairingRedeemError.BuildRejected => new Abxr.PairingRedeemResult(false, error, 0, BuildRejectedMessage),
            Abxr.PairingRedeemError.Unavailable => new Abxr.PairingRedeemResult(false, error, 0, UnavailableMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Use RateLimited, DeviceNameExists, or Refused.")
        };

        /// <summary>The name belongs to a paired device already. The message is the confirm's question.</summary>
        internal static Abxr.PairingRedeemResult DeviceNameExists(string deviceName) =>
            new Abxr.PairingRedeemResult(false, Abxr.PairingRedeemError.DeviceNameExists, 0,
                $"{deviceName ?? "That name"} is already in your organization. Add this app to it?", deviceName);

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
        internal static string ServerError(string body) => ReadError(body)?.Error;

        private static ErrorResponse ReadError(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try { return JsonConvert.DeserializeObject<ErrorResponse>(body); }
            catch (JsonException) { return null; }
        }

        /// <summary>The name is the one the Portal stored, which may differ in case from the one sent. Null when skipped.</summary>
        private static bool TryReadCredential(string body, out string token, out string instanceId, out string deviceName)
        {
            token = null;
            instanceId = null;
            deviceName = null;
            if (string.IsNullOrEmpty(body)) return false;
            RedeemResponse parsed;
            try { parsed = JsonConvert.DeserializeObject<RedeemResponse>(body); }
            catch (JsonException) { return false; }
            if (string.IsNullOrWhiteSpace(parsed?.AppInstanceToken) || string.IsNullOrWhiteSpace(parsed.AppInstanceId)) return false;

            token = parsed.AppInstanceToken.Trim();
            instanceId = parsed.AppInstanceId.Trim();
            deviceName = NormalizeDeviceName(parsed.DeviceName);
            return true;
        }

        private static int Clamp(int seconds) => Math.Min(Math.Max(seconds, 1), MaxRetryAfterSeconds);
    }
}
