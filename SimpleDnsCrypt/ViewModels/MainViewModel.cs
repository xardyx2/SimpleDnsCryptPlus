using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Extensions;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using ReactiveUI;
using SimpleDnsCrypt.Utils.Models;
using ReactiveCommand = ReactiveUI.ReactiveCommand;
using TabControl = System.Windows.Controls.TabControl;

namespace SimpleDnsCrypt.ViewModels
{
    public enum Tabs
    {
        MainTab,
        ResolverTab,
        AdvancedSettingsTab,
        QueryLogTab,
        DomainBlockLogTab,
        DomainBlacklistTab,
        AddressBlockLogTab,
        AddressBlacklistTab,
        CloakAndForwardTab
    }

    [Export(typeof(MainViewModel))]
    public class MainViewModel : Screen, INotifyDataErrorInfo
    {
        private static readonly ILog Log = LogManagerHelper.Factory();
        private readonly IEventAggregator _events;
        private readonly IWindowManager _windowManager;
        private AddressBlacklistViewModel _addressBlacklistViewModel;
        private AddressBlockLogViewModel _addressBlockLogViewModel;
        private DnscryptProxyConfiguration _dnscryptProxyConfiguration;
        private DomainBlacklistViewModel _domainBlacklistViewModel;
        private DomainBlockLogViewModel _domainBlockLogViewModel;
        private CloakAndForwardViewModel _cloakAndForwardViewModel;
        private SettingsViewModel _settingsViewModel;
        private QueryLogViewModel _queryLogViewModel;
        private ListenAddressesViewModel _listenAddressesViewModel;
        private FallbackResolversViewModel _fallbackResolversViewModel;
        private RouteViewModel _routeViewModel;
        private ProxiesViewModel _proxiesViewModel;
        private bool _isDnsCryptAutomaticModeEnabled;
        private bool _isOperatingAsGlobalResolver;
        private bool _isResolverRunning;
        private bool _isServiceInstalled;
        private bool _isSavingConfiguration;
        private bool _isUninstallingService;
        private bool _isWorkingOnService;
        private int _windowWidth;
        private int _windowHeight;

        private ObservableCollection<Language> _languages;

        private BindableCollection<LocalNetworkInterface> _localNetworkInterfaces =
            new BindableCollection<LocalNetworkInterface>();
        private BindableCollection<AvailableResolver> _resolvers;
        private readonly BindableCollection<StampFileEntry> _relays;
        private Language _selectedLanguage;
        private int _selectedTabIndex;

        private bool _showHiddenCards;
        private string _windowTitle;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MainViewModel" /> class
        /// </summary>
        /// <param name="windowManager">The window manager</param>
        /// <param name="events">The events</param>
        [ImportingConstructor]
        public MainViewModel(IWindowManager windowManager, IEventAggregator events)
        {
            Instance = this;
            _windowManager = windowManager;
            _events = events;
            _events.SubscribeOnPublishedThread(this);
            _windowWidth = Properties.Settings.Default.WindowWidth;
            _windowHeight = Properties.Settings.Default.WindowHeight;
            _windowTitle =
                $"{Global.ApplicationName} {VersionHelper.PublishVersion} {VersionHelper.PublishBuild} [dnscrypt-proxy {DnsCryptProxyManager.GetVersion()}]";
            SelectedTab = Tabs.MainTab;
            _isSavingConfiguration = false;
            _isWorkingOnService = false;

            _settingsViewModel = new SettingsViewModel(_windowManager, _events)
            {
                WindowTitle = LocalizationEx.GetUiString("settings", Thread.CurrentThread.CurrentCulture)
            };

            _settingsViewModel.PropertyChanged += SettingsViewModelOnPropertyChanged;
            _listenAddressesViewModel = new ListenAddressesViewModel();
            _fallbackResolversViewModel = new FallbackResolversViewModel();
            _routeViewModel = new RouteViewModel();
            _proxiesViewModel = new ProxiesViewModel(_windowManager, _events);
            _queryLogViewModel = new QueryLogViewModel(_windowManager, _events);
            _domainBlockLogViewModel = new DomainBlockLogViewModel();
            _domainBlacklistViewModel = new DomainBlacklistViewModel(_windowManager, _events);
            _addressBlockLogViewModel = new AddressBlockLogViewModel(_windowManager, _events);
            _addressBlacklistViewModel = new AddressBlacklistViewModel(_windowManager, _events);
            _cloakAndForwardViewModel = new CloakAndForwardViewModel(_windowManager, _events);
            Resolvers = new BindableCollection<AvailableResolver>();
            ResolverFilter.Throttle(TimeSpan.FromMilliseconds(100))
               .ObserveOnUIDispatcher()
               .Subscribe(_ => CollectionViewSource.GetDefaultView(Resolvers).Refresh());
            _relays = new BindableCollection<StampFileEntry>();
            UninstallServiceCommand = ReactiveCommand.CreateFromTask(UninstallService);
            InstallServiceCommand = ReactiveCommand.CreateFromTask(InstallService,
                this.ObservableForProperty(x => x.IsWorkingOnService, x => !x, true));
            SaveLocalServersCommand = ReactiveCommand.CreateFromTask(SaveLocalServers);
            AddCustomResolverFromStampCommand = ReactiveCommand.CreateFromTask(AddCustomResolverFromStamp);
        }

        public Tabs SelectedTab { get; set; }

        public static MainViewModel Instance { get; set; }

