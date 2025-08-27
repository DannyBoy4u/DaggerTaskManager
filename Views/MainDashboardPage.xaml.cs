using Atlassian.Jira;
using DaggerTaskManager.MappingObjects;
using DaggerTaskManager.TaskPlugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DaggerTaskManager.Views
{
    public partial class MainDashboardPage : Page
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly JiraLinkService _jiraService;

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
            //TODO: Set this with env later
            string email = "";
            string apiKey = "";
            string baseUrl = "https://dag-ger.atlassian.net/";

            _jiraService = new JiraLinkService(baseUrl, email, apiKey);
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
        //public string? AssigneeName { get; set; }
        //public string Title { get; set; } = string.Empty;
        //public long EpochCreateDate { get; set; }
        //public long EpochUpdatedDate { get; set; }
        //public string? Description { get; set; }
        //public string? UrlLink { get; set; }
        //public string? SiteSource { get; set; }
        //public long EpochDueDate { get; set; }
        //public long EpochStartDate { get; set; }
        //public string? Status { get; set; }
        //public WorkTaskTypeMappingObject? TaskType { get; set; }
        //public Guid TaskTypeId { get; set; }
        private async void SubmitTaskButton_Click(object sender, RoutedEventArgs e)
        {

            string taskLink = TaskLinkTextBox.Text;

            var uri = new Uri(taskLink);
            // Normalize: if user pasted a deep “/jira/software/…” path, still keep base host
            var siteBase = $"{uri.Scheme}://{uri.Host}";
            if (!siteBase.Equals("https://dag-ger.atlassian.net", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"This client is configured for {"https://dag-ger.atlassian.net/"}, but the link is for {siteBase}.");


            if (!string.IsNullOrWhiteSpace(taskLink))
            {
                var task = await _jiraService.LoadFromTaskLinkAsync(taskLink);

                var taskToInsert = task.Select(_ => new GetWorkItemFullMappingObject
                {
                    AssigneeName = _.Assignee,
                    Title = _.Title,
                    Description = _.Description,
                    UrlLink = taskLink,
                    SiteSource = "Jira",
                    EpochDueDate = _.EndDate ?? 0,
                    EpochStartDate = _.StartDate ?? 0,
                    Status = _.Status,
                    TaskTypeId = new Guid("9ce0ab91-8506-4005-908b-6e937c87d11a")
                }).FirstOrDefault();

                // POST to /work-tasks
                var response = await Http.PostAsJsonAsync(BuildUri(), taskToInsert);
                response.EnsureSuccessStatusCode();

                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    MessageBox.Show($"Task already is available under task chat.");
                }

                else
                {
                    // The endpoint returns 201 with the created DTO — read it back:
                    // var created = await response.Content.ReadFromJsonAsync<GetWorkItemFullMappingObject>();

                    // Placeholder logic for later endpoint integration
                    MessageBox.Show($"Task link submitted: {taskLink}");
                }
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

        private async void TaskLinkTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // prevent default newline behavior
                SubmitTaskButton_Click(SubmitTaskButton, new RoutedEventArgs());
            }
        }
    }
}
