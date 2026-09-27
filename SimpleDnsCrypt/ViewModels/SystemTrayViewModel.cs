using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows;
using System.Windows.Input;
using ReactiveUI;

namespace SimpleDnsCrypt.ViewModels
{
    public class SystemTrayViewModel : Screen
    {
        private readonly IWindowManager _windowManager;
        private readonly MainViewModel _mainViewModel;
        private readonly IEventAggregator _events;

        public SystemTrayViewModel(IWindowManager windowManager, IEventAggregator events, MainViewModel mainViewModel)
        {
            _windowManager = windowManager;
            _events = events;
            _mainViewModel = mainViewModel;
            ShowWindowCommand = ReactiveCommand.Create(ShowWindow);
            ToggleWindowStateCommand = ReactiveCommand.Create(ToggleWindowState);
            ExitApplicationCommand = ReactiveCommand.Create(() => Application.Current.Shutdown());
        }

        public ICommand ExitApplicationCommand { get; }

        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            _mainViewModel.ObservableForProperty(x => x.IsActive).TakeWhile(_ => IsActive).Do(_ => RefreshState()).Subscribe();
            RefreshState();
        }

        public ICommand ToggleWindowStateCommand { get; }

        private void ToggleWindowState()
        {
            if (_mainViewModel.IsActive)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        public ICommand ShowWindowCommand { get; }

        private void ShowWindow()
        {
            if (!_mainViewModel.IsActive)
            {
                _windowManager.ShowWindowAsync(_mainViewModel);
            }

            RefreshState();
        }

        public void HideWindow()
        {
            _mainViewModel.TryCloseAsync();
            RefreshState();
        }

        private void RefreshState()
        {
            NotifyOfPropertyChange(() => CanShowWindow);
            NotifyOfPropertyChange(() => CanHideWindow);
        }

        public bool CanShowWindow => !_mainViewModel.IsActive;
        public bool CanHideWindow => _mainViewModel.IsActive;
    }
}