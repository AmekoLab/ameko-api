
namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum OrderStatus
    {
       

        Pending = 0, // Khách đã checkout. Chờ shop xác nhận/thanh toán
        InCart = 1, // Đơn trong giỏ hàng chưa thanh toán

        Processing = 2, // Shop đã nhận đơn và đang tiến hành build

        Shipped = 3, // Shop đã giao cho đơn vị vận chuyển

        Completed = 4, // Đã nhận hàng và hoàn tất

        Cancelled = 5, // Đơn bị hủy

        Returning = 6, // Khách đang gửi trả hàng

        Returned = 7, // Shop đã nhận lại hàng hoàn trả

        Refunded = 8 // Đã hoàn tiền
    }
}
