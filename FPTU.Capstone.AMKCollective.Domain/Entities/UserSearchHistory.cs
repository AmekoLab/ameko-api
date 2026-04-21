using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class UserSearchHistory : BaseEntity
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string Keyword { get; set; } = string.Empty;

        // Phân loại tìm kiếm: "Shop", "Assembled", "Part"...
        public string SearchType { get; set; } = string.Empty;

        // Cộng dồn số lần search để làm mượt DB
        public int SearchCount { get; set; } = 1;

        // Lưu thời gian gần nhất để ưu tiên hiển thị gợi ý mới
        public DateTime LastSearchedAt { get; set; } = DateTime.UtcNow;
    }
}
