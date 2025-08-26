// ChatServices.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DaggerTaskManager
{
    // ---------- Models ----------
    public sealed class ChatSummary
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
    }

    public sealed class ChatMessage
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string ChatId { get; init; } = "";
        public string Sender { get; init; } = "";
        public string Text { get; init; } = "";
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    // ---------- Service seam ----------
    public interface IChatService
    {
        Task InitializeAsync();
        Task<IReadOnlyList<ChatSummary>> GetChatsAsync();
        Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
            string chatId,
            int take = 50,
            int skip = 0
        );
        Task<ChatMessage> SendMessageAsync(string chatId, string sender, string text);

        // Optional push (hook SignalR or server-sent events later)
        event Action<ChatMessage>? MessageReceived;
    }

    // ---------- Dummy data for demo ----------
    public sealed class DummyChatService : IChatService
    {
        private readonly Dictionary<string, List<ChatMessage>> _store = new();
        private readonly List<ChatSummary> _chats;

        public event Action<ChatMessage>? MessageReceived;

        public DummyChatService()
        {
            _chats = new()
            {
                new ChatSummary
                {
                    Id = "general",
                    Name = "General",
                    LastMessagePreview = "Welcome to DAG-GER 👋",
                    UnreadCount = 0
                },
                new ChatSummary
                {
                    Id = "sprint-42",
                    Name = "Sprint 42 Stand-up",
                    LastMessagePreview = "Blockers? none",
                    UnreadCount = 2
                },
                new ChatSummary
                {
                    Id = "demo-123",
                    Name = "DEMO-123 Parse Jira",
                    LastMessagePreview = "Link expander PoC",
                    UnreadCount = 5
                },
                new ChatSummary
                {
                    Id = "bug-bash",
                    Name = "Bug Bash",
                    LastMessagePreview = "Triage at 3 PM",
                    UnreadCount = 0
                },
            };

            void seed(string id, params (string from, string text)[] lines)
            {
                _store[id] = lines
                    .Select(
                        (m, i) =>
                            new ChatMessage
                            {
                                ChatId = id,
                                Sender = m.from,
                                Text = m.text,
                                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-60 + i * 5)
                            }
                    )
                    .ToList();
            }

            seed(
                "general",
                ("System", "Welcome to DAG-GER 👋"),
                ("Alex", "Let’s keep this thread tidy."),
                ("Jamie", "Copy that.")
            );

            seed(
                "sprint-42",
                ("ScrumBot", "Stand-up starting. What did you do yesterday?"),
                ("Alex", "Finished DEMO-120."),
                ("You", "Investigating Jira link parsing.")
            );

            seed(
                "demo-123",
                ("PM", "User pastes Jira URL → auto make group chat."),
                ("You", "Backend will parse issue key & participants."),
                ("Alex", "Edge cases: sub-tasks, epics.")
            );

            seed(
                "bug-bash",
                ("QA", "Tonight: focus on image upload failures."),
                ("You", "I’ll watch the logs."),
                ("Ops", "Rate limits raised.")
            );
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<IReadOnlyList<ChatSummary>> GetChatsAsync() =>
            Task.FromResult<IReadOnlyList<ChatSummary>>(_chats);

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
            string chatId,
            int take = 50,
            int skip = 0
        )
        {
            _store.TryGetValue(chatId, out var list);
            list ??= new List<ChatMessage>();
            var page = list.OrderBy(m => m.Timestamp).Skip(skip).Take(take).ToList();
            return Task.FromResult<IReadOnlyList<ChatMessage>>(page);
        }

        public Task<ChatMessage> SendMessageAsync(string chatId, string sender, string text)
        {
            var msg = new ChatMessage
            {
                ChatId = chatId,
                Sender = sender,
                Text = text,
                Timestamp = DateTimeOffset.UtcNow
            };
            if (!_store.TryGetValue(chatId, out var list))
            {
                list = new List<ChatMessage>();
                _store[chatId] = list;
            }
            list.Add(msg);

            var chat = _chats.FirstOrDefault(c => c.Id == chatId);
            if (chat != null)
                chat.LastMessagePreview = text;

            MessageReceived?.Invoke(msg);
            return Task.FromResult(msg);
        }
    }

    // ---------- Skeleton HTTP impl (swap in later) ----------
    // Drop in when backend endpoints exist. Usage is identical to DummyChatService.
    public sealed class HttpChatService : IChatService
    {
        private readonly HttpClient _http;
        public event Action<ChatMessage>? MessageReceived;

        public HttpChatService(string baseUrl, HttpMessageHandler? handler = null)
        {
            _http = handler is null ? new HttpClient() : new HttpClient(handler);
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public Task InitializeAsync()
        {
            // TODO: hook SignalR group connection here for per-chat pushes and raise MessageReceived
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ChatSummary>> GetChatsAsync()
        {
            // GET /api/chats
            var json = await _http.GetStringAsync("api/chats");
            return JsonSerializer.Deserialize<List<ChatSummary>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new();
        }

        public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
            string chatId,
            int take = 50,
            int skip = 0
        )
        {
            // GET /api/chats/{chatId}/messages?take=50&skip=0
            var json = await _http.GetStringAsync(
                $"api/chats/{Uri.EscapeDataString(chatId)}/messages?take={take}&skip={skip}"
            );
            return JsonSerializer.Deserialize<List<ChatMessage>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new();
        }

        public async Task<ChatMessage> SendMessageAsync(string chatId, string sender, string text)
        {
            // POST /api/chats/{chatId}/messages
            var payload = new { sender, text };
            var res = await _http.PostAsync(
                $"api/chats/{Uri.EscapeDataString(chatId)}/messages",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            );
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            var msg = JsonSerializer.Deserialize<ChatMessage>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;
            MessageReceived?.Invoke(msg);
            return msg;
        }
    }
}
