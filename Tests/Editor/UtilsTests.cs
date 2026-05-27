using AbxrLib.Runtime.Core;
using NUnit.Framework;

namespace AbxrLib.Tests.Editor
{
    /// <summary>
    /// Unit tests for pure helpers in <see cref="Utils"/>.
    /// These run in EditMode (no scene, no play mode) and should stay fast — pure input/output, no Unity engine dependencies.
    /// </summary>
    [TestFixture]
    public class UtilsTests
    {
        // ── IsValidUrl ───────────────────────────────────────────────

        [TestCase("https://example.com", true)]
        [TestCase("https://example.com/path?q=1", true)]
        [TestCase("http://127.0.0.1:8765", true)]
        [TestCase("ftp://example.com", false)]
        [TestCase("not a url", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValidUrl_Cases(string url, bool expected)
        {
            Assert.AreEqual(expected, Utils.IsValidUrl(url));
        }

        // ── PascalToCamelCase ────────────────────────────────────────

        [TestCase("KeepLatest", "keepLatest")]
        [TestCase("Device", "device")]
        [TestCase("A", "a")]
        [TestCase("", "")]
        [TestCase(null, null)]
        public void PascalToCamelCase_Cases(string input, string expected)
        {
            Assert.AreEqual(expected, Utils.PascalToCamelCase(input));
        }

        // ── SHA-256 (sanity, not crypto correctness) ─────────────────

        [Test]
        public void ComputeSha256Hash_IsDeterministic()
        {
            Assert.AreEqual(Utils.ComputeSha256Hash("abc"), Utils.ComputeSha256Hash("abc"));
            Assert.AreNotEqual(Utils.ComputeSha256Hash("abc"), Utils.ComputeSha256Hash("abd"));
        }

        // ── Email normalization ──────────────────────────────────────

        [TestCase("user@example.com", true, "user@example.com")]
        [TestCase("  User@Example.com  ", true, "User@Example.com")]
        [TestCase("not-an-email", false, null)]
        [TestCase("", false, null)]
        [TestCase(null, false, null)]
        public void TryNormalizePlausibleEmail_Cases(string input, bool expectedOk, string expectedNormalized)
        {
            bool ok = Utils.TryNormalizePlausibleEmail(input, out var normalized);
            Assert.AreEqual(expectedOk, ok);
            if (expectedOk)
                Assert.AreEqual(expectedNormalized, normalized);
        }
    }
}
