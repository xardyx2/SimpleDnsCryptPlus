using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Minisign;
using Newtonsoft.Json;
using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    /// The download step is where a hostile network position actually gets to choose bytes, so this
    /// asserts the two independent checks both bite: the manifest hash and the signature from the
    /// trusted key. A hash match alone must never be enough to unpack something.
    /// </summary>
    public class UpdateApplierTests
    {
        private const string ReleaseZipName = "SimpleDNSCryptPlus-x64-1.0.1-portable.zip";

        private string dir;
        private string installRoot;
        private string pubKey;
        private Minisign.Models.MinisignPrivateKey key;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "sdc-apply-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            installRoot = Path.Combine(dir, "app");
            Directory.CreateDirectory(installRoot);

            var pair = Core.GenerateKeyPair("test-passphrase", true, dir, "release");
            pubKey = File.ReadAllText(Path.Combine(dir, "release.pub"));
            key = pair.MinisignPrivateKey;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>A zip with one marker file in it, standing in for a published portable release.</summary>
        private byte[] BuildReleaseZip(string localName = ReleaseZipName, string marker = "the app")
        {
            var source = Path.Combine(dir, "payload-source-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "SimpleDnsCryptPlus.exe"), marker);
            File.WriteAllText(Path.Combine(source, "README.md"), "release notes");

            var zipPath = Path.Combine(dir, localName);
            ZipFile.CreateFromDirectory(source, zipPath);

            return File.ReadAllBytes(zipPath);
        }

        private string ArmoredSignatureFor(byte[] zipBytes)
        {
            // The signature is bound to a file name, so sign a copy carrying the published name.
            var toSign = Path.Combine(dir, "signing", ReleaseZipName);
            Directory.CreateDirectory(Path.GetDirectoryName(toSign));
            File.WriteAllBytes(toSign, zipBytes);

            var sigPath = Core.SignHashed(toSign, key, "Simple DNSCrypt Plus release",
                "file:" + ReleaseZipName + " hashed", Path.GetDirectoryName(toSign));

            return File.ReadAllText(sigPath);
        }

        private static string ManifestJson(string downloadUri, byte[] zipBytes, string armored)
        {
            return JsonConvert.SerializeObject(new
            {
                format = "zip",
                version = "1.0.1",
                releaseDate = "2026-09-27",
                downloadUri = downloadUri,
                sha256 = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant(),
                signatureArmored = armored
            });
        }

        private static string SignedDownloadUri(string zipName)
        {
            return "https://github.com/xardyx2/SimpleDnsCryptPlus/releases/download/v1.0.1/" + zipName;
        }

        private static Func<CancellationToken, Task<byte[]>> Returns(byte[] bytes)
        {
            return _ => Task.FromResult(bytes);
        }

        [Test]
        public async Task AnArtifactThatMatchesTheManifestAndOurKeyIsExtracted()
        {
            var zipBytes = BuildReleaseZip();
            var manifest = UpdateManifest.Parse(
                ManifestJson(SignedDownloadUri(ReleaseZipName), zipBytes, ArmoredSignatureFor(zipBytes)));

            Assert.IsNotNull(manifest, "the fixture manifest must parse");

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot, Returns(zipBytes), pubKey);

            Assert.IsTrue(result.IsApplied, "status was " + result.Status);
            Assert.IsNotNull(result.ExtractedPath);
            Assert.IsTrue(File.Exists(Path.Combine(result.ExtractedPath, "SimpleDnsCryptPlus.exe")),
                "the release should be unpacked into the staging folder");
            Assert.IsTrue(File.Exists(result.ArtifactPath),
                "the signed zip stays on disk as evidence of what was verified");
        }

        [Test]
        public async Task BytesThatDoNotMatchTheManifestHashAreNeverExtracted()
        {
            var zipBytes = BuildReleaseZip();
            var armored = ArmoredSignatureFor(zipBytes);

            // A manifest that promises one release and delivers another: the hash has to stop it.
            var other = BuildReleaseZip("promised.zip", "a different release");
            var manifest = UpdateManifest.Parse(
                ManifestJson(SignedDownloadUri(ReleaseZipName), other, armored));

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot, Returns(zipBytes), pubKey);

            Assert.AreEqual(UpdateApplyStatus.ChecksumMismatch, result.Status);
            Assert.IsNull(result.ExtractedPath);

            var staged = Path.Combine(installRoot, UpdateApplier.StagingFolderName, "1.0.1");
            Assert.IsFalse(Directory.Exists(staged),
                "a rejected download must not leave a tree behind");
            Assert.IsFalse(File.Exists(Path.Combine(staged, ReleaseZipName)),
                "the rejected bytes must not stay on disk either");
        }

        [Test]
        public async Task BytesWhoseHashMatchesButThatNoTrustedKeySignedAreRejected()
        {
            var zipBytes = BuildReleaseZip();

            // The attacker controls the manifest, so the hash is trivially satisfied; only the
            // signature can tell this apart from a real release.
            var foreign = Core.GenerateKeyPair("other", true, dir, "foreign");
            var foreignKey = foreign.MinisignPrivateKey;

            var toSign = Path.Combine(dir, "forged", ReleaseZipName);
            Directory.CreateDirectory(Path.GetDirectoryName(toSign));
            File.WriteAllBytes(toSign, zipBytes);
            var forgedSigPath = Core.SignHashed(toSign, foreignKey, "forged",
                "file:" + ReleaseZipName + " hashed", Path.GetDirectoryName(toSign));

            var manifest = UpdateManifest.Parse(
                ManifestJson(SignedDownloadUri(ReleaseZipName), zipBytes, File.ReadAllText(forgedSigPath)));

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot, Returns(zipBytes), pubKey);

            Assert.AreEqual(UpdateApplyStatus.SignatureInvalid, result.Status);
            Assert.IsNull(result.ExtractedPath);
        }

        [Test]
        public async Task AnEmptyDownloadIsRejectedBeforeAnythingIsWritten()
        {
            var zipBytes = BuildReleaseZip();
            var manifest = UpdateManifest.Parse(
                ManifestJson(SignedDownloadUri(ReleaseZipName), zipBytes, ArmoredSignatureFor(zipBytes)));

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot,
                Returns(Array.Empty<byte>()), pubKey);

            Assert.AreEqual(UpdateApplyStatus.EmptyDownload, result.Status);
            Assert.IsNull(result.ArtifactPath);
        }

        [Test]
        public async Task AManifestNamingNoFileIsRejected()
        {
            var zipBytes = BuildReleaseZip();
            var manifest = UpdateManifest.Parse(ManifestJson(
                "https://github.com/xardyx2/SimpleDnsCryptPlus/releases/download/v1.0.1/",
                zipBytes, ArmoredSignatureFor(zipBytes)));

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot, Returns(zipBytes), pubKey);

            Assert.AreEqual(UpdateApplyStatus.UnsafeFileName, result.Status);
        }

        [Test]
        public async Task ADownloadThatThrowsIsReportedRatherThanPropagated()
        {
            // A dead link is the ordinary case - offline laptop, blocked GitHub, expired URL - and
            // the caller should not need a try/catch to keep the app running.
            var zipBytes = BuildReleaseZip();
            var manifest = UpdateManifest.Parse(
                ManifestJson(SignedDownloadUri(ReleaseZipName), zipBytes, ArmoredSignatureFor(zipBytes)));

            var result = await UpdateApplier.ApplyAsync(manifest, installRoot,
                _ => Task.FromException<byte[]>(new IOException("no route to host")), pubKey);

            Assert.AreEqual(UpdateApplyStatus.DownloadFailed, result.Status);
            Assert.IsFalse(Directory.Exists(Path.Combine(installRoot, UpdateApplier.StagingFolderName, "1.0.1")));
        }

        [Test]
        public void CallersWhoDoNotChooseAKeyGetTheCompiledTrustedKey()
        {
            // The point of the default is that no call site can quietly verify against some other
            // key, so pin what the default actually is.
            var method = typeof(UpdateApplier).GetMethod(nameof(UpdateApplier.ApplyAsync));
            var parameters = method.GetParameters();

            var trusted = Array.Find(parameters, p => p.Name == "trustedPublicKey");

            Assert.IsNotNull(trusted);
            Assert.AreEqual(UpdateChannel.TrustedPublicKey, trusted.DefaultValue,
                "the default verification key must be the key this build ships with");
        }
    }
}
