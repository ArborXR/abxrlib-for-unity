namespace AbxrLib.Runtime.Core.UI
{
    // Internal on purpose: this is the shipped sample's route into authentication (it has InternalsVisibleTo),
    // and it skips the pending-request guard that the public route - Abxr.OnInputSubmitted - enforces. An app
    // supplying its own UI submits through that public route.
    internal interface IAbxrAuthBridge
    {
        /// <summary>Records how the user supplied the value ("user" for typed input, "QRlms" for a scan).</summary>
        void SetInputSource(string source);

        /// <summary>Submits what the user entered for authentication.</summary>
        void SubmitAuthInput(string input);
    }
}
