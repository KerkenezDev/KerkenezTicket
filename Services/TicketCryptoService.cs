using System;
using System.Security.Cryptography;
using System.Text;

namespace KerkenezTicket.Services
{
    public static class TicketCryptoService
    {
        private static readonly byte[] PrimaryTicketEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureTickets.v1");
        private static readonly byte[] FallbackSuiteEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureSuite.v1");

        /// <summary>
        /// Pass-through plaintext string (DPAPI encryption removed).
        /// </summary>
        public static string EncryptString(string? plainText)
        {
            return plainText ?? "";
        }

        /// <summary>
        /// Decrypts a legacy Base64 DPAPI ciphertext string into plaintext if encrypted; otherwise returns as-is.
        /// </summary>
        public static string DecryptString(string? cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
            {
                return "";
            }

            // Quick check: if string contains spaces, newlines, or cannot be base64, return as-is
            if (cipherText.Contains(' ') || cipherText.Contains('\n') || cipherText.Contains('\r'))
            {
                return cipherText;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[][] candidateEntropies = { PrimaryTicketEntropy, FallbackSuiteEntropy };

                foreach (var entropy in candidateEntropies)
                {
                    try
                    {
                        byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, entropy, DataProtectionScope.CurrentUser);
                        return Encoding.UTF8.GetString(plainBytes);
                    }
                    catch
                    {
                        // Try next candidate
                    }
                }

                return cipherText;
            }
            catch
            {
                return cipherText;
            }
        }

        public static byte[] EncryptBytes(byte[]? plainBytes)
        {
            return plainBytes ?? Array.Empty<byte>();
        }

        public static byte[] DecryptBytes(byte[]? cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length == 0) return Array.Empty<byte>();

            byte[][] candidateEntropies = { PrimaryTicketEntropy, FallbackSuiteEntropy };
            foreach (var entropy in candidateEntropies)
            {
                try
                {
                    return ProtectedData.Unprotect(cipherBytes, entropy, DataProtectionScope.CurrentUser);
                }
                catch
                {
                    // Try next
                }
            }

            return cipherBytes;
        }
    }
}
