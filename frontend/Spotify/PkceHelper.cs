using System;
using System.Security.Cryptography;
using System.Text;

namespace NeedleDrop.Spotify
{
    /// <summary>
    /// PKCE (Proof Key for Code Exchange) helpers for Spotify's Authorization
    /// Code flow. PKCE lets a distributed desktop app authenticate against
    /// Spotify's OAuth server without embedding a client secret anywhere in
    /// the shipped exe — the verifier/challenge pair proves the token
    /// exchange came from the same app instance that started the login.
    /// </summary>
    internal static class PkceHelper
    {
        public static string GenerateCodeVerifier()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncode(bytes);
        }

        public static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
