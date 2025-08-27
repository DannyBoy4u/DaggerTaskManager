using DaggerTaskManager.MappingObjects;
using DaggerTaskManager.Views;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DaggerTaskManager
{
    public partial class MainWindow : Window
    {
        // Reuse one HttpClient for the app (avoid socket exhaustion).
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30) // adjust as needed
        };

        private CancellationTokenSource? _cts;

        // Dynamic, bindable list for Recent Chats
        public ObservableCollection<string> RecentChats { get; } = new()
        {
            "Sprint 42 — blocked tasks review",
            "Generate QA checklist for v0.3",
            "Summarize meeting notes → tasks"
        };

        public MainWindow()
        {
            this.Loaded += Get_WorkTasks;

            InitializeComponent();
            DataContext = this; // enables {Binding RecentChats}
        }

        private Uri BuildUri()
        {
            //TODO: Switch this out for a environment variable setting variable
            var baseUri = new Uri("http://localhost:5080", UriKind.Absolute);
            return new Uri(baseUri, "/work-tasks".TrimStart('/'));
        }

        private async void Get_WorkTasks(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            try
            {
                var resp = await Http.GetAsync(BuildUri(), _cts.Token);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadAsStringAsync(_cts.Token);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // handles camelCase from JSON
                };

                List<GetWorkItemFullMappingObject>? workTasks =
                    JsonSerializer.Deserialize<List<GetWorkItemFullMappingObject>>(json, options);

                if (workTasks.Count != 0)
                {
                    RecentChats.Clear();
                }


                workTasks.ForEach(task =>
                RecentChats.Add(task.Title));
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

        // Click handler for each chip
        private void RecentChat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string title)
            {
                // TODO: navigate/open the thread for 'title'
                MessageBox.Show($"Open chat: {title}", "Recent Chats");
            }
        }

        // Optional: open the currently "selected" chat if you add selection later
        private void OpenThread_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open selected thread (wire up to your selection model).");
        }

        // Top-left Task Chat actions
        private void Mode_Click(object sender, RoutedEventArgs e) { /* toggle logic optional */ }
        private void Streaming_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb) tb.Content = (tb.IsChecked == true) ? "On" : "Off";
        }
        private void PromptInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                // send prompt placeholder
                MessageBox.Show("Prompt sent.", "Task Chat");
            }
        }
        private void Attach_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Attach (placeholder)"); }
        private void Mic_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Mic (placeholder)"); }
        private void Emoji_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Emoji (placeholder)"); }
        private void GoToChat_Click(object sender, RoutedEventArgs e)
        {
            RootFrame.Navigate(new TaskChatPage());
        }

        // Bottom tiles
        private void Calendar_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Calendar (placeholder)"); }
        private void Status_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Status (placeholder)"); }
        private void Plugins_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Plugins (placeholder)"); }
        private void Archived_Click(object sender, RoutedEventArgs e) { MessageBox.Show("View Archived (placeholder)"); }

        private void AddTasks_Click(object sender, RoutedEventArgs e) { MessageBox.Show("View Archived (placeholder)"); }

    }
}
