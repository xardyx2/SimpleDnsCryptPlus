using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Minisign;

namespace SimpleDnsCryptPlus.Tools.MinisignTool
{
    /// <summary>
    /// Generates and uses the minisign key that authenticates Simple DNSCrypt Plus releases.
    ///
    /// Secrets are taken from environment variables, never from command-line arguments: argv is
    /// visible in the process list and gets echoed into CI logs by shell tracing.
    ///
    /// Verbs
    ///   keygen --out DIR [--name update]
    ///       Writes <name>.key, <name>.pub and <name>.passphrase into DIR, which must not already
    ///       contain them. The passphrase is random and is never printed to stdout.
    ///
    ///   sign --file PATH --signature-out PATH [--trusted-comment TEXT]
    ///       Reads MINISIGN_PRIVATE_KEY and MINISIGN_PASSWORD from the environment and writes a
    ///       detached, hashed minisign signature. Refuses to overwrite an existing signature.
    ///
    ///   pubkey --key-file PATH
    ///       Prints the public key line. MINISIGN_PASSWORD must be set; used to confirm the secret
    ///       stored in CI matches the public key committed in the repository.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.Error.WriteLine("usage: minisign-tool <keygen|sign|pubkey> [options]");
                    return 2;
                }

                var options = ParseOptions(args.Skip(1).ToArray());

                switch (args[0])
                {
                    case "keygen": return Keygen(options);
                    case "sign": return Sign(options);
                    case "pubkey": return PubKey(options);
                    default:
                        Console.Error.WriteLine("unknown verb: " + args[0]);
                        return 2;
                }
            }
            catch (Exception ex)
            {
                // Never print ex.ToString() here: a library exception message could echo key
                // material. Type and message only, and no stack.
                Console.Error.WriteLine("error: " + ex.GetType().Name + ": " + Sanitise(ex.Message));
                return 1;
            }
        }

        private static int Keygen(System.Collections.Generic.Dictionary<string, string> o)
        {
            var outDir = Required(o, "out");
            var name = o.TryGetValue("name", out var n) ? n : "update";

            Directory.CreateDirectory(outDir);

            foreach (var suffix in new[] { ".key", ".pub", ".passphrase" })
            {
                if (File.Exists(Path.Combine(outDir, name + suffix)))
                {
                    Console.Error.WriteLine($"refusing to overwrite existing {name}{suffix} in {outDir}");
                    return 1;
                }
            }

            var passphrase = RandomPassphrase();

            var pair = Core.GenerateKeyPair(passphrase, true, outDir, name);

            File.WriteAllText(
                Path.Combine(outDir, name + ".passphrase"),
                passphrase + Environment.NewLine,
                new UTF8Encoding(false));

            Console.WriteLine("keygen: wrote " + name + ".key, " + name + ".pub, " + name + ".passphrase to " + outDir);
            Console.WriteLine("keygen: public key = " + ExtractKeyLine(
                File.ReadAllText(Path.Combine(outDir, name + ".pub"))));
            Console.WriteLine("keygen: the private key and passphrase were NOT printed. Move " +
                              name + ".key and " + name + ".passphrase into your own storage now, " +
                              "then delete this folder.");

            _ = pair;
            return 0;
        }

        private static int Sign(System.Collections.Generic.Dictionary<string, string> o)
        {
            var file = Required(o, "file");
            var signatureOut = Required(o, "signature-out");

            if (!File.Exists(file))
            {
                Console.Error.WriteLine("file not found: " + file);
                return 1;
            }

            if (File.Exists(signatureOut))
            {
                Console.Error.WriteLine("refusing to overwrite existing signature: " + signatureOut);
                return 1;
            }

            var privateKey = LoadPrivateFromEnvironment();

            var comment = o.TryGetValue("trusted-comment", out var tc) ? tc : "file:" + Path.GetFileName(file) + " hashed";

            // SignHashed writes "<file>.minisig" next to the payload, so sign into a scratch
            // directory and move the result to wherever the caller asked for.
            var scratch = Path.Combine(Path.GetTempPath(), "sdc-sign-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);

            try
            {
                var stagedPayload = Path.Combine(scratch, Path.GetFileName(file));
                File.Copy(file, stagedPayload, true);

                var written = Core.SignHashed(stagedPayload, privateKey, "Simple DNSCrypt Plus release", comment, scratch);

                File.Move(written, signatureOut, true);
            }
            finally
            {
                Directory.Delete(scratch, true);
            }

            Console.WriteLine("sign: wrote " + signatureOut);
            return 0;
        }

        private static int PubKey(System.Collections.Generic.Dictionary<string, string> o)
        {
            var keyFile = Required(o, "key-file");

            if (!File.Exists(keyFile))
            {
                Console.Error.WriteLine("key file not found: " + keyFile);
                return 1;
            }

            var password = RequiredEnv("MINISIGN_PASSWORD");
            var pair = Core.LoadPrivateKeyFromFile(keyFile, password);

            Console.WriteLine(Convert.ToBase64String(pair.PublicKey));
            return 0;
        }

        private static Minisign.Models.MinisignPrivateKey LoadPrivateFromEnvironment()
        {
            var material = RequiredEnv("MINISIGN_PRIVATE_KEY");
            var password = RequiredEnv("MINISIGN_PASSWORD");

            // minisign-net's string loader wants the base64 line on its own, but a human pasting a
            // secret will paste the whole two-line file. Accept both.
            return Core.LoadPrivateKeyFromString(ExtractKeyLine(material), password);
        }

        private static string ExtractKeyLine(string multiLine)
        {
            var lines = multiLine.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("untrusted comment:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count == 0)
            {
                throw new InvalidOperationException("no base64 key material found in the supplied value");
            }

            return lines.Last();
        }

        private static string RandomPassphrase()
        {
            // 32 bytes of entropy, base64 encoded. Long enough that the file's encryption is the
            // thing protecting it rather than the passphrase's guessability.
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string RequiredEnv(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(name + " is not set");
            }

            return value;
        }

        private static string Required(System.Collections.Generic.Dictionary<string, string> o, string key)
        {
            if (!o.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("--" + key + " is required");
            }

            return value;
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseOptions(string[] args)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--"))
                {
                    throw new InvalidOperationException("unexpected argument: " + args[i]);
                }

                var key = args[i].Substring(2);

                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                {
                    throw new InvalidOperationException("--" + key + " needs a value");
                }

                result[key] = args[++i];
            }

            return result;
        }

        private static string Sanitise(string message)
        {
            // Trim to one line and cap length so a library message cannot dump a whole key.
            var oneLine = message.Replace("\r", " ").Replace("\n", " ");

            return oneLine.Length > 160 ? oneLine.Substring(0, 160) + "..." : oneLine;
        }
    }
}
