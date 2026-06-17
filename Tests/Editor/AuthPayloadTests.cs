using System.Collections.Generic;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    [TestFixture]
    public class AuthPayloadTests
    {
        [Test]
        public void CopyForRequest_CopiesScalarFields()
        {
            var payload = new AuthPayload
            {
                appId = "app-id",
                orgId = "org-id",
                authSecret = "auth-secret",
                appToken = "app-token",
                orgToken = "org-token",
                deviceId = "device-id",
                userId = "user-id",
                sessionId = "session-id",
                partner = "partner",
                ipAddress = "ip-address",
                deviceModel = "device-model",
                osVersion = "os-version",
                xrdmVersion = "xrdm-version",
                appVersion = "app-version",
                unityVersion = "unity-version",
                abxrLibType = "abxr-lib-type",
                abxrLibVersion = "abxr-lib-version",
                buildFingerprint = "build-fingerprint"
            };

            AuthPayload copy = payload.CopyForRequest();

            Assert.AreNotSame(payload, copy);
            Assert.AreEqual(payload.appId, copy.appId);
            Assert.AreEqual(payload.orgId, copy.orgId);
            Assert.AreEqual(payload.authSecret, copy.authSecret);
            Assert.AreEqual(payload.appToken, copy.appToken);
            Assert.AreEqual(payload.orgToken, copy.orgToken);
            Assert.AreEqual(payload.deviceId, copy.deviceId);
            Assert.AreEqual(payload.userId, copy.userId);
            Assert.AreEqual(payload.sessionId, copy.sessionId);
            Assert.AreEqual(payload.partner, copy.partner);
            Assert.AreEqual(payload.ipAddress, copy.ipAddress);
            Assert.AreEqual(payload.deviceModel, copy.deviceModel);
            Assert.AreEqual(payload.osVersion, copy.osVersion);
            Assert.AreEqual(payload.xrdmVersion, copy.xrdmVersion);
            Assert.AreEqual(payload.appVersion, copy.appVersion);
            Assert.AreEqual(payload.unityVersion, copy.unityVersion);
            Assert.AreEqual(payload.abxrLibType, copy.abxrLibType);
            Assert.AreEqual(payload.abxrLibVersion, copy.abxrLibVersion);
            Assert.AreEqual(payload.buildFingerprint, copy.buildFingerprint);
        }

        [Test]
        public void CopyForRequest_DeepCopiesMutableFields()
        {
            var requestAuthMechanism = new Dictionary<string, string> { ["type"] = "email", ["prompt"] = "original" };
            var payload = new AuthPayload
            {
                tags = new[] { "tag-a", "tag-b" },
                geolocation = new Dictionary<string, string> { ["latitude"] = "1" },
                authMechanism = new Dictionary<string, string> { ["type"] = "stale" }
            };

            AuthPayload copy = payload.CopyForRequest(requestAuthMechanism);

            Assert.AreNotSame(payload.tags, copy.tags);
            Assert.AreNotSame(payload.geolocation, copy.geolocation);
            Assert.AreNotSame(requestAuthMechanism, copy.authMechanism);
            CollectionAssert.AreEqual(payload.tags, copy.tags);
            CollectionAssert.AreEquivalent(payload.geolocation, copy.geolocation);
            CollectionAssert.AreEquivalent(requestAuthMechanism, copy.authMechanism);

            copy.tags[0] = "changed";
            copy.geolocation["latitude"] = "2";
            copy.authMechanism["prompt"] = "changed";

            Assert.AreEqual("tag-a", payload.tags[0]);
            Assert.AreEqual("1", payload.geolocation["latitude"]);
            Assert.AreEqual("stale", payload.authMechanism["type"]);
            Assert.AreEqual("original", requestAuthMechanism["prompt"]);
        }

        [Test]
        public void CopyForRequest_DoesNotCopySourceAuthMechanismByDefault()
        {
            var payload = new AuthPayload
            {
                authMechanism = new Dictionary<string, string> { ["type"] = "stale" }
            };

            AuthPayload copy = payload.CopyForRequest();

            Assert.IsNull(copy.authMechanism);
            Assert.AreEqual("stale", payload.authMechanism["type"]);
        }
    }
}
