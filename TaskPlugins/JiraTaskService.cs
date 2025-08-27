using Atlassian.Jira;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DaggerTaskManager.TaskPlugins;

public sealed class IssueRow
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Assignee { get; set; } = "";
    public string Key { get; set; } = "";
    public string Status { get; set; } = "";
    public long? StartDate { get; set; }
    public long? EndDate { get; set; }
}

public class JiraLinkService
{
    private readonly string _baseUrl;
    private readonly string _email;
    private readonly string _apiToken;
    private readonly Jira _jira;

    public JiraLinkService(string baseUrl, string email, string apiToken)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _email = email;
        _apiToken = apiToken;

        _jira = Jira.CreateRestClient(_baseUrl, _email, _apiToken);
    }

    public async Task<List<IssueRow>> LoadFromTaskLinkAsync(string taskLink)
    {
        if (string.IsNullOrWhiteSpace(taskLink))
            return new List<IssueRow>();

        var uri = new Uri(taskLink);
        var siteBase = $"{uri.Scheme}://{uri.Host}";
        if (!siteBase.Equals(_baseUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Configured for {_baseUrl}, but link is for {siteBase}.");

        var query = ParseQuery(uri.Query);

        // Case 1: single issue
        if (query.TryGetValue("selectedIssue", out var selectedIssueKey) && !string.IsNullOrWhiteSpace(selectedIssueKey))
        {
            var issue = await _jira.Issues.GetIssueAsync(selectedIssueKey.Trim());
            return new List<IssueRow> { ToRow(issue) };
        }


        // Case 2: explicit JQL
        if (query.TryGetValue("jql", out var jql) && !string.IsNullOrWhiteSpace(jql))
        {
            var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql, 100);
            return issues.Select(ToRow).ToList();
        }

        // Otherwise, extract from path/fragment, e.g. /browse/KAN-1 or /jira/browse/KAN-1#KAN-1
        var key = TryExtractIssueKey(uri);
        if (!string.IsNullOrEmpty(key))
        {
            var issue = await _jira.Issues.GetIssueAsync(key);
            return new List<IssueRow> { ToRow(issue) };
        }

        // Case 3: /projects/{KEY}/… fallback
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var projIndex = Array.FindIndex(segments, s => s.Equals("projects", StringComparison.OrdinalIgnoreCase));
        if (projIndex >= 0 && projIndex + 1 < segments.Length)
        {
            var projectKey = segments[projIndex + 1];
            var jql2 = $"project = {projectKey} ORDER BY updated DESC";
            var issues = await _jira.Issues.GetIssuesFromJqlAsync(jql2, 100);
            return issues.Select(ToRow).ToList();
        }

        throw new NotSupportedException("Couldn’t infer what to fetch from that link.");
    }

    private static string? TryExtractIssueKey(Uri uri)
    {
        // Look across path, fragment and query (some Jira UIs echo the key in the hash)
        var haystack = $"{uri.AbsolutePath} {uri.Fragment} {uri.Query}";

        // JIRA key pattern: PROJECT-123 (PROJECT starts with a letter, then letters/digits; dash; number)
        var m = Regex.Match(haystack, @"(?<![A-Z0-9-])([A-Z][A-Z0-9]+-\d+)(?![A-Z0-9-])",
                            RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static IssueRow ToRow(Issue i) => new IssueRow
    {
        Assignee = i.Assignee,
        Description = i.Description,
        Title = i.Summary,
        Key = i.Key.Value,
        EndDate = i.DueDate != null ? EpochTime.GetIntDate(i.DueDate.Value) : null,
        StartDate = i.CustomFields["Start date"] != null ? (DateTime.TryParse(i.CustomFields["Start date"].Values.FirstOrDefault(), out var startDate) ? EpochTime.GetIntDate(startDate) : null) : null,
        Status = i.Status?.Name ?? ""
    };

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return dict;
        var q = query[0] == '?' ? query.Substring(1) : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            dict[key] = val;
        }
        return dict;
    }
}
