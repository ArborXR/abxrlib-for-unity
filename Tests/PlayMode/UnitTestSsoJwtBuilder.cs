// Copyright (c) 2026 ArborXR. All rights reserved.
// Builds unsigned sample JWTs for PlayMode tests of the MDM SSO identity path (GetIsAuthenticated + GetAccessToken).
using System;
using System.Text;

/// <summary>Minimal JWT (alg=none) with <c>sub</c> and <c>email</c> claims for MDM SSO PlayMode tests.</summary>
public static class UnitTestSsoJwtBuilder
{
    /// <summary>
    /// Deterministic token decodable by Utils.TryDecodeJwtPayload; paste into AbxrLib Unit Test Credentials → SSO access token (JWT).
    /// </summary>
    public static string MinimalIdentityJwt()
    {
        string header = Base64UrlUtf8("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        string payload = Base64UrlUtf8("{\"sub\":\"abxrlib-unit-test-sso\",\"email\":\"abxr-sso-unit-test@example.com\"}");
        return header + "." + payload + ".unit-test-signature";
    }

    static string Base64UrlUtf8(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        string b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
