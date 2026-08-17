namespace AbxrLib.Runtime.Core.UI
{
    public interface IAbxrAuthBridge
    {
        /// <summary>Records how the user supplied the value ("user" for typed input, "QRlms" for a scan).</summary>
        void SetInputSource(string source);

        /// <summary>Submits what the user entered for authentication.</summary>
        void SubmitAuthInput(string input);
    }
}
