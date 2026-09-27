using System;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// source: https://gist.github.com/igorushko/cccef0561aea7e46ae52bc62270b2b61
    /// </summary>
    public static class Base64Url
    {
        public static string Encode(byte[] arg)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg));

            var s = Convert.ToBase64String(arg);
            return s
                .Replace("=", "")
                .Replace("/", "_")
                .Replace("+", "-");
        }

        public static string ToBase64(string arg)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg));
            var lastQuadCount = arg.Length % 4;
            if (lastQuadCount == 1)
            {
                throw new Exception("Invalid base64 string: the last four-character block cannot consist of only one character");
            }

            var s = arg 
                .PadRight(arg.Length + (4 - lastQuadCount) % 4, '=')
                .Replace("_", "/")
                .Replace("-", "+");

            return s;
        }

        public static byte[] Decode(string arg)
        {
            var decrypted = ToBase64(arg);

            return Convert.FromBase64String(decrypted);
        }
    }
}