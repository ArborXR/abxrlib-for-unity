using AbxrLib.Runtime.Services.Auth;
using NUnit.Framework;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture]
    public class AuthErrorMessageTests
    {
        [Test]
        public void TryExtractAuthErrorMessage_Reads_Backend_Message_Field()
        {
            Assert.IsTrue(AbxrAuthService.TryExtractAuthErrorMessage(
                "{\"message\":\"Invalid or missing assessment pin.\"}",
                out string message));

            Assert.AreEqual("Invalid or missing assessment pin.", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_FastApi_Detail_String()
        {
            Assert.IsTrue(AbxrAuthService.TryExtractAuthErrorMessage(
                "{\"detail\":\"Invalid app token\"}",
                out string message));

            Assert.AreEqual("Invalid app token", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_Error_Field()
        {
            Assert.IsTrue(AbxrAuthService.TryExtractAuthErrorMessage(
                "{\"error\":\"Unauthorized\"}",
                out string message));

            Assert.AreEqual("Unauthorized", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_FastApi_Validation_Array_Msg()
        {
            Assert.IsTrue(AbxrAuthService.TryExtractAuthErrorMessage(
                "{\"detail\":[{\"loc\":[\"body\",\"appId\"],\"msg\":\"Value error, badly formed hexadecimal UUID string\",\"type\":\"value_error\"}]}",
                out string message));

            Assert.AreEqual("Value error, badly formed hexadecimal UUID string", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Does_Not_Return_Raw_Json_Object_Without_Error_Field()
        {
            Assert.IsFalse(AbxrAuthService.TryExtractAuthErrorMessage(
                "{\"appId\":\"00000000-0000-0000-0000-000000000001\"}",
                out string message));

            Assert.IsNull(message);
        }


        [Test]
        public void TryExtractAuthErrorMessage_Does_Not_Truncate_Plain_Text_Fallback()
        {
            string longMessage = new string('x', 250);

            Assert.IsTrue(AbxrAuthService.TryExtractAuthErrorMessage(
                longMessage,
                out string message));

            Assert.AreEqual(longMessage, message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Can_Suppress_Plain_Text_Fallback()
        {
            Assert.IsFalse(AbxrAuthService.TryExtractAuthErrorMessage(
                "temporary gateway failure",
                out string message,
                includePlainTextFallback: false));

            Assert.IsNull(message);
        }
    }
}
