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
        /// Encrypts sensitive string data using Windows DPAPI tied to the current interactive Windows user.
        /// </summary>
        public static string EncryptString(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "";
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, PrimaryTicketEntropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TicketCryptoService] Encryption failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Decrypts a Base64 DPAPI ciphertext string into plaintext.
        /// </summary>
        public static string DecryptString(string? cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
            {
                return "";
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

                // If ciphertext was somehow unencrypted legacy text, return as-is
                return cipherText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TicketCryptoService] Decryption error: {ex.Message}");
                return cipherText;
            }
        }

        /// <summary>
        /// Encrypts raw bytes using Windows DPAPI.
        /// </summary>
        public static byte[] EncryptBytes(byte[]? plainBytes)
        {
            if (plainBytes == null || plainBytes.Length == 0) return Array.Empty<byte>();

            try
            {
                return ProtectedData.Protect(plainBytes, PrimaryTicketEntropy, DataProtectionScope.CurrentUser);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TicketCryptoService] Byte encryption failed: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Decrypts DPAPI-protected bytes.
        /// </summary>
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

            return Array.Empty<byte>();
        }
    }
}
