using System;
using System.IO;
using System.Linq;
using System.Text;
using Minisign;
using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// The update channel has one job: decide whether a remote build is newer, and refuse anything
    /// that is not signed by the key this application was built to trust.
    ///
    /// The second half is the reason these tests exist. Upstream 0.7.x ships Christian Hermann's
    /// minisign public key as a compile-time constant, so a fork that reuses that key would accept
    /// whatever he signs; a fork that simply stops checking would accept whatever anyone hosts.
    /// Both are covered here.
    /// </summary>
    public class UpdateChannelTests
    {
        private string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "sdc-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        private (string publicKey, Minisign.Models.MinisignPrivateKey key) NewKey(string name)
        {
            var kp = Core.GenerateKeyPair("test-passphrase", true, dir, name);
            return (File.ReadAllText(Path.Combine(dir, name + ".pub")), kp.MinisignPrivateKey);
        }

        private string SignFile(string payloadPath, Minisign.Models.MinisignPrivateKey key)
        {
            // Match what minisign and tools/minisign-tool write: the trusted comment names the file
            // the hash was taken over, and UpdateArtifactVerifier enforces that binding.
            return Core.SignHashed(payloadPath, key, "sdc-plus test",
                "file:" + Path.GetFileName(payloadPath) + " hashed", dir);
        }

        [Test]
        public void AManifestSignedByKeyAIsRejectedWhenVerifiedWithKeyB()
        {
            var (pubA, keyA) = NewKey("a");
            var (pubB, _) = NewKey("b");

            var payload = Path.Combine(dir, "artifact.zip");
            File.WriteAllText(payload, "release bytes");
            var sigPath = SignFile(payload, keyA);

            Assert.IsTrue(UpdateArtifactVerifier.VerifyFile(payload, File.ReadAllText(sigPath), pubA),
                "sanity: the signature must verify against its own key");
            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(payload, File.ReadAllText(sigPath), pubB),
                "a signature from another key must never be accepted");
        }

        [Test]
        public void ATamperedArtifactIsRejected()
        {
            var (pub, key) = NewKey("k");
            var payload = Path.Combine(dir, "artifact.zip");
            File.WriteAllText(payload, "release bytes");
            var sig = File.ReadAllText(SignFile(payload, key));

            Assert.IsTrue(UpdateArtifactVerifier.VerifyFile(payload, sig, pub));

            File.AppendAllText(payload, " injected");
            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(payload, sig, pub),
                "modifying the artifact after signing must fail verification");
        }

        [Test]
        public void AHashedSignatureIsRejectedWhenTheFileWasRenamed()
        {
            // A pre-hashed minisign signature names its payload in the trusted comment, and the
            // official CLI refuses a file that does not match that name. minisign-net does not, so
            // without this the app would accept a release-blessed signature replayed onto a
            // differently-named artifact - and would disagree with the verification instructions we
            // publish for users who run `minisign -Vm` themselves.
            var (pub, key) = NewKey("n");

            var signed = Path.Combine(dir, "SimpleDNSCryptPlus-x64-1.0.1-portable.zip");
            File.WriteAllText(signed, "release bytes");
            var sig = File.ReadAllText(SignFile(signed, key));

            Assert.IsTrue(UpdateArtifactVerifier.VerifyFile(signed, sig, pub),
                "sanity: the name must match in the passing case");

            var renamed = Path.Combine(dir, "not-what-was-signed.zip");
            File.Copy(signed, renamed);

            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(renamed, sig, pub),
                "a hashed signature must be bound to the file name it was made over");
        }

        [Test]
        public void GarbageOrEmptySignatureInputIsRejectedNotThrown()
        {
            var (pub, _) = NewKey("k2");
            var payload = Path.Combine(dir, "artifact.zip");
            File.WriteAllText(payload, "bytes");

            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(payload, "", pub));
            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(payload, "not a signature at all", pub));
            Assert.IsFalse(UpdateArtifactVerifier.VerifyFile(payload, "RW", pub));
        }

        [Test]
        public void TheTrustedKeyIsNotUpstreamAuthorKey()
        {
            // Christian Hermann's key, published in the upstream README and compiled into 0.7.x.
            const string BitbeansKey = "RWTSM+4BNNvkZPNkHgE88ETlhWa+0HDzU5CN8TvbyvmhVUcr6aQXfssV";

            var trusted = UpdateChannel.TrustedPublicKey;

            Assert.False(string.IsNullOrWhiteSpace(trusted),
                "no update key is embedded, which would mean the updater trusts nothing or everything");
            Assert.That(trusted.Trim(), Is.Not.EqualTo(BitbeansKey),
                "trusting the original author's key would accept whatever upstream signs");
            Assert.That(trusted, Does.Not.Contain("RWTSM+4BNN"));

            // Without this, a placeholder string would satisfy the checks above and ship.
            Assert.DoesNotThrow(() => Minisign.Core.LoadPublicKeyFromString(trusted.Trim()),
                "the trusted key must be a real parseable minisign public key, not a placeholder");
            var parsed = Minisign.Core.LoadPublicKeyFromString(trusted.Trim());
            Assert.IsNotNull(parsed.PublicKey);
            Assert.AreEqual(32, parsed.PublicKey.Length, "an Ed25519 minisign public key is 32 bytes");
        }

        [Test]
        public void TheCommittedPublicKeyMatchesTheCompiledTrustedKey()
        {
            // tools/keys/update.pub is what an operator reads and what CI cross-checks against.
            // If it ever differs from the key compiled into the binary, the published key is a lie
            // about what the app actually trusts.
            var repoRoot = FindRepoRoot();
            var pubPath = Path.Combine(repoRoot, "tools", "keys", "update.pub");

            Assert.IsTrue(File.Exists(pubPath), "tools/keys/update.pub is missing");

            var published = File.ReadAllLines(pubPath)
                .Select(l => l.Trim())
                .Last(l => l.Length > 0 && !l.StartsWith("untrusted comment:"));

            Assert.AreEqual(UpdateChannel.TrustedPublicKey.Trim(), published,
                "the committed public key and the compiled trusted key disagree");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleDnsCrypt.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "could not locate the solution root");

            return dir.FullName;
        }

        [Test]
        public void TheUpdateUrisPointAtAChannelThisRepositoryControls()
        {
            // The URIs are compiled in, so a stale value would either 404 forever or - worse - read
            // someone else's release channel. Both architectures must be https and both must be ours.
            var uris = new[] { SimpleDnsCrypt.Config.Global.ApplicationUpdateUri,
                               SimpleDnsCrypt.Config.Global.ApplicationUpdateUri64 };

            foreach (var raw in uris)
            {
                Assert.IsTrue(Uri.TryCreate(raw, UriKind.Absolute, out var uri), "not a URI: " + raw);
                Assert.AreEqual(Uri.UriSchemeHttps, uri.Scheme, "plain http would let the network path rewrite the update");
                Assert.AreEqual("github.com", uri.Host);
                Assert.IsTrue(uri.AbsolutePath.StartsWith("/xardyx2/SimpleDnsCryptPlus/releases/"),
                    "the updater must read this project's releases, not another's: " + raw);
                Assert.That(raw, Does.Not.Contain("bitbeans"));
                Assert.That(raw, Does.Not.Contain("raw.githubusercontent.com"));
            }

            Assert.AreNotEqual(uris[0], uris[1], "x86 and x64 must not share one manifest");
        }

        [Test]
        public void AManifestAsTheBuilderScriptWritesItSurvivesParseAndVerification()
        {
            // build/make-update-manifest.ps1 puts the whole detached signature into a JSON string, so
            // it arrives here with embedded newlines and a trailing one. This is the real producer's
            // shape, including CRLF, which is what a manifest written on Windows looks like.
            var (pub, key) = NewKey("shaped");

            var payload = Path.Combine(dir, ReleaseZipName);
            File.WriteAllText(payload, "release bytes");
            var armored = File.ReadAllText(SignFile(payload, key));

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                format = "zip",
                version = "1.0.1",
                releaseDate = "2026-09-27",
                downloadUri = "https://github.com/xardyx2/SimpleDnsCryptPlus/releases/download/v1.0.1/" + ReleaseZipName,
                sha256 = new string('c', 64),
                signatureArmored = armored
            });

            var manifest = UpdateManifest.Parse(json);

            Assert.IsNotNull(manifest, "a manifest in the published shape must parse");
            Assert.IsTrue(UpdateArtifactVerifier.VerifyFile(
                payload, manifest.SignatureArmored, pub), "and its signature must still verify");

            // A manifest assembled on Windows carries CRLF inside that string; the reader must not
            // care which line endings a middle tool chose.
            var crlfJson = json.Replace("\\n", "\\r\\n");
            var crlfManifest = UpdateManifest.Parse(crlfJson);

            Assert.IsNotNull(crlfManifest, "a CRLF manifest must parse");
            Assert.IsTrue(UpdateArtifactVerifier.VerifyFile(
                payload, crlfManifest.SignatureArmored, pub),
                "line endings inside the armored signature must not change the verdict");
        }

        private const string ReleaseZipName = "SimpleDNSCryptPlus-x64-1.0.1-portable.zip";

        [Test]
        public void NewerVersionsAreDetectedAndOlderOrEqualAreNot()
        {
            var current = new Version(1, 0, 0);

            Assert.IsTrue(UpdateChecker.IsNewer("1.0.1", current));
            Assert.IsTrue(UpdateChecker.IsNewer("1.1.0", current));
            Assert.IsTrue(UpdateChecker.IsNewer("2.0.0", current));
            Assert.IsFalse(UpdateChecker.IsNewer("1.0.0", current), "equal is not an update");
            Assert.IsFalse(UpdateChecker.IsNewer("0.9.9", current));
            // the two builds still in the wild must never look like an upgrade
            Assert.IsFalse(UpdateChecker.IsNewer("0.7.1", current));
            Assert.IsFalse(UpdateChecker.IsNewer("0.8.2", current));
        }

        [Test]
        public void AnUnparseableVersionDoesNotTriggerAnUpdate()
        {
            var current = new Version(1, 0, 0);

            Assert.IsFalse(UpdateChecker.IsNewer(null, current));
            Assert.IsFalse(UpdateChecker.IsNewer("", current));
            Assert.IsFalse(UpdateChecker.IsNewer("nightly", current));
            Assert.IsFalse(UpdateChecker.IsNewer("1.0", current), "partial versions are rejected, not guessed");
        }

        [Test]
        public void ManifestParsesAndRejectsMissingFields()
        {
            var good = @"{
              ""format"": ""zip"",
              ""version"": ""1.0.1"",
              ""releaseDate"": ""2026-09-27"",
              ""downloadUri"": ""https://github.com/xardyx2/SimpleDnsCryptPlus/releases/download/v1.0.1/SimpleDNSCryptPlus-x64-1.0.1-portable.zip"",
              ""sha256"": """ + new string('a', 64) + @""",
              ""signatureArmored"": ""untrusted comment: x\nRUQ=\ntrusted comment: y\nAAA=""
            }";

            var manifest = UpdateManifest.Parse(good);

            Assert.IsNotNull(manifest);
            Assert.AreEqual("1.0.1", manifest.Version);
            Assert.AreEqual("zip", manifest.Format);
            Assert.IsTrue(manifest.DownloadUri.IsAbsoluteUri);
            Assert.AreEqual(64, manifest.Sha256.Length);

            Assert.IsNull(UpdateManifest.Parse(@"{""version"":""1.0.1""}"), "missing fields must not parse");
            Assert.IsNull(UpdateManifest.Parse("not json"), "malformed json must not parse");
            Assert.IsNull(UpdateManifest.Parse(null));
            Assert.IsNull(UpdateManifest.Parse(@"{
              ""format"": ""zip"", ""version"": ""1.0.1"", ""releaseDate"": ""2026-09-27"",
              ""downloadUri"": ""https://example.com/a.zip"", ""sha256"": ""ZZZ"",
              ""signatureArmored"": ""x""}"), "a non-hex sha256 must not parse");
        }

        [Test]
        public void ManifestFromAnHttpUriIsRejected()
        {
            // plain http lets anyone on the path swap the payload; only https plus the signature
            // is the intended trust story.
            var manifest = @"{
              ""format"": ""zip"", ""version"": ""1.0.1"", ""releaseDate"": ""2026-09-27"",
              ""downloadUri"": ""http://example.com/a.zip"", ""sha256"": """ + new string('b', 64) + @""",
              ""signatureArmored"": ""x""}";

            Assert.IsNull(UpdateManifest.Parse(manifest), "http download URIs must be refused");
        }
    }
}
