using AbxrLib.Runtime.Services.Auth;
using AbxrLib.Runtime.Services.Platform;
using AbxrLib.Runtime.Types;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    [TestFixture]
    public class AuthHandoffCoordinatorTests
    {
        [TearDown]
        public void TearDown()
        {
            AuthHandoffCoordinator.TestPayload = null;
        }

        [Test]
        public void TryReadIncomingPayload_ReadsAndroidIntentPayload()
        {
            var platform = new FakePlatformSource();
            platform.AndroidIntentPayload = " {\"Token\":\"android-token\"} ";
            var coordinator = new AuthHandoffCoordinator(platform);

            bool found = coordinator.TryReadIncomingPayload(out string normalized);

            Assert.IsTrue(found);
            Assert.AreEqual("{\"Token\":\"android-token\"}", normalized);
        }

        [Test]
        public void TryReadIncomingPayload_ReadsWebGlQueryPayload()
        {
            var platform = new FakePlatformSource
            {
                IsWebGlPlayer = true,
                AbsoluteUrl = "https://example.test/index.html?auth_handoff=%7B%22Token%22%3A%22webgl-token%22%7D"
            };
            var coordinator = new AuthHandoffCoordinator(platform);

            bool found = coordinator.TryReadIncomingPayload(out string normalized);

            Assert.IsTrue(found);
            Assert.AreEqual("{\"Token\":\"webgl-token\"}", normalized);
        }

        [Test]
        public void MarkApplied_SetsSessionStateAndStoresReturnPackageOnce()
        {
            var coordinator = new AuthHandoffCoordinator(new FakePlatformSource());
            var response = new AuthResponse { ReturnToPackage = "com.example.launcher" };

            coordinator.MarkApplied(response);

            Assert.IsTrue(coordinator.SessionUsedHandoff);
            Assert.AreEqual("com.example.launcher", coordinator.GetAndClearReturnToPackage());
            Assert.IsNull(coordinator.GetAndClearReturnToPackage());
        }

        [Test]
        public void BuildOutgoingPayload_ReturnsNullUntilAuthenticated()
        {
            var coordinator = new AuthHandoffCoordinator(new FakePlatformSource());

            string json = AuthHandoffCoordinator.BuildOutgoingPayload(
                new AuthResponse { Token = "token", Secret = "secret" },
                new AuthPayload(),
                System.DateTime.UtcNow.AddMinutes(5),
                authenticated: false);

            Assert.IsNull(json);
        }

        private sealed class FakePlatformSource : IAuthPlatformSource
        {
            public bool IsAndroidPlayer { get; set; }
            public bool IsWebGlPlayer { get; set; }
            public bool IsStandalonePlayer { get; set; }
            public string AbsoluteUrl { get; set; } = "";
            public string AndroidIntentPayload { get; set; } = "";
            public string CommandLinePayload { get; set; } = "";

            public string GetAndroidIntentParam(string key) => key == "auth_handoff" ? AndroidIntentPayload : "";
            public string GetCommandLineArg(string key) => key == "auth_handoff" ? CommandLinePayload : "";
            public string GetDesktopOrgToken() => "";
            public string GetOrCreateWebGlDeviceId() => "";
            public bool IsArborMdmConnected(ArborMdmClient arborMdmClient) => false;
            public string GetCurrentDeviceId() => "";
            public string[] GetCurrentDeviceTags() => null;
            public string GetCurrentOrgId() => "";
            public string GetCurrentFingerprint() => "";
            public string GetIpAddress() => "";
            public string GetAndroidManifestMetadata(string key) => "";
            public string GetXrdmVersion() => "";
        }
    }
}
