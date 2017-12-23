using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace ConnectLib.Cryptography
{
    public static class Cryptography
    {
        private const int AESKeySize = 128;
        private const int AESDerivationInterations = 1000;
        private static SecureString BasicAuth { get { SecureString ss = new SecureString(); foreach (char c in "YYAfBzWydX8WRJePGepUQDTz") { ss.AppendChar(c); } return ss; } }

        /// <summary>
        /// Encrypts a string using the American Encryption Standard.
        /// </summary>
        /// <param name="input">The string to encrypt.</param>
        /// <returns>An encrypted string representing the original information.</returns>
        public static string AESEncrypt(this string input)
        {
            return input.AESEncrypt(BasicAuth);
        }
        /// <summary>
        /// Encrypts a string with the specified passphrase using the American Encryption Standard.
        /// </summary>
        /// <param name="input">The string to encrypt.</param>
        /// <param name="passphrase">The passphrase to encrypt with.</param>
        /// <returns>An encrypted string representing the original information</returns>
        public static string AESEncrypt(this string input, SecureString passphrase)
        {
            return input.AESEncrypt(new NetworkCredential(string.Empty, passphrase).Password);
        }
        /// <summary>
        /// Encrypts a string with the specified passphrase using the American Encryption Standard.
        /// </summary>
        /// <param name="input">The string to encrypt.</param>
        /// <param name="passphrase">The passphrase to encrypt with.</param>
        /// <returns>An encrypted string representing the original information.</returns>
        public static string AESEncrypt(this string input, string passphrase)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            try
            {
                passphrase = passphrase.MD5_16();
                byte[] salt = GenerateBitsOfRandomEntropy(AESKeySize / 8);
                byte[] iv = GenerateBitsOfRandomEntropy(AESKeySize / 8);
                byte[] plainText = Encoding.UTF8.GetBytes(input);
                using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passphrase, salt, AESDerivationInterations))
                {
                    byte[] key = password.GetBytes(AESKeySize / 8);
                    using (Aes symmetricKey = Aes.Create())
                    {
                        symmetricKey.BlockSize = AESKeySize;
                        symmetricKey.Mode = CipherMode.CBC;
                        symmetricKey.Padding = PaddingMode.PKCS7;
                        using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(key, iv))
                        {
                            using (MemoryStream stream = new MemoryStream())
                            {
                                using (CryptoStream cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                                {
                                    cryptoStream.Write(plainText, 0, plainText.Length);
                                    cryptoStream.FlushFinalBlock();
                                    var cipherText = salt;
                                    cipherText = cipherText.Concat(iv).ToArray();
                                    cipherText = cipherText.Concat(stream.ToArray()).ToArray();
                                    return Convert.ToBase64String(cipherText);
                                }
                            }
                        }
                    }
                }
            }
            catch { return input; }
        }
        public static string AESDecrypt(this string input)
        {
            return input.AESDecrypt(BasicAuth);
        }
        /// <summary>
        /// Decrypts a string with the specified passphrase using the American Encryption Standard.
        /// </summary>
        /// <param name="input">The string to decrypt.</param>
        /// <param name="passphrase">The passphrase to decrypt with.</param>
        /// <returns>A decrypted string of the original information.</returns>
        public static string AESDecrypt(this string input, SecureString passphrase)
        {
            return input.AESDecrypt(new NetworkCredential(string.Empty, passphrase).Password);
        }
        /// <summary>
        /// Decrypts a string with the specified passphrase using the American Encryption Standard.
        /// </summary>
        /// <param name="input">The string to decrypt.</param>
        /// <param name="passphrase">The passphrase to decrypt with.</param>
        /// <returns>A decrypted string of the original information.</returns>
        public static string AESDecrypt(this string input, string passphrase)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            try
            {
                passphrase = passphrase.MD5_16();
                byte[] cipherText = Convert.FromBase64String(input);
                byte[] salt = cipherText.Take(AESKeySize / 8).ToArray();
                byte[] iv = cipherText.Skip(AESKeySize / 8).Take(AESKeySize / 8).ToArray();
                cipherText = cipherText.Skip((AESKeySize / 8) * 2).Take(cipherText.Length - ((AESKeySize / 8) * 2)).ToArray();
                using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passphrase, salt, AESDerivationInterations))
                {
                    byte[] key = password.GetBytes(AESKeySize / 8);
                    using (Aes symmetricKey = Aes.Create())
                    {
                        symmetricKey.BlockSize = AESKeySize;
                        symmetricKey.Mode = CipherMode.CBC;
                        symmetricKey.Padding = PaddingMode.PKCS7;
                        using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(key, iv))
                        {
                            using (MemoryStream stream = new MemoryStream(cipherText))
                            {
                                using (CryptoStream cryptoStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
                                {
                                    byte[] plainText = new byte[cipherText.Length];
                                    int decryptedBytes = cryptoStream.Read(plainText, 0, plainText.Length);
                                    return Encoding.UTF8.GetString(plainText, 0, decryptedBytes);
                                }
                            }
                        }
                    }
                }
            }
            catch { return input; }
        }
        /// <summary>
        /// Creates a sixteen-character MD5 hash of the specified string.
        /// </summary>
        /// <param name="input">The string to hash.</param>
        /// <returns>A sixteen-character MD5 string.</returns>
        public static string MD5_16(this string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in data)
                    sb.Append(b.ToString("x2"));
                return sb.ToString().Substring(0, 16);
            }
        }

        private static byte[] GenerateBitsOfRandomEntropy(this int bytes)
        {
            byte[] randomBytes = new byte[bytes];
            using (RandomNumberGenerator rngCsp = RandomNumberGenerator.Create())
                rngCsp.GetBytes(randomBytes);
            return randomBytes;
        }
    }
}