using System;
using System.Text;
using System.Security.Cryptography;

namespace ApiStudy.Services
{
    public static class CryptingService
    {
        public static string ToSha256(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return "";
            }

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(str));

            StringBuilder builder = new();

            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
