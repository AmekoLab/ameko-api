using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    /// <summary>
    /// Kết quả trả về khi FE hỏi "có những linh kiện nào có thể add-on tại vị trí này?"
    /// </summary>
    public class AddonOptionsResponse
    {
        /// <summary>Loại addon: "switch" | "keycap" | ...</summary>
        public string AddonType { get; set; } = string.Empty;

        /// <summary>
        /// Với switch: lấy từ KitDesignOption (đã được shop đăng ký tương thích với kit).
        /// Với keycap: lấy thẳng từ Model (hàng artisan của shop, không cần rule kit).
        /// </summary>
        public List<AddonOptionItem> Items { get; set; } = new();

        public int TotalCount { get; set; }
    }  
}
