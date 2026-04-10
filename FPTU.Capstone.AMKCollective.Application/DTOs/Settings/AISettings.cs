using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class AISettings
    {
        public string GoogleApiKey { get; set; } = string.Empty;
        public string GroqApiKey { get; set; } = string.Empty;
        public string OpenAiApiKey { get; set; } = string.Empty;
        public string GroqUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
        public string OpenAiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
        public string GoogleModelName { get; set; } = "gemini-embedding-exp-03-07";
        public string GroqModelName { get; set; } = "llama-3.3-70b-versatile";
        public string OpenAiModelName { get; set; } = "gpt-4o-mini";

        /// <summary>
        /// System prompt for keyboard build recommendation (RAG).
        /// </summary>
        public string RecommendationPrompt { get; set; } = @"You are a custom keyboard expert. Suggest a build (Kit, Switch, Keycap) based on the user request and provided parts.
Return ONLY JSON format: { ""KitId"": ""uuid"", ""SwitchId"": ""uuid"", ""KeycapId"": ""uuid"", ""Reasoning"": ""string"", ""TotalEstimatedPrice"": 0 }
Use the exact IDs provided in the context.";

        /// <summary>
        /// System prompt for AI-assisted order issue analysis (cancellation/refund).
        /// </summary>
        public string OrderIssuePrompt { get; set; } = @"You are a specialized Marketplace Support Assistant. 
Analyze the customer's cancellation or refund request against the order data.
Categorize the request, determine sentiment, and check for obvious policy violations (e.g. asking for return after order completed).
Return a concise summary and a recommendation (Approve, Reject, or Escalate).
Format: JSON with fields 'Category', 'Sentiment', 'Summary', 'Recommendation', 'ConfidenceScore'.";

        public QdrantSettings Qdrant { get; set; } = new();
    }


    public class QdrantSettings
    {
        public string Url { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int Port { get; set; } = 6334;
    }
}
