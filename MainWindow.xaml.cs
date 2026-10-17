using System;
using System.Collections.Specialized;
using System.Windows;
using DashboardMonitorBateraiManajemen.ViewModels;

namespace DashboardMonitorBateraiManajemen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (DataContext as MainViewModel) ?? new MainViewModel();
            DataContext = _viewModel;

            // Auto-scroll terminal log saat ada baris baru
            _viewModel.RawLogs.CollectionChanged += RawLogs_CollectionChanged;
        }

        private void RawLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogScrollViewer != null)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    LogScrollViewer.ScrollToEnd();
                });
            }
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshPorts();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleConnection();
        }

        private void BtnSimulation_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleSimulation();
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleTheme();
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLogs();
        }

        private void BtnToggleLog_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsLogExpanded = !_viewModel.IsLogExpanded;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Pastikan koneksi serial port ditutup rapi saat aplikasi ditutup
            if (_viewModel.IsConnected)
            {
                _viewModel.ToggleConnection();
            }
            base.OnClosed(e);
        }
    }
}