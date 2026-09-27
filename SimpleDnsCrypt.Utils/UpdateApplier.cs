using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    /// Where an update attempt stopped.
    /// </summary>
    public enum UpdateApplyStatus
    {
        /// <summary>Downloaded, hashed, signature-checked and extracted.</summary>
        Applied,

        /// <summary>The manifest names something that cannot be written as a plain file.</summary>
        UnsafeFileName,

        /// <summary>The download itself failed - no connection, HTTP error, DNS failure.</summary>
        DownloadFailed,

        /// <summary>The download produced nothing.</summary>
        EmptyDownload,

        /// <summary>The bytes do not hash to what the manifest says.</summary>
        ChecksumMismatch,

        /// <summary>No minisign signature by the trusted key covers these bytes.</summary>
        SignatureInvalid,

        /// <summary>Verified, but unpacking failed.</summary>
        ExtractionFailed
    }

    public sealed class UpdateApplyResult
    {
        private UpdateApplyResult(UpdateApplyStatus status, string artifactPath, string extractedPath)
        {
            Status = status;
            ArtifactPath = artifactPath;
            ExtractedPath = extractedPath;
        }

        public UpdateApplyStatus Status { get; }

        /// <summary>Location of the signed zip kept beside the extraction, as evidence.</summary>
        public string ArtifactPath { get; }

        /// <summary>Folder holding the unpacked release, or null when nothing was extracted.</summary>
        public string ExtractedPath { get; }

        public bool IsApplied => Status == UpdateApplyStatus.Applied;

        internal static UpdateApplyResult Failed(UpdateApplyStatus status, string artifactPath = null)
        {
            return new UpdateApplyResult(status, artifactPath, null);
        }

        internal static UpdateApplyResult Applied(string artifactPath, string extractedPath)
        {
            return new UpdateApplyResult(UpdateApplyStatus.Applied, artifactPath, extractedPath);
        }
    }

    /// <summary>
    /// Carries a manifest's release onto disk, only after the bytes match the manifest hash AND a
    /// signature from the trusted key covers them.
    ///
    /// Nothing about this replaces the running program: the unpacked files land in a sibling
    /// _update folder for the user to copy over. The executable that is running right now cannot
    /// overwrite itself, and a detached replacer is a separate, deliberate step.
    /// </summary>
    public static class UpdateApplier
    {
        /// <summary>Folder under the install root that holds staged updates.</summary>
        public const string StagingFolderName = "_update";

        public static async Task<UpdateApplyResult> ApplyAsync(
            UpdateManifest manifest,
            string installRoot,
            Func<CancellationToken, Task<byte[]>> downloadArtifact,
            string trustedPublicKey = UpdateChannel.TrustedPublicKey,
            CancellationToken cancellationToken = default)
        {
            var fileName = SafeName(manifest.DownloadUri?.LocalPath);
            var versionFolder = SafeVersionFolder(manifest.Version);

            if (fileName == null || versionFolder == null)
            {
                return UpdateApplyResult.Failed(UpdateApplyStatus.UnsafeFileName);
            }

            var staged = Path.Combine(installRoot, StagingFolderName, versionFolder);
            var artifactPath = Path.Combine(staged, fileName);

            byte[] bytes;

            try
            {
                bytes = await downloadArtifact(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // No connection, a proxy in the way, a 404 because the tag moved: ordinary, and the
                // caller reports it in one line rather than unwinding a stack.
                return UpdateApplyResult.Failed(UpdateApplyStatus.DownloadFailed);
            }

            if (bytes == null || bytes.Length == 0)
            {
                return UpdateApplyResult.Failed(UpdateApplyStatus.EmptyDownload);
            }

            Directory.CreateDirectory(staged);

            try
            {
                File.WriteAllBytes(artifactPath, bytes);

                if (!HashMatches(artifactPath, manifest.Sha256))
                {
                    Discard(staged);
                    return UpdateApplyResult.Failed(UpdateApplyStatus.ChecksumMismatch);
                }

                // The signature is written next to the payload only so the pair is inspectable after
                // the fact; verification itself reads the armored text from the manifest.
                File.WriteAllText(artifactPath + ".minisig", manifest.SignatureArmored);

                if (!UpdateArtifactVerifier.VerifyFile(artifactPath, manifest.SignatureArmored, trustedPublicKey))
                {
                    Discard(staged);
                    return UpdateApplyResult.Failed(UpdateApplyStatus.SignatureInvalid);
                }

                var extracted = Path.Combine(staged, "payload");

                if (Directory.Exists(extracted))
                {
                    Directory.Delete(extracted, true);
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(artifactPath, extracted);

                return UpdateApplyResult.Applied(artifactPath, extracted);
            }
            catch (Exception exception)
            {
                if (exception is InvalidDataException || exception is IOException ||
                    exception is UnauthorizedAccessException || exception is ArgumentException)
                {
                    Discard(staged);
                    return UpdateApplyResult.Failed(UpdateApplyStatus.ExtractionFailed);
                }

                throw;
            }
        }

        /// <summary>
        /// The published zip's own name, or null when the manifest points at something that cannot be
        /// written into a single file. Only the leaf is used, and it is what the signature's trusted
        /// comment has to name, so a manifest cannot redirect the download elsewhere on disk.
        /// </summary>
        private static string SafeName(string uriLocalPath)
        {
            if (string.IsNullOrWhiteSpace(uriLocalPath))
            {
                return null;
            }

            var name = Path.GetFileName(uriLocalPath);

            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return name;
        }

        /// <summary>
        /// The version as a folder name. Parsed rather than echoed, because the value comes from a
        /// remote document and is joined into a path.
        /// </summary>
        private static string SafeVersionFolder(string version)
        {
            return Version.TryParse(version, out var parsed) ? parsed.ToString() : null;
        }

        private static bool HashMatches(string path, string expectedSha256)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var actual = sha.ComputeHash(stream);

                if (string.Equals(Convert.ToHexString(actual), expectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Discard(string staged)
        {
            try
            {
                if (Directory.Exists(staged))
                {
                    Directory.Delete(staged, true);
                }
            }
            catch (IOException)
            {
                // A scanner holding the just-written zip open. Leaving a rejected artifact behind is
                // untidy but harmless: nothing was extracted and nothing will be run from there.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
