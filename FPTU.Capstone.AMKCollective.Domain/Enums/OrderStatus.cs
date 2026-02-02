
namespace FPTU.Capstone.AMKCollective.Domain.Enums
{
    public enum OrderStatus
    {
        InCart, // Đơn trong giỏ hàng chưa thanh toán

        Pending, // Khách đã checkout. Chờ shop xác nhận/thanh toán

        Processing, // Shop đã nhận đơn và đang tiến hành build

        Shipped, // Shop đã giao cho đơn vị vận chuyển

        Completed, // Đã nhận hàng và hoàn tất

        Cancelled, // Đơn bị hủy

        Returning, // Khách đang gửi trả hàng

        Returned, // Shop đã nhận lại hàng hoàn trả

        Refunded // Đã hoàn tiền
    }
}