        public bool IsDnsCryptAutomaticModeEnabled
        {
            get => _isDnsCryptAutomaticModeEnabled;
            set
            {
                if (value.Equals(_isDnsCryptAutomaticModeEnabled)) return;
                _isDnsCryptAutomaticModeEnabled = value;
                if (_isDnsCryptAutomaticModeEnabled)
                {
                    foreach (var resolver in _resolvers)
                    {
                        resolver.IsInServerList = false;
                    }

                    Dispatcher.CurrentDispatcher.InvokeAsync(async Task() =>
                    {
                        await SaveDnsCryptConfiguration();
                        await LoadResolvers();
                        await HandleService();
                        NotifyOfPropertyChange(() => IsDnsCryptAutomaticModeEnabled);
                    });
                }
                else
                {
                    if (!_resolvers.Any(x => x.IsInServerList))
                    {
                        _isDnsCryptAutomaticModeEnabled = true;
                        _ = _windowManager.ShowMetroMessageBox(
                            LocalizationEx.GetUiString("message_content_no_server_selected", Thread.CurrentThread.CurrentCulture),
                            LocalizationEx.GetUiString("message_title_no_server_selected", Thread.CurrentThread.CurrentCulture),
                            MessageBoxButton.OK, BoxType.Warning);
                    }

                    NotifyOfPropertyChange(() => IsDnsCryptAutomaticModeEnabled);
                }
            }
        }

        public DomainBlacklistViewModel DomainBlacklistViewModel
        {
            get => _domainBlacklistViewModel;
            set
            {
                if (value.Equals(_domainBlacklistViewModel)) return;
                _domainBlacklistViewModel = value;
                NotifyOfPropertyChange(() => DomainBlacklistViewModel);
            }
        }

        public AddressBlacklistViewModel AddressBlacklistViewModel
        {
            get => _addressBlacklistViewModel;
            set
            {
                if (value.Equals(_addressBlacklistViewModel)) return;
                _addressBlacklistViewModel = value;
                NotifyOfPropertyChange(() => AddressBlacklistViewModel);
            }
        }

        public DomainBlockLogViewModel DomainBlockLogViewModel
        {
            get => _domainBlockLogViewModel;
            set
            {
                if (value.Equals(_domainBlockLogViewModel)) return;
                _domainBlockLogViewModel = value;
                NotifyOfPropertyChange(() => DomainBlockLogViewModel);
            }
        }

        public AddressBlockLogViewModel AddressBlockLogViewModel
        {
            get => _addressBlockLogViewModel;
            set
            {
                if (value.Equals(_addressBlockLogViewModel)) return;
                _addressBlockLogViewModel = value;
                NotifyOfPropertyChange(() => AddressBlockLogViewModel);
            }
        }

        public CloakAndForwardViewModel CloakAndForwardViewModel
        {
            get => _cloakAndForwardViewModel;
            set
            {
                if (value.Equals(_cloakAndForwardViewModel)) return;
                _cloakAndForwardViewModel = value;
                NotifyOfPropertyChange(() => CloakAndForwardViewModel);
            }
        }

        public QueryLogViewModel QueryLogViewModel
        {
            get => _queryLogViewModel;
            set
            {
                if (value.Equals(_queryLogViewModel)) return;
                _queryLogViewModel = value;
                NotifyOfPropertyChange(() => QueryLogViewModel);
            }
        }

        public SettingsViewModel SettingsViewModel
        {
            get => _settingsViewModel;
            set
            {
                if (value.Equals(_settingsViewModel)) return;
                _settingsViewModel = value;
                NotifyOfPropertyChange(() => SettingsViewModel);
            }
        }

        public ProxiesViewModel ProxiesViewModel
        {
            get => _proxiesViewModel;
            set
            {
                if (value.Equals(_proxiesViewModel)) return;
                _proxiesViewModel = value;
                NotifyOfPropertyChange(() => ProxiesViewModel);
            }
        }

        public FallbackResolversViewModel FallbackResolversViewModel
        {
            get => _fallbackResolversViewModel;
            set
            {
                if (value.Equals(_fallbackResolversViewModel)) return;
                _fallbackResolversViewModel = value;
                NotifyOfPropertyChange(() => FallbackResolversViewModel);
            }
        }

        public ListenAddressesViewModel ListenAddressesViewModel
        {
            get => _listenAddressesViewModel;
            set
            {
                if (value.Equals(_listenAddressesViewModel)) return;
                _listenAddressesViewModel = value;
                NotifyOfPropertyChange(() => ListenAddressesViewModel);
            }
        }

        public RouteViewModel RouteViewModel
        {
            get => _routeViewModel;
            set
            {
                if (value.Equals(_routeViewModel)) return;
                _routeViewModel = value;
                NotifyOfPropertyChange(() => RouteViewModel);
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                _selectedTabIndex = value;
                NotifyOfPropertyChange(() => SelectedTabIndex);
            }
        }

        public DnscryptProxyConfiguration DnscryptProxyConfiguration
        {
            get => _dnscryptProxyConfiguration;
            set
            {
                if (value.Equals(_dnscryptProxyConfiguration)) return;
                _dnscryptProxyConfiguration = value;
                NotifyOfPropertyChange(() => DnscryptProxyConfiguration);
            }
        }

