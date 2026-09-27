using System;
using System.Threading;
using Caliburn.Micro;
using NUnit.Framework;
using SimpleDnsCrypt;
using SimpleDnsCrypt.ViewModels;
using SimpleDnsCrypt.Views;

namespace Tests
{
    /// <summary>
    /// Caliburn.Micro was taken from 4.0.212 to 5.0.258, and 5 changed how a ViewModel is mapped
    /// to its View: LocateTypeForModelType is now a delegate field on ViewLocator rather than a
    /// method on ViewModelViewManager, and the type-mapping configuration is a separate object.
    ///
    /// A green build proves nothing here. If the convention stops resolving, Caliburn shows an
    /// empty window rather than throwing, so the app would have looked broken at startup with no
    /// exception in the log. These tests call the real resolver.
    ///
    /// Only the ViewModels listed below are located by convention. QueryLogViewModel,
    /// CloakAndForwardViewModel, DomainBlacklistViewModel, AddressBlacklistViewModel,
    /// DomainBlockLogViewModel and AddressBlockLogViewModel have no View of their own - MainView
    /// binds them as the DataContext of inline markup - so asserting a located View for them
    /// would be testing something the app never claimed.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public class ViewLocationTests
    {
        private static System.Windows.Application _application;

        [OneTimeSetUp]
        public void RegisterCaliburnConventionsTheSameWayTheAppDoes()
        {
            // BootstrapperBase.Initialize() is what installs Caliburn's view conventions, and the
            // app calls it from AppBootstrapper's constructor (App.xaml instantiates it as a
            // resource). Without it LocateTypeForModelType returns null for every ViewModel, so
            // the test would be asserting against an unconfigured framework rather than the app.
            // Application.Run is never called, so OnStartup does not fire and nothing touches DNS.
            _application ??= new System.Windows.Application();
            _ = new AppBootstrapper();
        }
        [TestCase(typeof(AboutViewModel), typeof(AboutView))]
        [TestCase(typeof(AddCustomResolverViewModel), typeof(AddCustomResolverView))]
        [TestCase(typeof(FallbackResolversViewModel), typeof(FallbackResolversView))]
        [TestCase(typeof(ListenAddressesViewModel), typeof(ListenAddressesView))]
        [TestCase(typeof(LoaderViewModel), typeof(LoaderView))]
        [TestCase(typeof(MainViewModel), typeof(MainView))]
        [TestCase(typeof(MetroMessageBoxViewModel), typeof(MetroMessageBoxView))]
        [TestCase(typeof(ProxiesViewModel), typeof(ProxiesView))]
        [TestCase(typeof(RouteViewModel), typeof(RouteView))]
        [TestCase(typeof(SettingsViewModel), typeof(SettingsView))]
        [TestCase(typeof(SystemTrayViewModel), typeof(SystemTrayView))]
        public void CaliburnLocatesTheConventionalViewForTheViewModel(Type viewModelType, Type expectedViewType)
        {
            var located = ViewLocator.LocateTypeForModelType(viewModelType, null, null);

            Assert.IsNotNull(located,
                $"Caliburn located no View for {viewModelType.Name}; the app would show an empty window.");
            Assert.AreEqual(expectedViewType, located);
        }

        [Test]
        public void EveryLocatedViewIsUsableAsWpfContent()
        {
            foreach (var viewModelType in new[]
                     {
                         typeof(AboutViewModel), typeof(AddCustomResolverViewModel),
                         typeof(FallbackResolversViewModel), typeof(ListenAddressesViewModel),
                         typeof(LoaderViewModel), typeof(MainViewModel),
                         typeof(MetroMessageBoxViewModel), typeof(ProxiesViewModel),
                         typeof(RouteViewModel), typeof(SettingsViewModel), typeof(SystemTrayViewModel)
                     })
            {
                var located = ViewLocator.LocateTypeForModelType(viewModelType, null, null);

                Assert.IsNotNull(located, $"no View located for {viewModelType.Name}");
                Assert.IsTrue(typeof(System.Windows.UIElement).IsAssignableFrom(located),
                    $"{located.Name} located for {viewModelType.Name} is not a UIElement");
            }
        }
    }
}
