using Rag.Core.Abstractions;
using Rag.Core.Services;
using System.Text;
using System.Text.Json;

namespace Rag.Infrastructure.Evaluation;

/// <summary>
/// Uses LLM to detect hallucinations by checking if answer is grounded in context
/// </summary>
public class LlmHallucinationDetector : IHallucinationDetector
{
    private readonly IChatModel _chatModel;
    
    public LlmHallucinationDetector(IChatModel chatModel)
    {
        _chatModel = chatModel;
    }
    
    public async Task<(double score, List<string> hallucinatedFacts)> DetectHallucinationAsync(
        string answer,
        List<string> context,
        CancellationToken ct = default)
    {
        var prompt = BuildHallucinationDetectionPrompt(answer, context);
        
        var result = await _chatModel.AnswerAsync("You are a hallucination detector.", prompt, ct);
        
        // Parse the LLM response
        return ParseHallucinationResponse(result.Answer);
    }
    
    private static string BuildHallucinationDetectionPrompt(string answer, List<string> context)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a hallucination detector for a RAG system.");
        sb.AppendLine("Your task is to determine if the ANSWER contains information that is NOT present in the CONTEXT.");
        sb.AppendLine();
        sb.AppendLine("CONTEXT (Retrieved documents):");
        sb.AppendLine("---");
        
        for (int i = 0; i < context.Count; i++)
        {
            sb.AppendLine($"[Chunk {i + 1}]");
            sb.AppendLine(context[i]);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("ANSWER:");
        sb.AppendLine(answer);
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Carefully read the CONTEXT");
        sb.AppendLine("2. Analyze the ANSWER");
        sb.AppendLine("3. Identify any facts, claims, or statements in the ANSWER that are NOT supported by the CONTEXT");
        sb.AppendLine("4. A hallucination is when the answer makes up information not present in the context");
        sb.AppendLine("5. Respond in JSON format:");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"hallucinationScore\": 0.0,  // 0.0 = fully grounded, 1.0 = completely hallucinated");
        sb.AppendLine("  \"hallucinatedFacts\": [],    // List of specific hallucinated statements");
        sb.AppendLine("  \"reasoning\": \"...\"         // Brief explanation");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Be strict but fair. Minor paraphrasing is acceptable, but fabricated facts are not.");
        
        return sb.ToString();
    }
    
    private static (double score, List<string> hallucinatedFacts) ParseHallucinationResponse(string response)
    {
        try
        {
            // Extract JSON from response (LLM might include extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var json = JsonDocument.Parse(jsonStr);
                
                var score = json.RootElement.GetProperty("hallucinationScore").GetDouble();
                var facts = new List<string>();
                
                if (json.RootElement.TryGetProperty("hallucinatedFacts", out var factsArray))
                {
                    foreach (var fact in factsArray.EnumerateArray())
                    {
                        facts.Add(fact.GetString() ?? "");
                    }
                }
                
                return (score, facts);
            }
        }
        catch (Exception)
        {
            // If parsing fails, return conservative estimate
        }
        
        // Default to moderate hallucination score if parsing fails
        return (0.5, new List<string> { "Unable to parse hallucination analysis" });
    }
}
