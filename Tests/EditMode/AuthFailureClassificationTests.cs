// Copyright (c) 2026 ArborXR. All rights reserved.
// Pins which device-auth failures latch the session as rejected and which retry. Every REST failure used to latch:
// the transport never passed an empty body, and any non-empty body counted as an explicit error, so a headset that
// was offline at launch sent no data for the whole session. REST rows run through the transport's real mapping.
using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Transport;
using NUnit.Framework;
using UnityEngine.Networking;

[TestFixture]
public class AuthFailureClassificationTests
{
    private const string JsonError = "{\"detail\":\"Invalid app token\"}";

    private static bool RestLatches(UnityWebRequest.Result requestResult, int status, string body)
        => AbxrAuthService.IsCredentialRejection(AbxrTransportRest.ToAuthResult(requestResult, status, body));

    private static bool ServiceLatches(string body, bool getLastAuthRejected = false)
        => AbxrAuthService.IsCredentialRejection(new AuthTransportResult(false, body, getLastAuthRejected));

    // ── REST ──────────────────────────────────────────────────────────────

    [TestCase(401, "")]
    [TestCase(403, "")]
    [TestCase(401, "<html>Unauthorized</html>")]
    [TestCase(400, JsonError)]
    [TestCase(404, "{\"message\":\"App not found\"}")]
    [TestCase(422, "{\"detail\":[{\"loc\":[\"body\",\"app_token\"],\"msg\":\"field required\"}]}")]
    public void Rest_Refusal_Latches(int status, string body)
        => Assert.IsTrue(RestLatches(UnityWebRequest.Result.ProtocolError, status, body), $"{status} {body}");

    [TestCase(408, JsonError)]
    [TestCase(429, JsonError)]
    [TestCase(500, JsonError)]
    [TestCase(502, "<html>Bad Gateway</html>")]
    [TestCase(503, "")]
    [TestCase(400, "")]
    [TestCase(404, "<html>Not Found</html>")]
    public void Rest_TransientStatus_Retries(int status, string body)
        => Assert.IsFalse(RestLatches(UnityWebRequest.Result.ProtocolError, status, body), $"{status} {body}");

    [Test]
    public void Rest_ConnectionError_Retries()
    {
        // Offline, DNS, refused connection, timeout, and TLS failures all arrive as ConnectionError with no status.
        var result = AbxrTransportRest.ToAuthResult(UnityWebRequest.Result.ConnectionError, 0, "");

        Assert.IsTrue(result.NetworkError);
        Assert.AreEqual(0, result.StatusCode);
        Assert.IsEmpty(result.Body, "The transport passes an empty body through; a placeholder would read as an API error.");
        Assert.IsFalse(AbxrAuthService.IsCredentialRejection(result));
    }

    [Test]
    public void Rest_NullBody_PassesThroughAndRetries()
    {
        var result = AbxrTransportRest.ToAuthResult(UnityWebRequest.Result.ConnectionError, 0, null);

        Assert.IsNull(result.Body);
        Assert.IsFalse(AbxrAuthService.IsCredentialRejection(result));
    }

    [Test]
    public void Rest_CaptivePortalPage_Retries()
        => Assert.IsFalse(RestLatches(UnityWebRequest.Result.Success, 200, "<html>Sign in to Wi-Fi</html>"));

    // ── ArborInsightsClient (no HTTP status; getLastAuthRejected() is its verdict) ──

    [Test]
    public void Service_GetLastAuthRejected_Latches()
        => Assert.IsTrue(ServiceLatches("{\"result\":0}", getLastAuthRejected: true));

    [Test]
    public void Service_ExplicitJsonError_Latches()
        => Assert.IsTrue(ServiceLatches(JsonError));

    [TestCase("ArborInsightsClient.Bind failed")]
    [TestCase("ArborInsightsClient service not ready after bind")]
    [TestCase("{\"result\":0}")]
    [TestCase("")]
    public void Service_FailureWithoutApiError_Retries(string body)
        => Assert.IsFalse(ServiceLatches(body), body);

    [Test]
    public void Service_NullBody_Retries()
        => Assert.IsFalse(ServiceLatches(null));
}
