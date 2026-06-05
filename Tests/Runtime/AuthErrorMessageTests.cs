using AbxrLib.Runtime.Services.Auth;
using NUnit.Framework;

namespace AbxrLib.Tests.Runtime
{
    [TestFixture]
    public class AuthResponseParserTests
    {
        [Test]
        public void TryParseSuccess_Returns_Parsed_Response_For_Token_And_Secret()
        {
            const string body = "{\"token\":\"jwt-token\",\"secret\":\"api-secret\",\"userId\":\"learner-1\",\"userData\":{\"cohort\":\"alpha\"}}";

            Assert.IsTrue(AuthResponseParser.TryParseSuccess(body, out var response, out string errorMessage));

            Assert.IsNull(errorMessage);
            Assert.IsNotNull(response);
            Assert.AreEqual("jwt-token", response.Token);
            Assert.AreEqual("api-secret", response.Secret);
            Assert.AreEqual("learner-1", response.UserId?.ToString());
            Assert.AreEqual("alpha", response.UserData["cohort"]);
        }

        [Test]
        public void TryParseSuccess_Rejects_AppId_Only_Response()
        {
            Assert.IsFalse(AuthResponseParser.TryParseSuccess(
                "{\"appId\":\"00000000-0000-0000-0000-000000000001\"}",
                out var response,
                out string errorMessage));

            Assert.IsNull(response);
            Assert.That(errorMessage, Does.Contain("token or secret"));
        }

        [Test]
        public void TryParseSuccess_Returns_False_For_Invalid_Json()
        {
            Assert.IsFalse(AuthResponseParser.TryParseSuccess(
                "not json",
                out var response,
                out string errorMessage));

            Assert.IsNull(response);
            Assert.That(errorMessage, Does.Contain("could not be parsed"));
            Assert.IsTrue(AuthResponseParser.IsParseFailure(errorMessage));
        }

        [Test]
        public void IsParseFailure_Returns_False_For_Valid_Json_With_Invalid_Success_Shape()
        {
            Assert.IsFalse(AuthResponseParser.TryParseSuccess(
                "{\"appId\":\"00000000-0000-0000-0000-000000000001\"}",
                out _,
                out string errorMessage));

            Assert.IsFalse(AuthResponseParser.IsParseFailure(errorMessage));
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_Backend_Message_Field()
        {
            Assert.IsTrue(AuthResponseParser.TryExtractAuthErrorMessage(
                "{\"message\":\"Invalid or missing assessment pin.\"}",
                out string message));

            Assert.AreEqual("Invalid or missing assessment pin.", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_FastApi_Detail_String()
        {
            Assert.IsTrue(AuthResponseParser.TryExtractAuthErrorMessage(
                "{\"detail\":\"Invalid app token\"}",
                out string message));

            Assert.AreEqual("Invalid app token", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_Error_Field()
        {
            Assert.IsTrue(AuthResponseParser.TryExtractAuthErrorMessage(
                "{\"error\":\"Unauthorized\"}",
                out string message));

            Assert.AreEqual("Unauthorized", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Reads_FastApi_Validation_Array_Msg()
        {
            Assert.IsTrue(AuthResponseParser.TryExtractAuthErrorMessage(
                "{\"detail\":[{\"loc\":[\"body\",\"appId\"],\"msg\":\"Value error, badly formed hexadecimal UUID string\",\"type\":\"value_error\"}]}",
                out string message));

            Assert.AreEqual("Value error, badly formed hexadecimal UUID string", message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Does_Not_Return_Raw_Json_Object_Without_Error_Field()
        {
            Assert.IsFalse(AuthResponseParser.TryExtractAuthErrorMessage(
                "{\"appId\":\"00000000-0000-0000-0000-000000000001\"}",
                out string message));

            Assert.IsNull(message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Does_Not_Truncate_Plain_Text_Fallback()
        {
            string longMessage = new string('x', 250);

            Assert.IsTrue(AuthResponseParser.TryExtractAuthErrorMessage(
                longMessage,
                out string message));

            Assert.AreEqual(longMessage, message);
        }

        [Test]
        public void TryExtractAuthErrorMessage_Can_Suppress_Plain_Text_Fallback()
        {
            Assert.IsFalse(AuthResponseParser.TryExtractAuthErrorMessage(
                "temporary gateway failure",
                out string message,
                includePlainTextFallback: false));

            Assert.IsNull(message);
        }

        [Test]
        public void HasExplicitBackendError_Ignores_Plain_Text_Fallback()
        {
            Assert.IsFalse(AuthResponseParser.HasExplicitBackendError("temporary gateway failure"));
            Assert.IsTrue(AuthResponseParser.HasExplicitBackendError("{\"detail\":\"server exploded\"}"));
        }

        [Test]
        public void DescribeFailure_Uses_Backend_Error_Before_Http_Status()
        {
            string message = AuthResponseParser.DescribeFailure(
                "{\"detail\":\"server exploded\"}",
                500);

            Assert.AreEqual("server exploded", message);
        }

        [Test]
        public void DescribeFailure_Reports_Invalid_Response_For_2xx_With_Invalid_Shape()
        {
            Assert.AreEqual(
                "Authentication request returned an invalid response.",
                AuthResponseParser.DescribeFailure("{\"Token\":\"jwt-token\"}", 200));
        }
    }
}
