using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ContentModerationService : IContentModerationService
    {
        private readonly HashSet<string> _badWords = new();
        private readonly IHostEnvironment _env;
        private readonly IMemoryCache _cache;

        public ContentModerationService(IHostEnvironment env, IMemoryCache cache)
        {
            _env = env;
            _cache = cache;
            LoadBadWords();
        }

        public async Task<string> ProcessContentAsync(Guid userId, string content, string actionType, int limitPerMinute, CancellationToken ct = default)
        {
            // 1. Rate Limiting Check
            if (limitPerMinute > 0)
            {
                var cacheKey = $"RateLimit_{actionType}_{userId}_{DateTime.UtcNow:yyyyMMddHHmm}";
                if (_cache.TryGetValue(cacheKey, out int count))
                {
                    if (count >= limitPerMinute)
                    {
                        throw new InvalidOperationException($"Rate limit exceeded for {actionType}. Please wait a moment.");
                    }
                    _cache.Set(cacheKey, count + 1, TimeSpan.FromMinutes(2));
                }
                else
                {
                    _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(2));
                }
            }

            // 2. Content Masking
            return await MaskBadWordsAsync(content, ct);
        }

        private void LoadBadWords()
        {
            var path = Path.Combine(_env.ContentRootPath, "VietnameseBadWord.txt");
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    var word = line.Trim().ToLower();
                    if (!string.IsNullOrEmpty(word))
                    {
                        _badWords.Add(word);
                    }
                }
            }
        }

        public async Task<ModerationResult> ValidateContentAsync(string content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new ModerationResult { Severity = "LOW", Reason = "Empty content" };

            var normalizedContent = Normalize(content);
            var words = normalizedContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int matchCount = 0;
            List<string> matchedWords = new();

            foreach (var badWord in _badWords)
            {
                // Simple pattern for disguised words (e.g. v*c*l -> vcl)
                // We also check for exact contains
                if (normalizedContent.Contains(badWord))
                {
                    matchCount++;
                    matchedWords.Add(badWord);
                }
            }

            // Heuristic Classification
            if (matchCount >= 3 || matchedWords.Any(w => IsHighSeverity(w)))
            {
                return new ModerationResult
                {
                    Severity = "HIGH",
                    Reason = $"Detected harmful/prohibited content: {string.Join(", ", matchedWords.Take(3))}",
                    Suggestion = "Please remove offensive language or toxic content."
                };
            }

            if (matchCount >= 1)
            {
                return new ModerationResult
                {
                    Severity = "MEDIUM",
                    Reason = "Content contains inappropriate language.",
                    Suggestion = "Please rephrase your content to be more polite."
                };
            }

            return new ModerationResult
            {
                Severity = "LOW",
                Reason = "Content analysis passed."
            };
        }

        public async Task<string> MaskBadWordsAsync(string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;

            var result = content;

            foreach (var badWord in _badWords)
            {
                // Create a regex pattern that allows optional obfuscators (*, ., _, -) between letters
                // e.g. "vcl" -> "v[*._-]*c[*._-]*l"
                var pattern = string.Join("[\\*\\.\\_\\-]*", badWord.ToCharArray().Select(c => Regex.Escape(c.ToString())));
                
                // Match case-insensitive and on word boundaries if possible
                // Note: word boundaries (\b) might be tricky with Vietnamese characters, so we just use regex replacement
                try
                {
                    result = Regex.Replace(result, pattern, m => new string('*', m.Length), RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Fallback to simple replacement if regex fails for some reason
                    result = result.Replace(badWord, new string('*', badWord.Length), StringComparison.OrdinalIgnoreCase);
                }
            }

            return result;
        }

        private string Normalize(string text)
        {
            text = text.ToLower();
            // Remove common obfuscators inside words (e.g., v*c*l -> vcl)
            text = text.Replace("*", "").Replace(".", "").Replace("_", "").Replace("-", "");
            // Keep alphanumeric and common Vietnamese characters
            text = Regex.Replace(text, @"[^a-z0-9\sàáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]", "");
            return text;
        }

        private bool IsHighSeverity(string word)
        {
            string[] highSeverityWords = { "địt", "lồn", "cặc", "bú cu", "phò", "fuck", "đụ", "điếm", "chịch", "nứng", "lozz", "cak" };
            return highSeverityWords.Any(h => word == h || word.Contains(h));
        }
    }
}
