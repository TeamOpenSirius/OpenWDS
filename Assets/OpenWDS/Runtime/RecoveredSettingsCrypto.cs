using System;
using System.Security.Cryptography;
using System.Text;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Compatible implementation of PreferencesExtensions.Core.AesEncoder for settings files.
    /// It uses the original PBKDF2 stream for the AES key followed by the IV.
    /// </summary>
    public static class RecoveredSettingsCrypto
    {
        public static string EncryptUtf8(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (var aes = CreateAes())
            using (var encryptor = aes.CreateEncryptor())
            {
                var source = Encoding.UTF8.GetBytes(value);
                var encrypted = encryptor.TransformFinalBlock(source, 0, source.Length);
                return Convert.ToBase64String(encrypted);
            }
        }

        public static string DecryptUtf8(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (var aes = CreateAes())
            using (var decryptor = aes.CreateDecryptor())
            {
                var source = Convert.FromBase64String(value);
                var decrypted = decryptor.TransformFinalBlock(source, 0, source.Length);
                return Encoding.UTF8.GetString(decrypted);
            }
        }

        private static AesManaged CreateAes()
        {
            var aes = new AesManaged
            {
                KeySize = RecoveredSettingsPersistence.AesKeySize,
            };
            using (var deriveBytes = new Rfc2898DeriveBytes(
                       RecoveredSettingsPersistence.Password,
                       Encoding.UTF8.GetBytes(RecoveredSettingsPersistence.Salt)))
            {
                aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
                aes.IV = deriveBytes.GetBytes(aes.BlockSize / 8);
            }
            return aes;
        }
    }
}
