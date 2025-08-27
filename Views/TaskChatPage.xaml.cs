using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http;
using System.Windows.Media;
using DaggerTaskManager.MappingObjects;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace DaggerTaskManager.Views
{
    public partial class TaskChatPage : Page
    {
        private HubConnection? _connection;
        private readonly string _userName;
        private readonly Dictionary<string, Brush> _userColors = new();
        private int _nextColorIndex = 0;

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private CancellationTokenSource? _cts;

        private readonly Dictionary<Guid, List<TaskChatMessage>> _taskChats = new();

        // Palette of colors to cycle through
        private readonly List<Brush> _palette =
            new()
            {
                (SolidColorBrush)new BrushConverter().ConvertFrom("#007acc")!, // blue
                (SolidColorBrush)new BrushConverter().ConvertFrom("#1abc9c")!, // teal
                (SolidColorBrush)new BrushConverter().ConvertFrom("#9b59b6")!, // purple
                (SolidColorBrush)new BrushConverter().ConvertFrom("#e67e22")!, // orange
                (SolidColorBrush)new BrushConverter().ConvertFrom("#2ecc71")!, // green
                (SolidColorBrush)new BrushConverter().ConvertFrom("#e74c3c")! // red
            };

        public ObservableCollection<GetWorkItemFullMappingObject> WorkTasks { get; } = new();

        private GetWorkItemFullMappingObject? _currentTask;

        private Uri BuildUri()
        {
            var baseUri = new Uri("http://localhost:5080", UriKind.Absolute);
            return new Uri(baseUri, "/work-tasks".TrimStart('/'));
        }


        public class TaskChatMessage
        {
            public string Message { get; set; }
            public string User { get; set; }
        }

        private async void Get_WorkTasks(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            try
            {
                var resp = await Http.GetAsync(BuildUri(), _cts.Token);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync(_cts.Token);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                List<GetWorkItemFullMappingObject>? workTasks = JsonSerializer.Deserialize<
                    List<GetWorkItemFullMappingObject>
                >(json, options);

                if (workTasks != null && workTasks.Count != 0)
                {
                    WorkTasks.Clear();
                    workTasks.ForEach(task => WorkTasks.Add(task));
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Request canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "GET failed");
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        public TaskChatPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += Get_WorkTasks;

            _userName = $"User-{Environment.MachineName}-{DateTime.Now:HHmmss}";

            SetupSignalR();



            ChatList.SelectionChanged += ChatList_SelectionChanged;
        }

        private void ChatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatList.SelectedItem is GetWorkItemFullMappingObject task)
            {
                _currentTask = task;
                ChatPanel.Children.Clear();

                _taskChats.TryGetValue(_currentTask.Id, out var list);

                list?.ForEach(item =>
                {
                    bool isSelf = string.Equals(item.User, _userName, StringComparison.OrdinalIgnoreCase);

                    AddMessageBubble(item.User, item.Message, isSelf);
                });

                ChatTitleBlock.Text = task.Title;


            }
        }

        private void SetupSignalR()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5080/chatHub")
                .WithAutomaticReconnect()
                .Build();

            // Incoming messages
            _connection.On<string, string, Guid>("ReceiveMessage", (user, message, taskId) =>
            {
                if (!_taskChats.TryGetValue(taskId, out var list))
                {
                    list = new List<TaskChatMessage>();
                    _taskChats[taskId] = list;
                }

                list.Add(new TaskChatMessage
                {
                    Message = message,
                    User = user,
                });

                if (_currentTask.Id == taskId)
                {
                    Dispatcher.Invoke(() =>
                    {
                        bool isSelf = string.Equals(user, _userName, StringComparison.OrdinalIgnoreCase);
                        AddMessageBubble(user, message, isSelf);
                        ScrollToBottom();
                    });
                }
            });

            Connect();
        }

        private async void Connect()
        {
            if (_connection == null) return;

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

        private void SendButton_Click(object sender, RoutedEventArgs e) => SendMessage();

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                SendMessage();
                InputBox.Clear();
            }
        }

        private async void SendMessage()
        {
            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || _currentTask == null || _connection == null)
                return;

            try
            {
                await _connection.InvokeAsync("SendMessage", _userName, text, _currentTask.Id);
            }
            catch (Exception ex)
            {
                AddSystem($"Send failed: {ex.Message}");
            }
        }

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
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var label = new TextBlock
            {
                Text = user,
                Foreground = Brushes.White,
                Background = GetUserColor(user),
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
                Background = isSelf
                    ? (SolidColorBrush)new BrushConverter().ConvertFrom("#274b6d")!
                    : (SolidColorBrush)new BrushConverter().ConvertFrom("#3a3a3a")!,
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

        private Brush GetUserColor(string user)
        {
            if (_userColors.TryGetValue(user, out var color))
                return color;

            var newColor = _palette[_nextColorIndex % _palette.Count];
            _userColors[user] = newColor;
            _nextColorIndex++;
            return newColor;
        }
    }
}
