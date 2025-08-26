using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.SignalR.Client;

namespace DaggerTaskManager
{
    public partial class MainWindow : Window
    {
        private HubConnection _connection;
        private readonly string _userName;

        // NEW: chat data
        private readonly IChatService _chatService; // swap DummyChatService -> HttpChatService later
        private readonly ObservableCollection<ChatSummary> _chatItems = new();
        private ChatSummary? _selectedChat;

        public string SelectedChatTitle => _selectedChat?.Name ?? "Task Chat";

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Select service here
                _chatService = new DummyChatService();
                _chatService.MessageReceived += OnServiceMessageReceived;

                _userName = $"User-{Environment.MachineName}-{DateTime.Now:HHmmss}";
                UserNameBlock.Text = $" | You: {_userName}";

                Loaded += async (_, __) =>
                {
                    // Load chats/messages
                    await _chatService.InitializeAsync();
                    await LoadChatsAsync();

                    // SignalR (global demo channel for now)
                    SetupSignalR();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error");
                throw;
            }
        }

        // --------- Data loading ----------
        private async Task LoadChatsAsync()
        {
            ChatList.ItemsSource = _chatItems;
            _chatItems.Clear();
            var chats = await _chatService.GetChatsAsync();
            foreach (var c in chats)
                _chatItems.Add(c);

            if (_chatItems.Count > 0)
            {
                ChatList.SelectedIndex = 0; // triggers ChatList_SelectionChanged -> loads messages
            }
        }

        private async Task LoadMessagesAsync(string chatId)
        {
            ChatPanel.Children.Clear();
            var messages = await _chatService.GetMessagesAsync(chatId, take: 200);
            foreach (var m in messages)
            {
                var isSelf =
                    string.Equals(m.Sender, _userName, StringComparison.OrdinalIgnoreCase)
                    || m.Sender.Equals("You", StringComparison.OrdinalIgnoreCase);
                AddMessageBubble(m.Sender, m.Text, isSelf);
            }
            ScrollToBottom();
        }

        private void OnServiceMessageReceived(ChatMessage msg)
        {
            // Only render if it’s for the active chat
            if (_selectedChat?.Id != msg.ChatId)
                return;

            Dispatcher.Invoke(() =>
            {
                bool isSelf = string.Equals(
                    msg.Sender,
                    _userName,
                    StringComparison.OrdinalIgnoreCase
                );
                AddMessageBubble(msg.Sender, msg.Text, isSelf);
                ScrollToBottom();
            });
        }

        // --------- Sidebar handlers ----------
        private async void ChatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedChat = ChatList.SelectedItem as ChatSummary;
            // Update header title binding
            DataContext = null;
            DataContext = this;

            if (_selectedChat != null)
            {
                await LoadMessagesAsync(_selectedChat.Id);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = (sender as TextBox)?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query))
            {
                ChatList.ItemsSource = _chatItems;
                return;
            }
            var filtered = _chatItems
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            ChatList.ItemsSource = filtered;
        }

        private async void NewChat_Click(object sender, RoutedEventArgs e)
        {
            // Demo: create a new local chat with a welcome message
            var id = $"local-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var name = "New Chat";
            // In real impl: POST /api/chats (or parse Jira link)
            if (_chatItems.All(c => c.Id != id))
            {
                _chatItems.Insert(
                    0,
                    new ChatSummary
                    {
                        Id = id,
                        Name = name,
                        LastMessagePreview = "Chat created"
                    }
                );
                await _chatService.SendMessageAsync(
                    id,
                    "System",
                    "Welcome! Paste a Jira link to begin."
                );
                ChatList.SelectedIndex = 0;
            }
        }

        // --------- SignalR (global demo) ----------
        private void SetupSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5080/chatHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>(
                "ReceiveMessage",
                (user, message) =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        // Route incoming global message into the currently selected chat for demo purposes.
                        // Later: server will send to SignalR group by chatId.
                        if (_selectedChat is null)
                            return;

                        // Also add to the service store so history contains it
                        await _chatService.SendMessageAsync(_selectedChat.Id, user, message);
                    });
                }
            );

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

        // --------- Send handlers ----------
        private async void SendButton_Click(object sender, RoutedEventArgs e) =>
            await SendMessage();

        private async void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isEnter = e.Key == Key.Return || e.Key == Key.Enter;
            bool shiftHeld = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isEnter && !shiftHeld)
            {
                e.Handled = true;
                await SendMessage();
                InputBox.Clear();
            }
        }

        private async Task SendMessage()
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || _selectedChat is null)
                return;
            InputBox.Clear();

            try
            {
                // 1) Store via service (dummy now, HTTP later)
                await _chatService.SendMessageAsync(_selectedChat.Id, _userName, text);

                // 2) Still emit to existing SignalR hub (global)
                await _connection.InvokeAsync("SendMessage", _userName, text);
            }
            catch (Exception ex)
            {
                AddSystem($"Send failed: {ex.Message}");
            }
        }

        // --------- UI helpers (unchanged, with slight color touches) ----------
        private void AddSystem(string message)
        {
            var tb = new TextBlock
            {
                Text = message,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#9AA4B2"),
                Margin = new Thickness(10, 6, 10, 6),
                TextWrapping = TextWrapping.Wrap
            };
            ChatPanel.Children.Add(tb);
            ScrollToBottom();
        }

        private void AddMessageBubble(string user, string message, bool isSelf)
        {
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
                MaxWidth = 420
            };

            stack.Children.Add(label);
            stack.Children.Add(text);

            var bubble = new Border
            {
                Background = (SolidColorBrush)
                    new BrushConverter().ConvertFrom(isSelf ? "#1E334C" : "#2A3246"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(8, 6, 8, 6),
                HorizontalAlignment = isSelf ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.25,
                    Color = Colors.Black
                },
                Child = stack
            };

            ChatPanel.Children.Add(bubble);
        }

        private void ScrollToBottom() => ChatScroll.ScrollToEnd();
    }

    // Moved here for testing
    public sealed class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => (value is int n && n > 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }

    public sealed class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string)?.Trim();
            if (string.IsNullOrEmpty(s))
                return "🗨️";

            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();

            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpperInvariant();
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
