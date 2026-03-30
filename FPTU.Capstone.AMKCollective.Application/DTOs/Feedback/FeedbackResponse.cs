using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Feedback
{
    public class FeedbackResponse
    {
        public Guid FeedbackId { get; set; }
        public Guid OrderId { get; set; }
        public Guid ShopId { get; set; }

        public Guid FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public string? FromUserAvatar { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();

        public DateTime CreatedDate { get; set; }

        public string? ShopReply { get; set; }
        public DateTime? ShopRepliedAt { get; set; }
    }
}
