using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.AI
{
    public interface IWebSearchService
    {
        /// <summary>
        /// Tìm kiếm trên internet qua Tavily API.
        /// Trả về rỗng nếu API key chưa cấu hình hoặc có lỗi.
        /// </summary>
        Task<WebSearchResult> SearchAsync(string query, int maxResults = 5);
    }

    public class WebSearchResult
    {
        public List<WebSearchSource> Sources { get; set; } = new();
        public List<string> Images { get; set; } = new();
    }

    public class WebSearchSource
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
