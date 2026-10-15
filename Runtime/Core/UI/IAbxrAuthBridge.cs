namespace AbxrLib.Runtime.Core.UI
{
    // Internal on purpose: this is the shipped sample's route into authentication (it has InternalsVisibleTo).
    // Like the public route - Abxr.OnInputSubmitted - it only accepts input while a sign-in request is pending.
    // An app supplying its own UI submits through that public route.
    internal interface IAbxrAuthBridge
    {
        /// <summary>Records how the user supplied the value ("user" for typed input, "QRlms" for a scan).</summary>
        void SetInputSource(string source);

        /// <summary>Submits what the user entered for authentication.</summary>
        void SubmitAuthInput(string input);
    }
}
