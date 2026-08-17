namespace AbxrLib.Runtime.Core.UI
{
    /// <summary>Which input surface to show. Mirrors the shipped keyboard prefabs.</summary>
    public enum AuthUiKind
    {
        PinPad,
        FullKeyboard
    }

    /// <summary>
    /// Implemented by the world-space UI and registered with <see cref="AbxrUi"/> at load. Every method is
    /// called from the main thread during authentication.
    /// </summary>
    public interface IAbxrAuthUi
    {
        /// <summary>Shows the given input surface, creating it if it is not already up.</summary>
        void Show(AuthUiKind kind);

        /// <summary>Sets the prompt text on whatever is currently shown.</summary>
        void SetPrompt(string prompt);

        /// <summary>Tears the UI down after authentication finishes.</summary>
        void Hide();

        /// <summary>Ends the "processing" animation so an error message is readable.</summary>
        void StopProcessing();

        /// <summary>Brings the PIN pad back up after a failed attempt.</summary>
        void ShowPinPad();
    }
}
