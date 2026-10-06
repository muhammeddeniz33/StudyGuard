using System;
using System.Windows;
using System.Windows.Threading;

namespace StudyGuard
{
    public partial class MainWindow : Window
    {
        private readonly LocalEventServer eventServer;
        private readonly ConfigService configService;
        private readonly ViolationLogService logService;
        private readonly CloudConfigService cloudConfigService;

        private readonly DispatcherTimer cloudSyncTimer;

        private bool alertIsOpen = false;

        public MainWindow()
        {
            InitializeComponent();

            configService =
                new ConfigService();

            logService =
                new ViolationLogService();

            cloudConfigService =
                new CloudConfigService();

            MessageBox.Text =
                configService.Config.BlockMessage;

            eventServer =
                new LocalEventServer(
                    configService
                );

            eventServer.ViolationReceived +=
                OnViolationReceived;

            eventServer.Start();

            RefreshLogs();

            Loaded += async (_, _) =>
            {
                await SyncEverything();
            };

            cloudSyncTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(5)
                };

            cloudSyncTimer.Tick +=
                async (_, _) =>
                {
                    await SyncEverything();
                };

            cloudSyncTimer.Start();
        }

        private async System.Threading.Tasks.Task
            SyncEverything()
        {
            // 1. Heartbeat gönder
            bool heartbeatOk =
                await cloudConfigService
                    .SendHeartbeatAsync();

            // 2. Cloud config çek
            CloudConfigDto? cloudConfig =
                await cloudConfigService
                    .GetConfigAsync();

            if (cloudConfig == null)
            {
                StatusText.Text =
                    heartbeatOk
                        ? "Cihaz online fakat config alınamadı."
                        : "Cloud bağlantısı yok.";

                return;
            }

            configService.Config.BlockMessage =
                cloudConfig.BlockMessage;

            configService.Config.StudyModeEnabled =
                cloudConfig.StudyModeEnabled;

            configService.Save(
                configService.Config
            );

            MessageBox.Text =
                cloudConfig.BlockMessage;

            StatusText.Text =
                cloudConfig.StudyModeEnabled
                    ? "Cloud bağlı. Ders modu AÇIK."
                    : "Cloud bağlı. Ders modu KAPALI.";
        }

        private void SaveMessage_Click(
            object sender,
            RoutedEventArgs e)
        {
            string newMessage =
                MessageBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newMessage))
            {
                StatusText.Text =
                    "Mesaj boş bırakılamaz.";

                return;
            }

            configService.Config.BlockMessage =
                newMessage;

            configService.Save(
                configService.Config
            );

            StatusText.Text =
                "Mesaj lokal olarak kaydedildi.";
        }

        private void TestAlarm_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowAlert("TEST");
        }

        private async void OnViolationReceived(
            string url)
        {
            logService.LogViolation(url);

            string domain = url;

            try
            {
                Uri uri =
                    new Uri(url);

                domain =
                    uri.Host.ToLowerInvariant();
            }
            catch
            {
            }

            bool sentToCloud =
                await cloudConfigService
                    .SendViolationAsync(domain);

            Dispatcher.Invoke(() =>
            {
                RefreshLogs();

                if (!sentToCloud)
                {
                    StatusText.Text =
                        "İhlal lokal kaydedildi fakat cloud'a gönderilemedi.";
                }

                ShowAlert(url);
            });
        }

        private void RefreshLogs()
        {
            ViolationGrid.ItemsSource =
                null;

            ViolationGrid.ItemsSource =
                logService.GetAll();
        }

        private void ShowAlert(
            string url)
        {
            if (alertIsOpen)
                return;

            alertIsOpen =
                true;

            AlertWindow alert =
                new AlertWindow(
                    configService.Config.BlockMessage,
                    url
                );

            alert.Closed +=
                (_, _) =>
                {
                    alertIsOpen =
                        false;
                };

            alert.ShowDialog();
        }

        protected override void OnClosed(
            EventArgs e)
        {
            cloudSyncTimer.Stop();

            eventServer.Stop();

            base.OnClosed(e);
        }
    }
}