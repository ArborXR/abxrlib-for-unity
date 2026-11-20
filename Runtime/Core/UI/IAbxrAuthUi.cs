namespace AbxrLib.Runtime.Core.UI
{
    /// <summary>Which input surface to show. Mirrors the shipped keyboard prefabs.</summary>
    public enum AuthUiKind
    {
        PinPad,
        FullKeyboard,
        /// <summary>
        /// The pairing passcode on the PIN pad. Submitting "**skip**" closes the prompt; the default UI offers that as
        /// "Not now", unless enablePairingDismiss is off.
        /// </summary>
        PairingPasscode,
        /// <summary>The headset's name on the keyboard, with a skip.</summary>
        PairingDeviceName,
        /// <summary>The headset's name on the keyboard, without a skip: the passcode requires one.</summary>
        PairingDeviceNameRequired,
        /// <summary>
        /// A headset in the organization already has the name: confirm adding this app to it, or choose another name.
        /// Abxr.GetLastPairingRedeemResult().DeviceName holds the name; submit it to join, or "**skip**" for another.
        /// </summary>
        PairingDeviceJoin
    }

    /// <summary>
    /// Implemented by the world-space UI and registered with <see cref="AbxrUi"/> at load. Every method is
    /// called from the main thread during authentication.
    /// </summary>
    public interface IAbxrAuthUi
    {
        /// <summary>
        /// Shows the given input surface: creates it if it does not exist, and reveals it if something hid it
        /// (the shipped QR scanner hides the PIN pad while scanning). Core calls this again after a failed
        /// attempt, so it must be safe to call while the surface is already up.
        /// </summary>
        void Show(AuthUiKind kind);

        /// <summary>Sets the prompt text on whatever is currently shown.</summary>
        void SetPrompt(string prompt);

        /// <summary>Tears the UI down after authentication finishes.</summary>
        void Hide();

        /// <summary>Ends the "processing" animation so an error message is readable. No-op for a UI without one.</summary>
        void StopProcessing();
    }
}
