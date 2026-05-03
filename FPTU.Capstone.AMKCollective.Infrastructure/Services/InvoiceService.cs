using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;

        static InvoiceService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public InvoiceService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<byte[]> GenerateOrderInvoiceAsync(Guid requesterId, Guid orderId, CancellationToken token = default)
        {
            var order = await _unitOfWork.Orders.GetOrderDetailByIdAsync(orderId)
                ?? throw new KeyNotFoundException("Order not found.");

            // Verify requester is customer or shop owner
            var shopProfile = await _unitOfWork.Shops.GetByUserIdAsync(requesterId, token);
            bool isShopOwner = shopProfile != null && order.ShopId == shopProfile.Id;
            bool isCustomer = order.CustomerId == requesterId;

            if (!isCustomer && !isShopOwner)
                throw new UnauthorizedAccessException("You do not have permission to access this invoice.");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, order));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container)
        {
            container.PaddingBottom(20).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("AMKEKOLAB")
                        .Bold().FontSize(22).FontColor("#1a1a1a");
                    col.Item().PaddingTop(2).Text("Nền tảng bàn phím cơ thủ công")
                        .FontSize(8).FontColor("#666666");
                    col.Item().Text("Artisan Mechanical Keyboard Collective")
                        .FontSize(8).FontColor("#888888");
                });

                row.ConstantItem(180).AlignCenter().Column(col =>
                {
                    col.Item().Text("PHIẾU MUA HÀNG")
                        .Bold().FontSize(16).FontColor("#1a1a1a");
                    col.Item().PaddingTop(4).Text("INVOICE")
                        .FontSize(9).FontColor("#666666");
                });
            });
        }

        private static void ComposeContent(IContainer container, Order order)
        {
            container.Column(col =>
            {
                // Divider
                col.Item().LineHorizontal(1).LineColor("#cccccc");
                
                col.Item().PaddingVertical(14).Row(row =>
                {
                    // Left: bill to
                    row.RelativeItem(3).Column(c =>
                    {
                        c.Item().Text("THÔNG TIN NHẬN HÀNG").Bold().FontSize(8).FontColor("#999999");
                        c.Item().PaddingTop(6).Text(order.ReceiverName).Bold().FontSize(12).FontColor("#1a1a1a");
                        c.Item().PaddingTop(2).Text(order.ReceiverPhone).FontSize(9).FontColor("#555555");
                        c.Item().PaddingTop(2).Text(order.ShippingAddress).FontSize(9).FontColor("#555555");
                    });

                    // Right: invoice meta
                    row.RelativeItem(2).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Mã phiếu / Invoice #").FontSize(8).FontColor("#999999");
                            r.ConstantItem(110).AlignRight().Text(order.Id.ToString().Substring(0, 8).ToUpper()).Bold().FontSize(11);
                        });
                        c.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text("Ngày lập / Date").FontSize(8).FontColor("#999999");
                            r.ConstantItem(110).AlignRight().Text(order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                        });
                        c.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text("Cửa hàng / Shop").FontSize(8).FontColor("#999999");
                            r.ConstantItem(110).AlignRight().Text(order.Shop?.ShopName ?? "—").FontSize(9);
                        });
                        c.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text("Trạng thái / Status").FontSize(8).FontColor("#999999");
                            r.ConstantItem(110).AlignRight().Text(order.OrderStatus.ToString()).Bold().FontSize(9).FontColor("#1a1a1a");
                        });
                        c.Item().PaddingTop(3).Row(r =>
                        {
                            r.RelativeItem().Text("Thanh toán / Payment").FontSize(8).FontColor("#999999");
                            r.ConstantItem(110).AlignRight().Text(order.PaymentStatus.ToString()).FontSize(9).FontColor("#27ae60");
                        });
                    });
                });

                // Items table
                col.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.4f); // order number
                        cols.RelativeColumn(2.4f); // product
                        cols.RelativeColumn(0.8f); // qty
                        cols.RelativeColumn(1.8f); // unit price
                        cols.RelativeColumn(1.5f); // discount
                        cols.RelativeColumn(1.8f); // final price
                    });

                    // Header row
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background("#1a1a1a").Padding(8);

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor("#ffffff").FontSize(8);
                        h.Cell().Element(HeaderCell).Text("Tên sản phẩm / Product").Bold().FontColor("#ffffff").FontSize(8);
                        h.Cell().Element(HeaderCell).AlignCenter().Text("SL / Qty").Bold().FontColor("#ffffff").FontSize(8);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Đơn giá / Unit Price").Bold().FontColor("#ffffff").FontSize(8);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Giảm / Discount").Bold().FontColor("#ffffff").FontSize(8);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Thành tiền / Total").Bold().FontColor("#ffffff").FontSize(8);
                    });

                    bool isOdd = true;
                    int itemIndex = 1;
                    foreach (var item in order.OrderItems.Where(i => i.ItemStatus == Domain.Enums.OrderItemStatus.Active))
                    {
                        string rowBg = isOdd ? "#ffffff" : "#f9f9f9";
                        isOdd = !isOdd;

                        static IContainer DataCell(IContainer c, string bg) =>
                            c.Background(bg).Padding(7);

                        var totalDiscount = item.ShopAllocatedDiscount + item.SystemAllocatedDiscount;

                        table.Cell().Element(c => DataCell(c, rowBg)).AlignCenter().Text(itemIndex.ToString()).FontSize(9);
                        table.Cell().Element(c => DataCell(c, rowBg)).Column(inner =>
                        {
                            inner.Item().Text(item.ProductName).SemiBold().FontSize(9);
                            if (item.IsCustom)
                                inner.Item().PaddingTop(1).Text("Tuỳ chỉnh / Custom Build").FontSize(7).FontColor("#999999").Italic();
                        });
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignCenter().Text(item.Quantity.ToString()).FontSize(9);
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight().Text(FormatVnd(item.UnitPrice)).FontSize(9);
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight()
                            .Text(totalDiscount > 0 ? $"-{FormatVnd(totalDiscount)}" : "—").FontSize(9).FontColor("#e74c3c");
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight().Text(FormatVnd(item.FinalPrice)).Bold().FontSize(9);
                        
                        itemIndex++;
                    }
                });

                // Totals
                col.Item().PaddingTop(16).AlignRight().Width(260).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor("#dddddd");
                    c.Item().PaddingTop(6).PaddingBottom(2).Row(r =>
                    {
                        r.RelativeItem().Text("Tạm tính / Subtotal").FontSize(9).FontColor("#555555");
                        r.ConstantItem(110).AlignRight().Text(FormatVnd(order.SubTotal)).FontSize(9);
                    });
                    if (order.DiscountAmount > 0)
                    {
                        c.Item().PaddingBottom(2).Row(r =>
                        {
                            r.RelativeItem().Text("Voucher cửa hàng / Shop").FontSize(9).FontColor("#e74c3c");
                            r.ConstantItem(110).AlignRight().Text($"-{FormatVnd(order.DiscountAmount)}").FontSize(9).FontColor("#e74c3c").Bold();
                        });
                    }
                    if (order.SystemDiscountAmount > 0)
                    {
                        c.Item().PaddingBottom(2).Row(r =>
                        {
                            r.RelativeItem().Text("Voucher hệ thống / Platform").FontSize(9).FontColor("#e74c3c");
                            r.ConstantItem(110).AlignRight().Text($"-{FormatVnd(order.SystemDiscountAmount)}").FontSize(9).FontColor("#e74c3c").Bold();
                        });
                    }
                    c.Item().PaddingTop(6).LineHorizontal(1).LineColor("#1a1a1a");
                    c.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text("TỔNG CỘNG / TOTAL").Bold().FontSize(13).FontColor("#1a1a1a");
                        r.ConstantItem(110).AlignRight().Text(FormatVnd(order.TotalAmount)).Bold().FontSize(13).FontColor("#1a1a1a");
                    });
                });

                // Special Notice
                col.Item().PaddingTop(20).BorderTop(0.5f).BorderColor("#e74c3c").BorderBottom(0.5f).PaddingVertical(10)
                    .Background("#fef5f5").Padding(10).Column(c =>
                    {
                        c.Item().Text("LƯU Ý QUAN TRỌNG / IMPORTANT NOTICE").Bold().FontSize(9).FontColor("#c0392b");
                        c.Item().PaddingTop(4).Text("Khách hàng vui lòng kiểm tra và xác nhận tình trạng sản phẩm trước khi nhận hàng. Nếu sản phẩm bị trầy xước, bể, vỡ hoặc móp méo vui lòng hoàn trả lại nhân viên giao hàng.")
                            .FontSize(8).FontColor("#555555");
                        c.Item().PaddingTop(3).Text("Please check and confirm the condition of products before delivery. If the product is scratched, broken, cracked or dented, please return it to the delivery staff.")
                            .FontSize(8).FontColor("#555555").Italic();
                    });

                if (!string.IsNullOrWhiteSpace(order.Note))
                {
                    col.Item().PaddingTop(20).Column(c =>
                    {
                        c.Item().Text("GHI CHÚ / NOTE").Bold().FontSize(8).FontColor("#999999");
                        c.Item().PaddingTop(4).Text(order.Note).FontSize(9).FontColor("#555555");
                    });
                }
            });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.BorderTop(0.5f).BorderColor("#dddddd").PaddingTop(10).PaddingBottom(5)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Cảm ơn bạn đã mua hàng tại AMEKOLAB")
                            .FontSize(8).FontColor("#999999").Italic();
                        col.Item().PaddingTop(1).Text("Thank you for shopping at AMEKOLAB")
                            .FontSize(8).FontColor("#999999").Italic();
                    });
                    
                    row.ConstantItem(90).AlignRight()
                        .Text(x =>
                        {
                            x.Span("Trang ").FontSize(7).FontColor("#999999");
                            x.CurrentPageNumber().FontSize(7).FontColor("#999999");
                            x.Span(" / ").FontSize(7).FontColor("#999999");
                            x.TotalPages().FontSize(7).FontColor("#999999");
                        });
                });
        }

        private static string FormatVnd(decimal amount) =>
            $"{amount:N0} ₫";
    }
}
