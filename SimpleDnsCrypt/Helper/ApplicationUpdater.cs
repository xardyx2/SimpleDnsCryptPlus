using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Utils;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleDnsCrypt.Helper
{
    /// <summary>
    ///     Talks to this project's own release channel: fetches the update manifest, decides whether
    ///     it is newer, and hands a chosen release to <see cref="UpdateApplier"/> for verification.
    ///
    ///     Nothing here throws at the caller. A missing update is the normal answer, and a broken
    ///     network must not be able to stop the application from starting.
    /// </summary>
    public static class ApplicationUpdater
    {
        private static readonly ILog Log = LogManagerHelper.Factory();

        private static readonly HttpClient ManifestClient = NewClient(TimeSpan.FromSeconds(30));

        /// <summary>
        ///     A release zip is tens of megabytes and may arrive over a slow link. HttpClient's
        ///     100-second default would abort downloads that are working fine, so this client has no
        ///     timeout of its own and relies on the caller's cancellation token.
        /// </summary>
        private static readonly HttpClient ArtifactClient = NewClient(Timeout.InfiniteTimeSpan);

        private static HttpClient NewClient(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Simple-DNSCrypt-Plus updater");
            return client;
        }

        /// <summary>
        ///     The version this running build reports, which is what a manifest is compared against.
        /// </summary>
        public static Version CurrentVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        ///     The manifest for the architecture this process is running as.
        /// </summary>
        public static Uri ManifestUri { get; } = new Uri(
            Environment.Is64BitProcess ? Global.ApplicationUpdateUri64 : Global.ApplicationUpdateUri);

        /// <summary>
        ///     Returns the newer release this build should offer, or null when there is nothing to
        ///     offer - up to date, unreadable manifest, or unreachable server.
        /// </summary>
        public static async Task<UpdateManifest> CheckForUpdateAsync(CancellationToken token = default)
        {
            try
            {
                var json = await ManifestClient.GetStringAsync(ManifestUri, token).ConfigureAwait(false);

                var manifest = UpdateManifest.Parse(json);

                if (manifest == null)
                {
                    Log.Info("The update manifest at " + ManifestUri + " was not understood; ignoring it.");
                    return null;
                }

                if (!UpdateChecker.IsNewer(manifest.Version, CurrentVersion))
                {
                    return null;
                }

                return manifest;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                return null;
            }
        }

        /// <summary>
        ///     Downloads a manifest's release next to this installation and verifies it before
        ///     unpacking. The running program is never overwritten - see <see cref="UpdateApplier"/>.
        /// </summary>
        public static Task<UpdateApplyResult> StageUpdateAsync(UpdateManifest manifest,
            CancellationToken token = default)
        {
            return UpdateApplier.ApplyAsync(manifest, Global.InstallPath,
                ct => ArtifactClient.GetByteArrayAsync(manifest.DownloadUri, ct),
                cancellationToken: token);
        }
    }
}
