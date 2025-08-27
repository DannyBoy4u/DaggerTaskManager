using System;
using System.Net.Http;
using System.Windows;
using DaggerTaskManager.Views;

namespace DaggerTaskManager
{
    public partial class MainWindow : Window
    {
        // Reuse one HttpClient for the app (avoid socket exhaustion).
        private static readonly HttpClient Http =
            new()
            {
                Timeout = TimeSpan.FromSeconds(30) // adjust as needed
            };

        public MainWindow()
        {
            InitializeComponent();
            // Default navigation target
            RootFrame.Navigate(new MainDashboardPage());
        }

        // === Side menu navigation ===
        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(new MainDashboardPage());
        }

        private void ChatButton_Click(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(new TaskChatPage()); // add later
        }
    }
}
