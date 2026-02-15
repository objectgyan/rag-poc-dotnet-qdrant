using Rag.Core.Abstractions;
using Rag.Core.Agent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Rag.Infrastructure.Agent;

/// <summary>
/// Orchestrates agent behavior with tool calling, RAG, and reasoning capabilities.
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IChatModel _chatModel;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;

    public AgentOrchestrator(
        IChatModel chatModel,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor)
    {
        _chatModel = chatModel;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
    }

    public async Task<AgentResponse> ProcessAsync(
        string userMessage,
        List<AgentMessage> conversationHistory,
        AgentConfig config,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var messages = new List<AgentMessage>(conversationHistory);
        var toolCallsExecuted = new List<ToolCall>();
        var retrievedDocuments = new List<string>();
        var citations = new List<AgentCitation>();
        var toolUsageCounts = new Dictionary<string, int>();
        var requestToolCache = new Dictionary<string, ToolResult>(); // Cache tool results within this request

        // Add user message
        messages.Add(new AgentMessage("user", userMessage));

        // Iterate up to max tool calls
        for (int iteration = 0; iteration < config.MaxToolCalls; iteration++)
        {
            // Build system prompt with available tools
            var systemPrompt = BuildSystemPrompt(config, tenantId);

            // Build conversation context
            var conversationContext = BuildConversationContext(messages);

            // Get agent response
            var chatResult = await _chatModel.AnswerAsync(
                systemPrompt,
                conversationContext,
                cancellationToken
            );

            var agentResponse = chatResult.Answer;

            // Parse response for tool calls
            var (needsToolCall, toolCalls) = ParseToolCalls(agentResponse);

            if (!needsToolCall || toolCalls.Count == 0)
            {
                // Final answer - no more tool calls needed
                stopwatch.Stop();

                return new AgentResponse(
                    agentResponse,
                    messages,
                    toolCallsExecuted,
                    retrievedDocuments,
                    citations,
                    new AgentMetrics(
                        toolCallsExecuted.Count,
                        retrievedDocuments.Count,
                        stopwatch.Elapsed,
                        EstimateCost(messages, toolCallsExecuted),
                        toolUsageCounts
                    )
                );
            }

            // Execute tool calls
            // Inject tenant_id for rag_search calls to ensure proper data isolation
            if (!string.IsNullOrEmpty(tenantId))
            {
                foreach (var toolCall in toolCalls.Where(tc => tc.ToolName == "rag_search"))
                {
                    if (!toolCall.Arguments.ContainsKey("tenant_id"))
                    {
                        toolCall.Arguments["tenant_id"] = tenantId;
                    }
                }
            }

            // Deduplicate tool calls - check request-level cache first
            var deduplicatedToolCalls = new List<ToolCall>();
            var toolResults = new Dictionary<string, ToolResult>();

            foreach (var toolCall in toolCalls)
            {
                var cacheKey = GetToolCallCacheKey(toolCall);
                
                if (requestToolCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    // Use cached result from earlier in this request
                    toolResults[toolCall.ToolName] = cachedResult;
                    // Don't add to toolCallsExecuted again - it's a duplicate
                }
                else
                {
                    // New tool call - need to execute
                    deduplicatedToolCalls.Add(toolCall);
                }
            }

            // Execute only deduplicated tool calls
            if (config.AllowParallelToolCalls && deduplicatedToolCalls.Count > 1)
            {
                var parallelResults = await _toolExecutor.ExecuteParallelAsync(deduplicatedToolCalls, cancellationToken);
                foreach (var kvp in parallelResults)
                {
                    toolResults[kvp.Key] = kvp.Value;
                }
                
                toolCallsExecuted.AddRange(deduplicatedToolCalls);
                foreach (var tc in deduplicatedToolCalls)
                {
                    var cacheKey = GetToolCallCacheKey(tc);
                    requestToolCache[cacheKey] = parallelResults[tc.ToolName];
                    
                    if (!toolUsageCounts.ContainsKey(tc.ToolName))
                        toolUsageCounts[tc.ToolName] = 0;
                    toolUsageCounts[tc.ToolName]++;
                }
            }
            else if (deduplicatedToolCalls.Count > 0)
            {
                foreach (var toolCall in deduplicatedToolCalls)
                {
                    var result = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken);
                    toolResults[toolCall.ToolName] = result;
                    
                    // Cache for this request
                    var cacheKey = GetToolCallCacheKey(toolCall);
                    requestToolCache[cacheKey] = result;

                    toolCallsExecuted.Add(toolCall);

                    // Track usage
                    if (!toolUsageCounts.ContainsKey(toolCall.ToolName))
                        toolUsageCounts[toolCall.ToolName] = 0;
                    toolUsageCounts[toolCall.ToolName]++;

                    // Track RAG documents and citations
                    if (toolCall.ToolName == "rag_search" && result.Data != null &&
                        result.Data.TryGetValue("documents", out var docs))
                    {
                        retrievedDocuments.Add(result.Content ?? "");
                        
                        // Extract citation details from documents array
                        if (docs is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item is System.Text.Json.JsonElement jsonDoc)
                                {
                                    var documentId = jsonDoc.TryGetProperty("document_id", out var docId) ? docId.GetString() : null;
                                    var page = jsonDoc.TryGetProperty("page", out var p) && int.TryParse(p.GetString(), out var pageNum) ? pageNum : (int?)null;
                                    var score = jsonDoc.TryGetProperty("score", out var s) ? s.GetDouble() : 0.0;
                                    var text = jsonDoc.TryGetProperty("text", out var t) ? t.GetString() : null;
                                    
                                    if (documentId != null)
                                    {
                                        citations.Add(new AgentCitation(documentId, page, score, text));
                                    }
                                }
                                else
                                {
                                    // Handle as dynamic object using reflection
                                    var itemType = item?.GetType();
                                    if (itemType != null)
                                    {
                                        var docIdProp = itemType.GetProperty("document_id");
                                        var pageProp = itemType.GetProperty("page");
                                        var scoreProp = itemType.GetProperty("score");
                                        var textProp = itemType.GetProperty("text");
                                        
                                        var documentId = docIdProp?.GetValue(item)?.ToString();
                                        var pageStr = pageProp?.GetValue(item)?.ToString();
                                        var page = pageStr != null && int.TryParse(pageStr, out var pageNum) ? pageNum : (int?)null;
                                        var score = (double?)(scoreProp?.GetValue(item) ?? 0.0) ?? 0.0;
                                        var text = textProp?.GetValue(item)?.ToString();
                                        
                                        if (documentId != null)
                                        {
                                            citations.Add(new AgentCitation(documentId, page, score, text));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Add tool results to conversation
            foreach (var toolCall in toolCalls)
            {
                messages.Add(new AgentMessage("assistant", null, toolCall, null));

                if (toolResults.TryGetValue(toolCall.ToolName, out var result))
                {
                    messages.Add(new AgentMessage("tool", null, null, result));
                }
            }
        }

        // Max iterations reached
        stopwatch.Stop();

        return new AgentResponse(
            "I apologize, but I reached the maximum number of tool calls without completing your request. Please try rephrasing your question.",
            messages,
            toolCallsExecuted,
            retrievedDocuments,
            citations,
            new AgentMetrics(
                toolCallsExecuted.Count,
                retrievedDocuments.Count,
                stopwatch.Elapsed,
                EstimateCost(messages, toolCallsExecuted),
                toolUsageCounts
            )
        );
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
        string userMessage,
        List<AgentMessage> conversationHistory,
        AgentConfig config,
        string? tenantId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new AgentStreamChunk(
            AgentStreamChunkType.Reasoning,
            ReasoningTrace: "Starting agent processing..."
        );

        // For now, process non-streaming and yield results
        // TODO: Implement true streaming with IChatModel streaming support
        var response = await ProcessAsync(userMessage, conversationHistory, config, tenantId, cancellationToken);

        // Yield tool calls
        foreach (var toolCall in response.ToolCallsExecuted)
        {
            yield return new AgentStreamChunk(
                AgentStreamChunkType.ToolCallStart,
                ToolCall: toolCall
            );
        }

        // Yield final answer
        yield return new AgentStreamChunk(
            AgentStreamChunkType.ContentComplete,
            Content: response.FinalAnswer
        );
    }

    private string BuildSystemPrompt(AgentConfig config, string? tenantId)
    {
        var sb = new StringBuilder();

        sb.AppendLine(config.SystemPrompt ?? @"You are an intelligent AI agent with access to tools.
You can call tools to help answer user questions.

When you need to use a tool, respond in this JSON format:
{
  ""reasoning"": ""Why you need this tool"",
  ""tool_calls"": [
    {
      ""tool_name"": ""tool_name_here"",
      ""arguments"": {
        ""param1"": ""value1""
      }
    }
  ]
}

IMPORTANT GUIDELINES:
- DO NOT call the same tool with the same arguments multiple times in one conversation turn
- After receiving tool results, USE THEM to formulate your answer instead of calling tools again
- If you already have information from a previous tool call, synthesize an answer from that
- Only call tools when you genuinely need NEW information that you don't already have
- Avoid redundant searches - use the context and tool results you already received

After tool results are provided, synthesize a final answer for the user.
If you can answer directly without tools, just provide the answer normally.

Available tools:");

        var tools = _toolRegistry.GetAllTools();
        foreach (var tool in tools)
        {
            sb.AppendLine($"\n**{tool.Name}**: {tool.Description}");
            sb.AppendLine("Parameters:");
            foreach (var param in tool.Parameters)
            {
                var required = param.Required ? "(required)" : "(optional)";
                sb.AppendLine($"  - {param.Name} ({param.Type}) {required}: {param.Description}");
            }
        }

        if (config.UseRagForContext && tenantId != null)
        {
            sb.AppendLine($"\nCurrent tenant: {tenantId}");
            sb.AppendLine("IMPORTANT: When calling 'rag_search' tool, ALWAYS include the tenant_id parameter with the current tenant value to ensure proper data isolation.");
            sb.AppendLine($"Always use: {{\"tenant_id\": \"{tenantId}\"}} in rag_search calls.");
        }

        return sb.ToString();
    }

    private string BuildConversationContext(List<AgentMessage> messages)
    {
        var sb = new StringBuilder();

        foreach (var msg in messages)
        {
            if (msg.Role == "user")
            {
                sb.AppendLine($"User: {msg.Content}");
            }
            else if (msg.Role == "assistant" && msg.ToolCall == null)
            {
                sb.AppendLine($"Assistant: {msg.Content}");
            }
            else if (msg.Role == "tool" && msg.ToolResult != null)
            {
                sb.AppendLine($"Tool Result: {msg.ToolResult.Content}");
            }
        }

        return sb.ToString().Trim();
    }

    private (bool NeedsToolCall, List<ToolCall> ToolCalls) ParseToolCalls(string response)
    {
        // Try to parse JSON tool call format
        try
        {
            // Look for JSON in response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("tool_calls", out var toolCallsArray))
                {
                    var toolCalls = new List<ToolCall>();

                    foreach (var tcElement in toolCallsArray.EnumerateArray())
                    {
                        var toolName = tcElement.GetProperty("tool_name").GetString()!;
                        var arguments = new Dictionary<string, object>();

                        if (tcElement.TryGetProperty("arguments", out var argsElement))
                        {
                            foreach (var prop in argsElement.EnumerateObject())
                            {
                                arguments[prop.Name] = ParseJsonValue(prop.Value);
                            }
                        }

                        var reasoning = root.TryGetProperty("reasoning", out var reasoningEl)
                            ? reasoningEl.GetString()
                            : null;

                        toolCalls.Add(new ToolCall(toolName, arguments, reasoning));
                    }

                    return (true, toolCalls);
                }
            }
        }
        catch
        {
            // Not a tool call, return as regular response
        }

        return (false, new List<ToolCall>());
    }

    private object ParseJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ParseJsonValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ParseJsonValue(p.Value)),
            _ => element.ToString()
        };
    }

    private string GetToolCallCacheKey(ToolCall toolCall)
    {
        // Create a unique key for this tool call based on tool name and arguments
        var argsJson = JsonSerializer.Serialize(toolCall.Arguments, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return $"{toolCall.ToolName}:{argsJson}";
    }

    private double EstimateCost(List<AgentMessage> messages, List<ToolCall> toolCalls)
    {
        // Rough cost estimation
        // Claude API: ~$0.003 per 1K tokens for Sonnet
        var totalTokens = messages.Sum(m => (m.Content?.Length ?? 0) / 4); // Rough token estimate
        var baseCost = (totalTokens / 1000.0) * 0.003;

        // Add tool execution costs
        var toolCost = toolCalls.Count * 0.001; // Rough estimate

        return baseCost + toolCost;
    }
}
