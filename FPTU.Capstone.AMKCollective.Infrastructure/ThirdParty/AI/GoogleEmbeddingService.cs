using System;
using System.Linq;
using System.Threading.Tasks;
using Google.GenAI;
using Microsoft.Extensions.Options;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty.AI
{
    public class GoogleEmbeddingService : IEmbeddingService
    {
        private readonly AISettings _settings;

        public GoogleEmbeddingService(IOptions<AISettings> options)
        {
            _settings = options.Value;
            if (string.IsNullOrEmpty(_settings.GoogleApiKey))
            {
                throw new ArgumentNullException("Google API Key is not configured.");
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<float>();

            try
            {
                var client = new Client(apiKey: _settings.GoogleApiKey);
                
                var response = await client.Models.EmbedContentAsync(
                    model: _settings.GoogleModelName,
                    contents: text
                );

                // Google.GenAI 1.6.1: Values are double[], cast to float[]
                return response.Embeddings.First().Values.Select(v => (float)v).ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating embedding from Google Gemini: {ex.Message}", ex);
            }
        }
    }
}
