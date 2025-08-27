using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DaggerTaskManager.MappingObjects;

namespace DaggerTaskManager.Views
{
    public partial class MainDashboardPage : Page
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private CancellationTokenSource? _cts;

        // Bindable list for Recent Chats
        public ObservableCollection<string> RecentChats { get; } =
            new()
            {
                "Sprint 42 — blocked tasks review",
                "Generate QA checklist for v0.3",
                "Summarize meeting notes → tasks"
            };

        public MainDashboardPage()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += Get_WorkTasks;
        }

        private Uri BuildUri()
        {
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

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                List<GetWorkItemFullMappingObject>? workTasks = JsonSerializer.Deserialize<
                    List<GetWorkItemFullMappingObject>
                >(json, options);

                if (workTasks != null && workTasks.Count != 0)
                {
                    RecentChats.Clear();
                    workTasks.ForEach(task => RecentChats.Add(task.Title));
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

        // Click handler for each chat chip
        private void RecentChat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string title)
            {
                MessageBox.Show($"Open chat: {title}", "Recent Chats");
            }
        }

        private void OpenThread_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open selected thread (wire up to your selection model).");
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            // toggle logic optional
        }

        private void Streaming_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
                tb.Content = (tb.IsChecked == true) ? "On" : "Off";
        }

        private void PromptInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                MessageBox.Show("Prompt sent.", "Task Chat");
            }
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Attach (placeholder)");
        }

        private void Mic_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Mic (placeholder)");
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Emoji (placeholder)");
        }

        private void GoToChat_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.RootFrame.Navigate(new TaskChatPage());
            }
        }

        // Bottom tiles
        private void Calendar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Calendar (placeholder)");
        }

        private void Status_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Status (placeholder)");
        }

        private void Plugins_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Plugins (placeholder)");
        }

        private void Archived_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("View Archived (placeholder)");
        }

        private void AddTasks_Click(object sender, RoutedEventArgs e)
        {
            // Show the input panel when Add Tasks is clicked
            AddTaskInputPanel.Visibility = Visibility.Visible;
        }

        private void SubmitTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string taskLink = TaskLinkTextBox.Text;

            if (!string.IsNullOrWhiteSpace(taskLink))
            {
                // Placeholder logic for later endpoint integration
                MessageBox.Show($"Task link submitted: {taskLink}");
            }
            else
            {
                MessageBox.Show("Please enter a valid link.");
            }

            // Optionally clear and hide the panel again
            TaskLinkTextBox.Text = string.Empty;
            AddTaskInputPanel.Visibility = Visibility.Collapsed;
        }

        private void TaskLinkTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TaskLinkPlaceholder.Visibility =
                string.IsNullOrEmpty(TaskLinkTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TaskLinkTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // prevent default newline behavior
                SubmitTaskButton_Click(SubmitTaskButton, new RoutedEventArgs());
            }
        }
    }
}
