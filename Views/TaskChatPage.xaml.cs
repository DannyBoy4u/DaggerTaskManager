using Microsoft.AspNetCore.SignalR.Client;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DaggerTaskManager.Views
{
    public partial class TaskChatPage : Page
    {
        private HubConnection _connection;
        private readonly string _userName;

        public TaskChatPage()
        {
            InitializeComponent();

            // Simple username (timestamp to avoid duplicates)
            _userName = $"User-{Environment.MachineName}-{DateTime.Now:HHmmss}";
            UserNameBlock.Text = $" |   You: {_userName}";

            SetupSignalR();
        }

        private void SetupSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5080/chatHub") // matches server
                .WithAutomaticReconnect()
                .Build();

            // Incoming messages
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    bool isSelf = string.Equals(user, _userName, StringComparison.OrdinalIgnoreCase);
                    AddMessageBubble(user, message, isSelf);
                    ScrollToBottom();
                });
            });

            Connect();
        }

        private async void Connect()
        {
            try
            {
                await _connection.StartAsync();
                AddSystem("Connected to server.");
            }
            catch (Exception ex)
            {
                AddSystem($"Connection error: {ex.Message}");
            }
        }

        // Send handlers
        private void SendButton_Click(object sender, RoutedEventArgs e) => SendMessage();

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isEnter = e.Key == Key.Return || e.Key == Key.Enter;     // main + numpad
            bool shiftHeld = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isEnter && !shiftHeld)
            {
                e.Handled = true; // stop newline
                SendMessage();
                InputBox.Clear();
            }
            // If Shift+Enter -> do nothing; TextBox will insert a newline
        }

        private async void SendMessage()
        {
            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            InputBox.Clear();

            try
            {
                await _connection.InvokeAsync("SendMessage", _userName, text);
            }
            catch (Exception ex)
            {
                AddSystem($"Send failed: {ex.Message}");
            }
        }

        // UI helpers
        private void AddSystem(string message)
        {
            var tb = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Gray,
                Margin = new Thickness(10, 6, 10, 6),
                TextWrapping = TextWrapping.Wrap
            };
            ChatPanel.Children.Add(tb);
            ScrollToBottom();
        }

        private void AddMessageBubble(string user, string message, bool isSelf)
        {
            // Inner content: top-left blue label + text
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var label = new TextBlock
            {
                Text = user,
                Foreground = Brushes.White,
                Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007acc"),
                Padding = new Thickness(6, 2, 6, 2),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var text = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320
            };

            stack.Children.Add(label);
            stack.Children.Add(text);

            var bubble = new Border
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFrom(isSelf ? "#274b6d" : "#3a3a3a"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                HorizontalAlignment = isSelf ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Child = stack
            };

            ChatPanel.Children.Add(bubble);
        }

        private void ScrollToBottom()
        {
            ChatScroll.ScrollToEnd();
        }
    }
}
