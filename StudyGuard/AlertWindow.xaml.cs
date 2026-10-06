using System;
using System.Media;
using System.Windows;
using System.Windows.Threading;

namespace StudyGuard
{
    public partial class AlertWindow : Window
    {
        private readonly DispatcherTimer alarmTimer;

        private const string AdminPin = "1234";

        public AlertWindow(string message, string url)
        {
            InitializeComponent();

            // Dinamik mesaj
            MessageText.Text = message;

            // Engellenen adres
            UrlText.Text = string.IsNullOrWhiteSpace(url)
                ? ""
                : $"Engellenen adres: {url}";

            alarmTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(700)
            };

            alarmTimer.Tick += AlarmTimer_Tick;
            alarmTimer.Start();

            PinBox.Focus();
        }

        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            SystemSounds.Hand.Play();
        }

        private void Unlock_Click(object sender, RoutedEventArgs e)
        {
            if (PinBox.Password == AdminPin)
            {
                alarmTimer.Stop();
                Close();
            }
            else
            {
                ErrorText.Text = "Hatalı PIN.";

                PinBox.Clear();
                PinBox.Focus();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            alarmTimer.Stop();

            base.OnClosed(e);
        }
    }
}