        /// <summary>
        ///     The currently selected language.
        /// </summary>
        public Language SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (value.Equals(_selectedLanguage)) return;
                _selectedLanguage = value;
                Properties.Settings.Default.PreferredLanguage = _selectedLanguage.ShortCode;
                Properties.Settings.Default.Save();
                LocalizationEx.SetCulture(_selectedLanguage.ShortCode);
                NotifyOfPropertyChange(() => SelectedLanguage);
            }
        }

        /// <summary>
        ///     List of all available languages.
        /// </summary>
        public ObservableCollection<Language> Languages
        {
            get => _languages;
            set
            {
                if (value.Equals(_languages)) return;
                _languages = value;
                NotifyOfPropertyChange(() => Languages);
            }
        }

        public bool IsResolverRunning
        {
            get => _isResolverRunning;
            set
            {
                if (value != _isResolverRunning)
                {
                    Task.Run(async () =>
                    {
                        await HandleService();
                        NotifyOfPropertyChange(() => IsResolverRunning);
                    });
                }
            }
        }

        public bool IsServiceInstalled
        {
            get => _isServiceInstalled;
            set
            {
                _isServiceInstalled = value;
                NotifyOfPropertyChange(() => IsServiceInstalled);
            }
        }

        public bool IsSavingConfiguration
        {
            get => _isSavingConfiguration;
            set
            {
                _isSavingConfiguration = value;
                NotifyOfPropertyChange(() => IsSavingConfiguration);
            }
        }

        public bool IsOperatingAsGlobalResolver
        {
            get => _isOperatingAsGlobalResolver;
            set
            {
                _isOperatingAsGlobalResolver = value;
                DnscryptProxyConfiguration.listen_addresses = value
                    ? new ObservableCollection<string> { Global.GlobalResolver }
                    : new ObservableCollection<string> { Global.DefaultResolverIpv4, Global.DefaultResolverIpv6 };
                NotifyOfPropertyChange(() => IsOperatingAsGlobalResolver);
            }
        }

        public bool IsWorkingOnService
        {
            get => _isWorkingOnService;
            set
            {
                _isWorkingOnService = value;
                NotifyOfPropertyChange(() => IsWorkingOnService);
            }
        }

        public BindableCollection<LocalNetworkInterface> LocalNetworkInterfaces
        {
            get => _localNetworkInterfaces;
            set
            {
                _localNetworkInterfaces = value;
                NotifyOfPropertyChange(() => LocalNetworkInterfaces);
            }
        }

        public BindableCollection<AvailableResolver> Resolvers
        {
            get => _resolvers;
            set
            {
                var view = CollectionViewSource.GetDefaultView(value);
                view.Filter = o => FilterResolver((AvailableResolver)o);
                _resolvers = value;
                NotifyOfPropertyChange(() => Resolvers);
            }
        }

        public ReactiveProperty<string> ResolverFilter { get; set; } = new ReactiveProperty<string>("");

        /// <summary>
        /// Closing the main window.
        /// </summary>
        /// <param name="cancelEventArgs"></param>
        public void OnClose(CancelEventArgs cancelEventArgs)
        {
            try
            {
                Properties.Settings.Default.WindowWidth = WindowWidth;
                Properties.Settings.Default.WindowHeight = WindowHeight;
                Properties.Settings.Default.Save();
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }


        /// <summary>
        ///     The title of the window.
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                NotifyOfPropertyChange(() => WindowTitle);
            }
        }

        /// <summary>
        ///		The width of the main window.
        /// </summary>
        public int WindowWidth
        {
            get => _windowWidth;
            set
            {
                _windowWidth = value;
                NotifyOfPropertyChange(() => WindowWidth);
            }
        }

        /// <summary>
        ///		The height of the main window.
        /// </summary>
        public int WindowHeight
        {
            get => _windowHeight;
            set
            {
                _windowHeight = value;
                NotifyOfPropertyChange(() => WindowHeight);
            }
        }

        /// <summary>
        ///     Show or hide the filtered network cards.
        /// </summary>
        public bool ShowHiddenCards
        {
            get => _showHiddenCards;
            set
            {
                _showHiddenCards = value;
                ReloadLoadNetworkInterfaces();
                NotifyOfPropertyChange(() => ShowHiddenCards);
            }
        }

        public bool FilterResolver(AvailableResolver resolver)
        {
            var filter = ResolverFilter.Value;
            return string.IsNullOrWhiteSpace(filter) 
                || resolver.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase) 
                || resolver.Description.Contains(filter, StringComparison.InvariantCultureIgnoreCase);
        }

        public void Initialize()
        {
            if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
            {
                _isServiceInstalled = true;
                if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
                {
                    _isResolverRunning = true;
                }
            }
            else
            {
                _isServiceInstalled = false;
            }

            if (DnscryptProxyConfiguration != null && (DnscryptProxyConfiguration.server_names == null ||
                                                       DnscryptProxyConfiguration.server_names.Count == 0))
            {
                _isDnsCryptAutomaticModeEnabled = true;
            }
            else
            {
                _isDnsCryptAutomaticModeEnabled = false;
            }

            if (!string.IsNullOrEmpty(DnscryptProxyConfiguration?.query_log?.file))
            {
                QueryLogViewModel.IsQueryLogLogging = true;
            }

            if (!string.IsNullOrEmpty(DnscryptProxyConfiguration?.blocked_names?.log_file))
            {
                if (!File.Exists(DnscryptProxyConfiguration.blocked_names.log_file))
                {
                    File.Create(DnscryptProxyConfiguration.blocked_names.log_file).Dispose();
                }
                DomainBlockLogViewModel.IsDomainBlockLogLogging = true;
            }

            if (!string.IsNullOrEmpty(DnscryptProxyConfiguration?.blocked_names?.blocked_names_file))
            {
                if (!File.Exists(DnscryptProxyConfiguration.blocked_names.blocked_names_file))
                {
                    File.Create(DnscryptProxyConfiguration.blocked_names.blocked_names_file).Dispose();
                }
                DomainBlacklistViewModel.IsBlacklistEnabled = true;
            }

            if (DnscryptProxyConfiguration?.listen_addresses != null)
            {
                _isOperatingAsGlobalResolver = DnscryptProxyConfiguration.listen_addresses.Contains(Global.GlobalResolver);
            }

            if (DnscryptProxyConfiguration?.fallback_resolvers != null)
            {
                if (DnscryptProxyConfiguration.fallback_resolvers.Count > 0)
                {
                    FallbackResolvers = DnscryptProxyConfiguration.fallback_resolvers;
                }
            }

            if (!string.IsNullOrEmpty(DnscryptProxyConfiguration?.cloaking_rules))
            {
                if (!File.Exists(DnscryptProxyConfiguration.cloaking_rules))
                {
                    File.Create(DnscryptProxyConfiguration.cloaking_rules).Dispose();
                }
                CloakAndForwardViewModel.IsCloakingEnabled = true;
            }

            if (!string.IsNullOrEmpty(DnscryptProxyConfiguration?.forwarding_rules))
            {
                if (!File.Exists(DnscryptProxyConfiguration.forwarding_rules))
                {
                    File.Create(DnscryptProxyConfiguration.forwarding_rules).Dispose();
                }
                CloakAndForwardViewModel.IsForwardingEnabled = true;
            }
        }

        private void SettingsViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs != null)
            {
                if (propertyChangedEventArgs.PropertyName.Equals("IsInitialized") ||
                    propertyChangedEventArgs.PropertyName.Equals("IsActive")) return;

                switch (propertyChangedEventArgs.PropertyName)
                {
                    case "IsAdvancedSettingsTabVisible":
                        if (!SettingsViewModel.IsAdvancedSettingsTabVisible)
                            if (SelectedTab == Tabs.AdvancedSettingsTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsQueryLogTabVisible":
                        if (QueryLogViewModel.IsQueryLogLogging) QueryLogViewModel.IsQueryLogLogging = false;

                        if (!SettingsViewModel.IsQueryLogTabVisible)
                            if (SelectedTab == Tabs.QueryLogTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsDomainBlockLogLogging":
                        if (DomainBlockLogViewModel.IsDomainBlockLogLogging) DomainBlockLogViewModel.IsDomainBlockLogLogging = false;

                        if (!SettingsViewModel.IsDomainBlockLogTabVisible)
                            if (SelectedTab == Tabs.DomainBlockLogTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsAddressBlockLogLogging":
                        if (AddressBlockLogViewModel.IsAddressBlockLogLogging) AddressBlockLogViewModel.IsAddressBlockLogLogging = false;

                        if (!SettingsViewModel.IsAddressBlockLogTabVisible)
                            if (SelectedTab == Tabs.AddressBlockLogTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsDomainBlacklistTabVisible":
                        if (!SettingsViewModel.IsDomainBlacklistTabVisible)
                            if (SelectedTab == Tabs.DomainBlacklistTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsAddressBlacklistTabVisible":
                        if (!SettingsViewModel.IsAddressBlacklistTabVisible)
                            if (SelectedTab == Tabs.AddressBlacklistTab)
                                SelectedTabIndex = 0;
                        break;
                    case "IsCloakAndForwardTabVisible":
                        if (!SettingsViewModel.IsCloakAndForwardTabVisible)
                            if (SelectedTab == Tabs.CloakAndForwardTab)
                                SelectedTabIndex = 0;
                        break;
                }
            }
        }

        public async Task TabControl_SelectionChanged(SelectionChangedEventArgs selectionChangedEventArgs)
        {
            try
            {
                if (selectionChangedEventArgs.Source.GetType() != typeof(TabControl)) return;
                if (selectionChangedEventArgs.AddedItems.Count != 1) return;
                var tabItem = (TabItem)selectionChangedEventArgs.AddedItems[0];
                if (string.IsNullOrEmpty((string)tabItem.Tag)) return;

                switch ((string)tabItem.Tag)
                {
                    case "mainTab":
                        SelectedTab = Tabs.MainTab;
                        IsServiceInstalled = DnsCryptProxyManager.IsDnsCryptProxyInstalled();
                        _isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
                        NotifyOfPropertyChange(() => IsResolverRunning);
                        break;
                    case "resolverTab":
                        SelectedTab = Tabs.ResolverTab;
                        await LoadResolvers();
                        break;
                    case "advancedSettingsTab":
                        SelectedTab = Tabs.AdvancedSettingsTab;
                        break;
                    case "queryLogTab":
                        SelectedTab = Tabs.QueryLogTab;
                        break;
                    case "cloakAndForwardTab":
                        SelectedTab = Tabs.CloakAndForwardTab;
                        break;
                    case "domainBlockLogTab":
                        SelectedTab = Tabs.DomainBlockLogTab;
                        break;
                    case "domainBlacklistTab":
                        SelectedTab = Tabs.DomainBlacklistTab;
                        break;
                    case "addressBlockLogTab":
                        SelectedTab = Tabs.AddressBlockLogTab;
                        break;
                    case "addressBlacklistTab":
                        SelectedTab = Tabs.AddressBlacklistTab;
                        break;
                    default:
                        SelectedTab = Tabs.MainTab;
                        break;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        public void About()
        {
            var win = new AboutViewModel()
            {
                WindowTitle = LocalizationEx.GetUiString("about", Thread.CurrentThread.CurrentCulture)
            };
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _windowManager.ShowDialogAsync(win, null, settings);
        }

        public void Settings()
        {
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = _windowManager.ShowDialogAsync(SettingsViewModel, null, settings).Result;
            if (!result) Properties.Settings.Default.Save();
        }

        public void Proxies()
        {
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ProxiesViewModel.WindowTitle = LocalizationEx.GetUiString("proxy_manage_proxies", Thread.CurrentThread.CurrentCulture);
            ProxiesViewModel.HttpProxyInput = string.IsNullOrEmpty(DnscryptProxyConfiguration.http_proxy) ? "" : DnscryptProxyConfiguration.http_proxy;
            ProxiesViewModel.SocksProxyInput = string.IsNullOrEmpty(DnscryptProxyConfiguration.proxy) ? "" : DnscryptProxyConfiguration.proxy;
            var result = _windowManager.ShowDialogAsync(ProxiesViewModel, null, settings).Result;
            if (result) return;
            var saveAdvancedSettings = false;

            if (string.IsNullOrEmpty(ProxiesViewModel.HttpProxyInput))
            {
                if (!string.IsNullOrEmpty(DnscryptProxyConfiguration.http_proxy))
                {
                    DnscryptProxyConfiguration.http_proxy = null;
                    saveAdvancedSettings = true;
                }
            }
            else
            {
                DnscryptProxyConfiguration.http_proxy = ProxiesViewModel.HttpProxyInput;
                saveAdvancedSettings = true;
            }

            if (string.IsNullOrEmpty(ProxiesViewModel.SocksProxyInput))
            {
                if (!string.IsNullOrEmpty(DnscryptProxyConfiguration.proxy))
                {
                    DnscryptProxyConfiguration.proxy = null;
                    saveAdvancedSettings = true;
                }
            }
            else
            {
                DnscryptProxyConfiguration.proxy = ProxiesViewModel.SocksProxyInput;
                saveAdvancedSettings = true;
            }

            if (saveAdvancedSettings)
            {
                SaveAdvancedSettings();
            }
        }

        public void ManageFallbackResolvers()
        {
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var oldAddresses = DnscryptProxyConfiguration.listen_addresses.ToList();
            FallbackResolversViewModel.FallbackResolvers = DnscryptProxyConfiguration.fallback_resolvers;
            FallbackResolversViewModel.WindowTitle = LocalizationEx.GetUiString("advanced_settings_fallback_resolvers", Thread.CurrentThread.CurrentCulture);
            var result = _windowManager.ShowDialogAsync(FallbackResolversViewModel, null, settings).Result;
            if (!result)
            {
                if (FallbackResolversViewModel.FallbackResolvers.Count == 0) return;
                if (FallbackResolversViewModel.FallbackResolvers.ToHashSet().SetEquals(oldAddresses)) return;
                DnscryptProxyConfiguration.fallback_resolvers = FallbackResolversViewModel.FallbackResolvers;
                SaveAdvancedSettings();
            }
        }

        public async void ListenAddresses()
        {
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var oldAddresses = DnscryptProxyConfiguration.listen_addresses.ToList();
            ListenAddressesViewModel.ListenAddresses = DnscryptProxyConfiguration.listen_addresses;
            ListenAddressesViewModel.WindowTitle = LocalizationEx.GetUiString("address_settings_listen_addresses", Thread.CurrentThread.CurrentCulture);
            var result = _windowManager.ShowDialogAsync(ListenAddressesViewModel, null, settings).Result;
            if (!result)
            {
                var newAddresses = ListenAddressesViewModel.ListenAddresses.ToList();
                if (newAddresses.Count == 0) return;
                if (newAddresses.ToHashSet().SetEquals(oldAddresses)) return;
                DnscryptProxyConfiguration.listen_addresses = new ObservableCollection<string>(newAddresses);
                SaveAdvancedSettings();

                var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(oldAddresses.ToList());
                foreach (var localNetworkInterface in localNetworkInterfaces)
                {
                    localNetworkInterface.IsChangeable = false;
                    if (!localNetworkInterface.UseDnsCrypt) continue;
                    localNetworkInterface.UseDnsCrypt = localNetworkInterface.SetNameservers(LocalNetworkInterfaceManager.ConvertToDnsList(
                        newAddresses));
                    localNetworkInterface.IsChangeable = true;
                }
                await Task.Delay(1000).ConfigureAwait(false);
                ReloadLoadNetworkInterfaces();
            }
        }

        public async Task SaveDnsCryptConfiguration()
        {
            IsSavingConfiguration = true;
            try
            {
                if (DnscryptProxyConfiguration == null) return;
                DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = _dnscryptProxyConfiguration;

                DnscryptProxyConfiguration.server_names = new ObservableCollection<string>(_resolvers.Where(x => x.IsInServerList).Select(x => x.Name));
                IsDnsCryptAutomaticModeEnabled = !(DnscryptProxyConfiguration?.server_names?.Count > 0);

                if (DnscryptProxyConfigurationManager.SaveConfiguration())
                {
                    _dnscryptProxyConfiguration = DnscryptProxyConfigurationManager.DnscryptProxyConfiguration;
                    using var ws = new WorkingOnService(this);

                    if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
                    {
                        IsServiceInstalled = true;
                        await DnsCryptProxyManager.RestartIfRunning().ConfigureAwait(false);
                    }
                    else
                    {
                        IsServiceInstalled = false;
                    }
                }

                _isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
                NotifyOfPropertyChange(() => IsResolverRunning);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            finally
            {
                IsSavingConfiguration = false;
            }
        }

        private async Task HandleService()
        {
            using var ws = new WorkingOnService(this);
            if (IsResolverRunning)
            {
                // service is running, stop it
                await DnsCryptProxyManager.Stop().ConfigureAwait(false);
                _isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
                NotifyOfPropertyChange(() => IsResolverRunning);
            }
            else
            {
                if (DnsCryptProxyManager.IsDnsCryptProxyInstalled())
                {
                    IsServiceInstalled = true;
                    await DnsCryptProxyManager.Start().ConfigureAwait(false);
                    _isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
                    NotifyOfPropertyChange(() => IsResolverRunning);
                }
            }
        }

        public ICommand InstallServiceCommand { get; }

        private async Task<bool> InstallService()
        {
            using var ws = new WorkingOnService(this);
            await Task.Run(DnsCryptProxyManager.Install).ConfigureAwait(false);
            await Task.Delay(Global.ServiceInstallTime).ConfigureAwait(false);
            return IsServiceInstalled = DnsCryptProxyManager.IsDnsCryptProxyInstalled();
        }

        /// <summary>
        ///     Load the local network cards.
        /// </summary>
        private void ReloadLoadNetworkInterfaces()
        {
            var dnsServer = IsOperatingAsGlobalResolver
                ? new List<string>
                {
                    Global.DefaultResolverIpv4,
                    Global.DefaultResolverIpv6
                }
                : DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList();
            var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(dnsServer, ShowHiddenCards);

            _localNetworkInterfaces.Clear();

            if (localNetworkInterfaces.Count == 0) return;

            foreach (var localNetworkInterface in localNetworkInterfaces) _localNetworkInterfaces.Add(localNetworkInterface);
        }

        public async Task NetworkCardClicked(LocalNetworkInterface localNetworkInterface)
        {
            if (localNetworkInterface is not { IsChangeable: true }) return;
            localNetworkInterface.IsChangeable = false;
            if (localNetworkInterface.UseDnsCrypt)
            {
                var status = LocalNetworkInterfaceManager.UnsetNameservers(localNetworkInterface);
                localNetworkInterface.UseDnsCrypt = !status;
            }
            else
            {
                // only add the local address if the proxy is running 
                if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
                {
                    var dnsServer = IsOperatingAsGlobalResolver
                        ? new List<string>
                        {
                            Global.DefaultResolverIpv4,
                            Global.DefaultResolverIpv6
                        }
                        : DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList();
                    localNetworkInterface.UseDnsCrypt = localNetworkInterface.SetNameservers(LocalNetworkInterfaceManager.ConvertToDnsList(dnsServer));
                }
                else
                    _ = _windowManager.ShowMetroMessageBox(
                        LocalizationEx.GetUiString("message_content_service_not_running", Thread.CurrentThread.CurrentCulture),
                        LocalizationEx.GetUiString("message_title_service_not_running", Thread.CurrentThread.CurrentCulture),
                        MessageBoxButton.OK, BoxType.Warning);
            }

            await Task.Delay(1000).ConfigureAwait(false);
            localNetworkInterface.IsChangeable = true;
            ReloadLoadNetworkInterfaces();
        }

        /// <summary>
        ///     Open the applications log directory (Windows Explorer).
        /// </summary>
        public void OpenLogDirectory()
        {
            try
            {
                var logDirectory = Path.Combine(Global.InstallPath, Global.LogDirectoryName);
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                Process.Start(logDirectory);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        #region Advanced Settings

        public void SaveAdvancedSettings()
        {
            if (!HasErrors)
            {
                //TODO: do something about this
                _ = SaveDnsCryptConfiguration();
            }
        }

        #endregion

        #region Resolvers

        public ICommand SaveLocalServersCommand { get; }

        private async Task SaveLocalServers()
        {
            if (_resolvers.Any(x => x.IsInServerList))
            {
                IsDnsCryptAutomaticModeEnabled = false;
                await SaveDnsCryptConfiguration();
                await LoadResolvers();
            }
            else
            {
                _ = _windowManager.ShowMetroMessageBox(
                    LocalizationEx.GetUiString("message_content_no_server_selected", Thread.CurrentThread.CurrentCulture),
                    LocalizationEx.GetUiString("message_title_no_server_selected", Thread.CurrentThread.CurrentCulture),
                    MessageBoxButton.OK, BoxType.Warning);
            }
        }

        public ICommand AddCustomResolverFromStampCommand { get; }

        private async Task AddCustomResolverFromStamp()
        {
            var customResolverViewModel = new AddCustomResolverViewModel { ExistingNames = _resolvers.Select(x => x.Name).ToHashSet() };
            dynamic settings = new ExpandoObject();
            settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = await _windowManager.ShowDialogAsync(customResolverViewModel, null, (IDictionary<string, object>) settings);
            if (result == true)
            {
                DnscryptProxyConfiguration.@static.Add(customResolverViewModel.Name.Value, new CustomServer
                {
                    Stamp = customResolverViewModel.Stamp.Value
                });
                await SaveDnsCryptConfiguration();
                await LoadResolvers();
            }
        }

        public async Task HandleManageRoutes(AvailableResolver availableResolver)
        {
            try
            {
                if (availableResolver is not { Protocol: "DNSCrypt" }) return;
                dynamic settings = new ExpandoObject();
                settings.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                RouteViewModel.Route = new ObservableCollection<StampFileEntry>();
                if (availableResolver.Route?.via != null)
                {
                    for (var v = 0; v < availableResolver.Route.via.Count; v++)
                    {
                        var stampFileEntry = _relays.FirstOrDefault(r => r.Name.Equals(availableResolver.Route.via[v]));
                        if (stampFileEntry != null)
                        {
                            //add only entries that are present in relays
                            RouteViewModel.Route.Add(stampFileEntry);
                        }
                        else
                        {
                            //remove non-existent relays from route
                            availableResolver.Route.via.RemoveAt(v);
                        }
                    }
                }
                RouteViewModel.Relays = _relays;
                RouteViewModel.Resolver = availableResolver.DisplayName;
                var result = _windowManager.ShowDialogAsync(RouteViewModel, null, settings).Result;
                if (result) return;

                if (!RouteViewModel.Route.Any())
                {
                    //remove route
                    if (availableResolver.Route == null) return;
                    var oldRoute = _dnscryptProxyConfiguration.anonymized_dns.routes.FindIndex(r => r.server_name.Equals(availableResolver.Route.server_name));
                    if (oldRoute != -1)
                    {
                        _dnscryptProxyConfiguration.anonymized_dns.routes.RemoveAt(oldRoute);
                    }
                    await SaveDnsCryptConfiguration();
                    await LoadResolvers();
                }
                else
                {
                    var oldRoute = -1;
                    if (availableResolver.Route != null && !string.IsNullOrEmpty(availableResolver.Route.server_name))
                    {
                        oldRoute = _dnscryptProxyConfiguration.anonymized_dns.routes.FindIndex(r => r.server_name.Equals(availableResolver.Route.server_name));
                    }
                    if (oldRoute != -1)
                    {
                        //update
                        _dnscryptProxyConfiguration.anonymized_dns.routes[oldRoute].via = new ObservableCollection<string>();
                        foreach (var stampFileEntry in RouteViewModel.Route)
                        {
                            _dnscryptProxyConfiguration.anonymized_dns.routes[oldRoute].via ??= new ObservableCollection<string>();
                            _dnscryptProxyConfiguration.anonymized_dns.routes[oldRoute].via.Add(stampFileEntry.Name);
                        }
                    }
                    else
                    {
                        //create
                        var newRoute = new Route
                        {
                            server_name = availableResolver.Name,
                            via = new ObservableCollection<string>()
                        };
                        foreach (var stampFileEntry in RouteViewModel.Route)
                        {
                            newRoute.via.Add(stampFileEntry.Name);
                        }

                        _dnscryptProxyConfiguration.anonymized_dns ??= new AnonymizedDns();
                        _dnscryptProxyConfiguration.anonymized_dns.routes ??= new List<Route>();
                        _dnscryptProxyConfiguration.anonymized_dns.routes.Add(newRoute);
                    }
                    await SaveDnsCryptConfiguration();
                    await LoadResolvers();
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            finally
            {
                IsWorkingOnService = false;
            }
        }

        private void PrepareRoutes()
        {
            //anonymized_dns
            _relays.Clear();
            var relays = RelayHelper.GetRelays();
            if (relays is { Count: > 0 })
            {
                _relays.AddRange(relays);
            }
        }

        /// <summary>
        ///     Get the list of available resolvers for the enabled filters.
        /// </summary>
        /// <remarks>Current solution is not very effective.</remarks>
        private async Task LoadResolvers()
        {
            IsSavingConfiguration = true;
            try
            {
                PrepareRoutes();
                var availableResolvers = await DnsCryptProxyManager.GetAvailableResolvers();
                var allResolvers = await DnsCryptProxyManager.GetAllResolversWithoutFilters();
                var filteredResolvers = new List<AvailableResolver>();

                foreach (var resolver in allResolvers)
                {
                    if (_dnscryptProxyConfiguration.doh_servers)
                        if (!_dnscryptProxyConfiguration.dnscrypt_servers)
                            if (!resolver.Protocol.Equals("DoH"))
                                continue;

                    if (_dnscryptProxyConfiguration.dnscrypt_servers)
                        if (!_dnscryptProxyConfiguration.doh_servers)
                            if (!resolver.Protocol.Equals("DNSCrypt"))
                                continue;

                    if (!_dnscryptProxyConfiguration.doh_servers && !_dnscryptProxyConfiguration.dnscrypt_servers)
                        continue;

                    if (_dnscryptProxyConfiguration.require_dnssec)
                        if (!resolver.DnsSec)
                            continue;

                    if (_dnscryptProxyConfiguration.require_nofilter)
                        if (!resolver.NoFilter)
                            continue;

                    if (_dnscryptProxyConfiguration.require_nolog)
                        if (!resolver.NoLog)
                            continue;

                    if (resolver.Ipv6)
                        if (!_dnscryptProxyConfiguration.ipv6_servers)
                            continue;
                    filteredResolvers.Add(resolver);
                }

                var filteredResolversByName = filteredResolvers.DistinctBy(x => x.Name).ToDictionary(x => x.Name);

                var availableRelayNames = _relays?.Select(x => x.Name).ToHashSet();
                var configRoutesByServerName = _dnscryptProxyConfiguration.anonymized_dns?.routes?.DistinctBy(x => x.server_name).ToDictionary(x => x.server_name);
                foreach (var resolverName in availableResolvers.Select(x => x.Name))
                {
                    if (filteredResolversByName.TryGetValue(resolverName, out var matchingResolver))
                    {
                        if (configRoutesByServerName?.TryGetValue(matchingResolver.Name, out var route) == true)
                        {
                            matchingResolver.Route = route;
                            var valid = availableRelayNames?.IsSupersetOf(matchingResolver.Route.via) ?? false;
                            matchingResolver.RouteState = valid ? RouteState.Valid : RouteState.Invalid;
                        }

                        matchingResolver.IsInServerList = true;
                    }
                }

                if (_isDnsCryptAutomaticModeEnabled)
                {
                    foreach (var resolver in filteredResolvers)
                    {
                        resolver.IsInServerList = false;
                    }
                }

                Resolvers = new BindableCollection<AvailableResolver>(filteredResolvers.OrderBy(o => o.DisplayName));
            }
            finally
            {
                IsSavingConfiguration = false;
            }
        }

        #endregion

        #region Advanced Settings

        public ICommand UninstallServiceCommand { get; }

        /// <summary>
        ///     Uninstall the installed dnscrypt-proxy service.
        /// </summary>
        private async Task UninstallService()
        {
            var result = await _windowManager.ShowMetroMessageBox(
                LocalizationEx.GetUiString("dialog_message_uninstall", Thread.CurrentThread.CurrentCulture),
                LocalizationEx.GetUiString("dialog_uninstall_title", Thread.CurrentThread.CurrentCulture),
                MessageBoxButton.YesNo, BoxType.Default);

            if (result != MessageBoxResult.Yes) return;
            IsUninstallingService = true;

            if (DnsCryptProxyManager.IsDnsCryptProxyRunning())
            {
                await DnsCryptProxyManager.Stop().ConfigureAwait(false);
            }

            await Task.Run(() => { DnsCryptProxyManager.Uninstall(); }).ConfigureAwait(false);
            await Task.Delay(Global.ServiceUninstallTime).ConfigureAwait(false);
            _isResolverRunning = DnsCryptProxyManager.IsDnsCryptProxyRunning();
            NotifyOfPropertyChange(() => IsResolverRunning);

            // recover the network interfaces (also the hidden and down cards)
            var localNetworkInterfaces = LocalNetworkInterfaceManager.GetLocalNetworkInterfaces(
                DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.listen_addresses.ToList());
            foreach (var localNetworkInterface in localNetworkInterfaces)
            {
                if (!localNetworkInterface.UseDnsCrypt) continue;
                var status = localNetworkInterface.SetNameservers(new List<DnsServer>());
                var card = _localNetworkInterfaces.SingleOrDefault(n => n.Description.Equals(localNetworkInterface.Description));
                if (card != null) card.UseDnsCrypt = !status;
            }

            await Task.Delay(1000).ConfigureAwait(false);
            ReloadLoadNetworkInterfaces();
            IsUninstallingService = false;
            if (!DnsCryptProxyManager.IsDnsCryptProxyInstalled())
            {
                IsServiceInstalled = false;
                _ = _windowManager.ShowMetroMessageBox(
                    LocalizationEx.GetUiString("message_content_uninstallation_successful",
                        Thread.CurrentThread.CurrentCulture),
                    LocalizationEx.GetUiString("message_title_uninstallation_successful",
                        Thread.CurrentThread.CurrentCulture),
                    MessageBoxButton.OK, BoxType.Default);
            }
            else
            {
                IsServiceInstalled = true;
                _ = _windowManager.ShowMetroMessageBox(
                    LocalizationEx.GetUiString("message_content_uninstallation_error",
                        Thread.CurrentThread.CurrentCulture),
                    LocalizationEx.GetUiString("message_title_uninstallation_error",
                        Thread.CurrentThread.CurrentCulture),
                    MessageBoxButton.OK, BoxType.Warning);
            }
        }

        public bool IsUninstallingService
        {
            get => _isUninstallingService;
            set
            {
                _isUninstallingService = value;
                NotifyOfPropertyChange(() => IsUninstallingService);
            }
        }

        #endregion

        private ObservableCollection<string> _fallbackResolvers;
        public ObservableCollection<string> FallbackResolvers
        {
            get => _fallbackResolvers;
            set
            {
                _fallbackResolvers = value;
                ValidateFallbackResolvers();
                NotifyOfPropertyChange(() => FallbackResolvers);
            }
        }

        private readonly Dictionary<string, string> _validationErrors = new Dictionary<string, string>();

        public IEnumerable GetErrors(string propertyName)
        {
            return _validationErrors.TryGetValue(propertyName, out var value) ? new List<string>(1) { value } : null;
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool HasErrors => _validationErrors.Any();


        private void ValidateFallbackResolvers()
        {
            var validatedFallbackResolvers = new ObservableCollection<string>();
            var allValid = true;
            foreach (var fallbackResolver in _fallbackResolvers)
            {
                var validatedFallbackResolver = ValidationHelper.ValidateIpEndpoint(fallbackResolver);
                if (!string.IsNullOrEmpty(validatedFallbackResolver))
                {
                    validatedFallbackResolvers.Add(validatedFallbackResolver);
                }
                else
                {
                    allValid = false;
                }
            }

            if (!allValid)
            {
                _validationErrors.TryAdd("FallbackResolvers", "invalid");
            }
            else
            {
                _validationErrors.Remove("FallbackResolvers");
            }

            DnscryptProxyConfiguration.fallback_resolvers = validatedFallbackResolvers;
        }

        private sealed record WorkingOnService :IDisposable
        {
            private readonly bool _state;
            private readonly MainViewModel _model;

            public WorkingOnService(MainViewModel model)
            {
                _model = model;
                _state = _model.IsWorkingOnService;
                _model.IsWorkingOnService = true;
            }

            public void Dispose()
            {
                _model.IsWorkingOnService = _state;
            }
        }
    }
}