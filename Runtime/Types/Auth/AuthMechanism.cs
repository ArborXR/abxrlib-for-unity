using System;

namespace AbxrLib.Runtime.Types
{
    [Serializable]
    public class AuthMechanism
    {
        public string type;
        public string prompt;
        public string domain;
        public string inputSource = "user";
        public bool? allowGuest;
    }
}
