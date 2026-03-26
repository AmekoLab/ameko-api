using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderAddonRequest
    {
        // ID của phiên build hiện tại
        public Guid SessionId { get; set; }

        // ID của linh kiện lẻ (Switch hoặc Keycap) mà khách vừa chọn đổi
        public Guid ComponentId { get; set; }

        // Số lượng nút khách đổi (Mặc định là 1, nếu khách bôi đen 4 nút WASD thì FE truyền 4)
        public int Quantity { get; set; } = 1;

        // Ghi chú vị trí nút trên phím ảo để thợ biết đường cắm (VD: "Nút W", "Cụm WASD")
        public string PositionNote { get; set; } = string.Empty;
    }
}
