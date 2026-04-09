using System;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.AI
{
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates a vector embedding for the given text using Google Gemini API.
        /// </summary>
        /// <param name="text">The text to embed.</param>
        /// <returns>A float array representing the embedding vector.</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
}
