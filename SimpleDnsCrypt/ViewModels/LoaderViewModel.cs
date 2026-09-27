using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Reactive.Bindings;
using WPFLocalizeExtension.Engine;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace SimpleDnsCrypt.ViewModels
{
    [Export(typeof(LoaderViewModel))]
    public class LoaderViewModel : Screen
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _events;
        private static readonly ILog Log = LogManagerHelper.Factory();
        private readonly MainViewModel _mainViewModel;
        private readonly SystemTrayViewModel _systemTrayViewModel;

        private string _progressText;
        private string _titleText;

        public LoaderViewModel()
        {
            Failed = new ReactiveProperty<bool>(false);
            ExitCommand = ReactiveCommand.Create(() => Application.Current.Shutdown());
        }

        public ICommand ExitCommand { get; }

        public ReactiveProperty<bool> Failed { get; }

        private async Task InitializeApplication()
        {
            try
            {
                if (IsAdministrator())
                {
                    ProgressText =
                        LocalizationEx.GetUiString("loader_administrative_rights_available", Thread.CurrentThread.CurrentCulture);
                }
                else
                {
                    ProgressText =
                        LocalizationEx.GetUiString("loader_administrative_rights_missing", Thread.CurrentThread.CurrentCulture);
                    Failed.Value = true;
                    return;
                }

                ProgressText =
                    string.Format(LocalizationEx.GetUiString("loader_validate_folder", Thread.CurrentThread.CurrentCulture),
                        Global.DnsCryptProxyFolderName);

                var validatedFolder = ValidateDnsCryptProxyFolder();

                if (validatedFolder.Any())
                {
                    var fileErrors = "";
                    foreach (var pair in validatedFolder)
                    {
                        fileErrors += $"{pair.Key}: {pair.Value}\n";
                    }

                    ProgressText =
                        string.Format(
                            LocalizationEx.GetUiString("loader_missing_files", Thread.CurrentThread.CurrentCulture).Replace("\\n", "\n"),
                            Global.DnsCryptProxyFolderName, fileErrors, Global.ApplicationName);
                    Failed.Value = true;
                    return;
                }

                ProgressText = LocalizationEx.GetUiString("loader_all_files_available", Thread.CurrentThread.CurrentCulture);

                PatchHelper.Patch();

                ProgressText = string.Format(LocalizationEx.GetUiString("loader_loading", Thread.CurrentThread.CurrentCulture),
                    Global.DnsCryptConfigurationFileName);
                if (DnscryptProxyConfigurationManager.LoadConfiguration())
                {
                    ProgressText =
                        string.Format(LocalizationEx.GetUiString("loader_successfully_loaded", Thread.CurrentThread.CurrentCulture),
                            Global.DnsCryptConfigurationFileName);
                    _mainViewModel.DnscryptProxyConfiguration = DnscryptProxyConfigurationManager.DnscryptProxyConfiguration;
                }
                else
                {
                    ProgressText =
                        string.Format(LocalizationEx.GetUiString("loader_failed_loading", Thread.CurrentThread.CurrentCulture),
                            Global.DnsCryptConfigurationFileName);
                    Failed.Value = true;
                    return;
                }

                ProgressText = LocalizationEx.GetUiString("loader_loading_network_cards", Thread.CurrentThread.CurrentCulture);

                var dnsServer = DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.Contains(Global.GlobalResolver)
                    ? new List<string>
                    {
                        Global.DefaultResolverIpv4,
                        Global.DefaultResolverIpv6
                    }
                    : DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList();
                var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(dnsServer);

                _mainViewModel.LocalNetworkInterfaces = new BindableCollection<LocalNetworkInterface>();
                _mainViewModel.LocalNetworkInterfaces.AddRange(localNetworkInterfaces);
                _mainViewModel.Initialize();
                ProgressText = LocalizationEx.GetUiString("loader_starting", Thread.CurrentThread.CurrentCulture);

                if (Properties.Settings.Default.TrayMode)
                {
                    await Execute.OnUIThreadAsync(() => _windowManager.ShowWindowAsync(_systemTrayViewModel));
                    if (Properties.Settings.Default.StartInTray)
                    {
                        Execute.OnUIThread(() => _systemTrayViewModel.HideWindow());
                    }
                    else
                    {
                        await Execute.OnUIThreadAsync(() => _windowManager.ShowWindowAsync(_mainViewModel));
                    }
                }
                else
                {
                    await Execute.OnUIThreadAsync(() => _windowManager.ShowWindowAsync(_mainViewModel));
                }

                await TryCloseAsync(true);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                Failed.Value = true;
            }
        }

        [ImportingConstructor]
        public LoaderViewModel(IWindowManager windowManager, IEventAggregator events) : this()
        {
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            _windowManager = windowManager;
            _events = events;
            _events.SubscribeOnPublishedThread(this);
            _titleText = $"{Global.ApplicationName} {VersionHelper.PublishVersion} {VersionHelper.PublishBuild}";
            LocalizeDictionary.Instance.SetCurrentThreadCulture = true;
            var languages = LocalizationEx.GetSupportedLanguages();
            if (!string.IsNullOrEmpty(Properties.Settings.Default.PreferredLanguage))
            {
                Log.Info($"Preferred language: {Properties.Settings.Default.PreferredLanguage}");
                var preferredLanguage = languages.FirstOrDefault(l => l.ShortCode.Equals(Properties.Settings.Default.PreferredLanguage));
                LocalizeDictionary.Instance.Culture = preferredLanguage != null ? new CultureInfo(preferredLanguage.CultureCode) : Thread.CurrentThread.CurrentCulture;
            }
            else
            {
                var language = languages.FirstOrDefault(l =>
                    l.ShortCode.Equals(Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName));
                if (language != null)
                {
                    Log.Info($"Using {language.ShortCode} as language");
                    LocalizeDictionary.Instance.Culture = new CultureInfo(language.CultureCode);
                }
                else
                {
                    Log.Warn($"Translation for {Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} is not available");
                    LocalizeDictionary.Instance.Culture = new CultureInfo("en");
                }
            }

            var selectedLanguage = languages.SingleOrDefault(l => l.ShortCode.Equals(LocalizeDictionary.Instance.Culture.TwoLetterISOLanguageName)) ??
                                   languages.SingleOrDefault(l => l.ShortCode.Equals(LocalizeDictionary.Instance.Culture.Name));


            _mainViewModel = new MainViewModel(_windowManager, _events)
            {
                Languages = languages,
                SelectedLanguage = selectedLanguage
            };
            _systemTrayViewModel = new SystemTrayViewModel(_windowManager, _events, _mainViewModel);
            Failed = new ReactiveProperty<bool>(false);

            _ = InitializeApplication();
        }

        public string TitleText
        {
            get => _titleText;
            set
            {
                _titleText = value;
                NotifyOfPropertyChange(() => TitleText);
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                NotifyOfPropertyChange(() => ProgressText);
            }
        }

        /// <summary>
        ///     Check if the current user has administrative privileges.
        /// </summary>
        /// <returns><c>true</c> if the user has administrative privileges, otherwise <c>false</c></returns>
        public static bool IsAdministrator()
        {
            try
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Check the dnscrypt-proxy directory on completeness.
        /// </summary>
        /// <returns><c>true</c> if all files are available, otherwise <c>false</c></returns>
        private static Dictionary<string, string> ValidateDnsCryptProxyFolder()
        {
            var report = new Dictionary<string, string>();
            foreach (var proxyFile in Global.DnsCryptProxyFiles)
            {
                var proxyFilePath = Path.Combine(Global.DnsCryptFolderPath, proxyFile);
                if (!File.Exists(proxyFilePath))
                {
                    report[proxyFile] = LocalizationEx.GetUiString("loader_missing", Thread.CurrentThread.CurrentCulture);
                }
            }

            return report;
        }
    }
}