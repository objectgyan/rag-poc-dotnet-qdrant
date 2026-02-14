using Rag.Core.Agent;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rag.Infrastructure.Agent.Tools;

/// <summary>
/// Tool for searching GitHub repositories.
/// </summary>
public class GitHubSearchRepositoriesTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string? _githubToken;

    public string Name => "github_search_repositories";

    public string Description => "Search for GitHub repositories by query. Returns repository names, descriptions, and metadata.";

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("query", "Search query (e.g., 'vector database', 'machine learning python')", "string", true),
        new("sort", "Sort by: stars, forks, updated (default: stars)", "string", false, "stars", new List<string> { "stars", "forks", "updated" }),
        new("max_results", "Maximum number of results (default: 5)", "number", false, 5)
    };

    public GitHubSearchRepositoriesTool(IHttpClientFactory httpClientFactory, string? githubToken = null)
    {
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _githubToken = githubToken;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RagPocAgent");
        if (!string.IsNullOrEmpty(_githubToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
        }
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        var query = arguments["query"].ToString()!;
        var sort = arguments.TryGetValue("sort", out var sortObj) ? sortObj.ToString() : "stars";
        var maxResults = arguments.TryGetValue("max_results", out var maxObj) ? Convert.ToInt32(maxObj) : 5;

        var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(query)}&sort={sort}&per_page={maxResults}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            if (!response.TryGetProperty("items", out var items))
            {
                return ToolResult.Fail("Invalid response from GitHub API");
            }

            var repositories = new List<object>();
            foreach (var item in items.EnumerateArray())
            {
                repositories.Add(new
                {
                    name = item.GetProperty("full_name").GetString(),
                    description = item.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                    stars = item.GetProperty("stargazers_count").GetInt32(),
                    forks = item.GetProperty("forks_count").GetInt32(),
                    language = item.TryGetProperty("language", out var lang) ? lang.GetString() : "Unknown",
                    url = item.GetProperty("html_url").GetString(),
                    updated_at = item.GetProperty("updated_at").GetString()
                });
            }

            var content = $"Found {repositories.Count} repositories:\n\n";
            for (int i = 0; i < repositories.Count; i++)
            {
                dynamic repo = repositories[i];
                content += $"{i + 1}. **{repo.name}** (⭐ {repo.stars})\n";
                content += $"   {repo.description}\n";
                content += $"   Language: {repo.language} | Forks: {repo.forks}\n";
                content += $"   URL: {repo.url}\n\n";
            }

            return ToolResult.Ok(
                content.Trim(),
                new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["count"] = repositories.Count,
                    ["repositories"] = repositories
                }
            );
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"GitHub API error: {ex.Message}");
        }
    }
}

/// <summary>
/// Tool for searching code in GitHub repositories.
/// </summary>
public class GitHubSearchCodeTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string? _githubToken;

    public string Name => "github_search_code";

    public string Description => "Search for code snippets across GitHub. Useful for finding examples and implementations.";

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("query", "Code search query (e.g., 'vector database embedding', 'async await pattern')", "string", true),
        new("language", "Programming language filter (optional)", "string", false),
        new("max_results", "Maximum number of results (default: 5)", "number", false, 5)
    };

    public GitHubSearchCodeTool(IHttpClientFactory httpClientFactory, string? githubToken = null)
    {
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _githubToken = githubToken;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RagPocAgent");
        if (!string.IsNullOrEmpty(_githubToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
        }
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        var query = arguments["query"].ToString()!;
        var language = arguments.TryGetValue("language", out var langObj) ? langObj.ToString() : null;
        var maxResults = arguments.TryGetValue("max_results", out var maxObj) ? Convert.ToInt32(maxObj) : 5;

        var searchQuery = query;
        if (!string.IsNullOrEmpty(language))
        {
            searchQuery += $"+language:{language}";
        }

        var url = $"https://api.github.com/search/code?q={Uri.EscapeDataString(searchQuery)}&per_page={maxResults}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            if (!response.TryGetProperty("items", out var items))
            {
                return ToolResult.Fail("Invalid response from GitHub API");
            }

            var codeResults = new List<object>();
            foreach (var item in items.EnumerateArray())
            {
                var repository = item.GetProperty("repository");
                codeResults.Add(new
                {
                    name = item.GetProperty("name").GetString(),
                    path = item.GetProperty("path").GetString(),
                    repository = repository.GetProperty("full_name").GetString(),
                    url = item.GetProperty("html_url").GetString()
                });
            }

            var content = $"Found {codeResults.Count} code snippet(s):\n\n";
            for (int i = 0; i < codeResults.Count; i++)
            {
                dynamic code = codeResults[i];
                content += $"{i + 1}. **{code.name}**\n";
                content += $"   Repository: {code.repository}\n";
                content += $"   Path: {code.path}\n";
                content += $"   URL: {code.url}\n\n";
            }

            return ToolResult.Ok(
                content.Trim(),
                new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["count"] = codeResults.Count,
                    ["results"] = codeResults
                }
            );
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"GitHub API error: {ex.Message}");
        }
    }
}
