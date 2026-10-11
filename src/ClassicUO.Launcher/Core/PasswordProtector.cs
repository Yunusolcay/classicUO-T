// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClassicUO.Launcher.Core
{
    // Passwords saved by the launcher, so copying launcher.json to another machine or account does
    // not reveal them. Windows binds them to the user account with DPAPI; Linux/macOS use AES-GCM
    // with a random per-user key stored outside the launcher folder, readable only by that user.
    internal static class PasswordProtector
    {
        private const string DPAPI_PREFIX = "dpapi:";
        private const string AES_PREFIX = "aes:";
        private const string CUO_PREFIX = "cuo:";
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("classicUO-T.launcher");

        public static string Protect(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            byte[] plain = Encoding.UTF8.GetBytes(password);

            if (OperatingSystem.IsWindows())
            {
                byte[] data = ProtectedData.Protect(plain, _entropy, DataProtectionScope.CurrentUser);

                return DPAPI_PREFIX + Convert.ToBase64String(data);
            }

            return ProtectAes(plain);
        }

        internal static string ProtectAes(byte[] plain)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
            byte[] cipher = new byte[plain.Length];

            using (var aes = new AesGcm(GetUserKey(), tag.Length))
            {
                aes.Encrypt(nonce, plain, cipher, tag, _entropy);
            }

            return AES_PREFIX + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        private static byte[] DecryptAes(byte[] blob)
        {
            int nonceSize = AesGcm.NonceByteSizes.MaxSize;
            int tagSize = AesGcm.TagByteSizes.MaxSize;

            if (blob.Length < nonceSize + tagSize)
            {
                throw new CryptographicException("Saved password is truncated");
            }

            byte[] plain = new byte[blob.Length - nonceSize - tagSize];

            using var aes = new AesGcm(GetUserKey(), tagSize);
            aes.Decrypt(blob.AsSpan(0, nonceSize), blob.AsSpan(nonceSize + tagSize), blob.AsSpan(nonceSize, tagSize), plain, _entropy);

            return plain;
        }

        private static byte[] GetUserKey()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "classicUO-T");
            string path = Path.Combine(dir, "launcher.key");

            if (File.Exists(path))
            {
                byte[] existing = File.ReadAllBytes(path);

                if (existing.Length == 32)
                {
                    return existing;
                }
            }

            Directory.CreateDirectory(dir);
            byte[] key = RandomNumberGenerator.GetBytes(32);

            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var fs = new FileStream(path, options))
            {
                fs.Write(key);
            }

            return key;
        }

        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                if (value.StartsWith(DPAPI_PREFIX, StringComparison.Ordinal) && OperatingSystem.IsWindows())
                {
                    byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(value.Substring(DPAPI_PREFIX.Length)), _entropy, DataProtectionScope.CurrentUser);

                    return Encoding.UTF8.GetString(data);
                }

                if (value.StartsWith(AES_PREFIX, StringComparison.Ordinal))
                {
                    return Encoding.UTF8.GetString(DecryptAes(Convert.FromBase64String(value.Substring(AES_PREFIX.Length))));
                }

                // older launcher builds on Linux/macOS
                if (value.StartsWith(CUO_PREFIX, StringComparison.Ordinal))
                {
                    return ClassicUOCrypter.Decrypt(value.Substring(CUO_PREFIX.Length));
                }
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                LauncherLog.Write($"Saved password could not be decrypted: {ex.Message}");
            }

            return string.Empty;
        }
    }

    // Same format as ClassicUO.Utility.Crypter, which ClassicUO uses for the "password" field of
    // settings.json. Copied instead of referenced so the launcher does not pull in FNA.
    internal static class ClassicUOCrypter
    {
        public static string Encrypt(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            byte[] buff = Encoding.ASCII.GetBytes(source);
            string key = Environment.MachineName;

            if (key.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(source.Length * 2 + 2);
            sb.Append("1-");

            for (int i = 0, k = 0; i < buff.Length; i++)
            {
                sb.AppendFormat("{0:X2}", (byte)(buff[i] ^ (byte)key[k++]));

                if (k >= key.Length)
                {
                    k = 0;
                }
            }

            return sb.ToString();
        }

        public static string Decrypt(string source)
        {
            if (source.Length <= 2 || source[0] != '1' || source[1] != '-' && source[1] != '+')
            {
                return string.Empty;
            }

            string key = Environment.MachineName;

            if (key.Length == 0)
            {
                return string.Empty;
            }

            byte[] buff = new byte[(source.Length - 2) >> 1];

            for (int i = 2, k = 0; i + 1 < source.Length; i += 2)
            {
                buff[(i - 2) >> 1] = (byte)(Convert.ToByte(source.Substring(i, 2), 16) ^ (byte)key[k++]);

                if (k >= key.Length)
                {
                    k = 0;
                }
            }

            return Encoding.ASCII.GetString(buff);
        }
    }
}
