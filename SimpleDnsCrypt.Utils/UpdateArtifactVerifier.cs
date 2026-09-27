using System;
using System.IO;
using System.Linq;
using Minisign;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// Checks a downloaded release artifact against a minisign signature and a public key.
    /// Never throws on bad input: an unparseable signature is a verification failure, not an
    /// exception, so a caller cannot mistake a crash for a pass.
    /// </summary>
    public static class UpdateArtifactVerifier
    {
        /// <summary>
        /// True only when <paramref name="armoredSignature"/> is a well-formed minisign signature
        /// over <paramref name="filePath"/> made by the key in <paramref name="publicKeyString"/>.
        /// </summary>
        public static bool VerifyFile(string filePath, string armoredSignature, string publicKeyString)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(armoredSignature) || string.IsNullOrWhiteSpace(publicKeyString))
            {
                return false;
            }

            try
            {
                var publicKey = Core.LoadPublicKeyFromString(ExtractKeyLine(publicKeyString));
                var signature = LoadArmored(armoredSignature);

                if (signature == null || publicKey == null)
                {
                    return false;
                }

                // The key id embedded in the signature must be the key we trust, otherwise a
                // signature from any other key would be judged only on its bytes.
                if (!SameKeyId(signature, publicKey))
                {
                    return false;
                }

                return signature.IsHashed
                    ? Core.ValidateHashedSignature(filePath, signature, publicKey)
                    : Core.ValidateSignature(filePath, signature, publicKey);
            }
            catch (Exception)
            {
                // Corrupt key, corrupt signature, wrong password, truncated file - all a "no".
                return false;
            }
        }

        /// <summary>
        /// minisign's .pub file is two lines - a comment then the base64 key - but
        /// Core.LoadPublicKeyFromString only accepts the base64 on its own. Accept either form so a
        /// caller can paste the file contents or just the key.
        /// </summary>
        private static string ExtractKeyLine(string publicKeyString)
        {
            var lines = publicKeyString
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            return lines.Last(l => !l.StartsWith("untrusted comment:", StringComparison.OrdinalIgnoreCase));
        }

        private static Minisign.Models.MinisignSignature LoadArmored(string armored)
        {
            var lines = new System.Collections.Generic.List<string>();

            foreach (var raw in armored.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }

            // canonical hashed/legacy minisign format is four lines:
            //   untrusted comment: ...
            //   <base64 signature incl. algorithm + key id>
            //   trusted comment: ...
            //   <base64 global signature>
            if (lines.Count != 4)
            {
                return null;
            }

            var untrusted = StripCommentPrefix(lines[0], "untrusted comment:");
            var trusted = StripCommentPrefix(lines[2], "trusted comment:");

            if (untrusted == null || trusted == null)
            {
                return null;
            }

            return Core.LoadSignatureFromString(lines[1], trusted, lines[3]);
        }

        private static string StripCommentPrefix(string line, string prefix)
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return line.Substring(prefix.Length).Trim();
        }

        private static bool SameKeyId(
            Minisign.Models.MinisignSignature signature,
            Minisign.Models.MinisignPublicKey publicKey)
        {
            var a = signature.KeyId;
            var b = publicKey.KeyId;

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
