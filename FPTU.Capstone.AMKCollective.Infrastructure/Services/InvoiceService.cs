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
            container.PaddingBottom(16).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("AMK COLLECTIVE")
                        .Bold().FontSize(20).FontColor("#1a1a1a");
                    col.Item().Text("artisan keyboard marketplace")
                        .FontSize(9).FontColor("#888888");
                });

                row.ConstantItem(120).AlignRight().Column(col =>
                {
                    col.Item().Text("INVOICE")
                        .Bold().FontSize(16).FontColor("#333333");
                });
            });
        }

        private static void ComposeContent(IContainer container, Order order)
        {
            container.Column(col =>
            {
                // Divider
                col.Item().LineHorizontal(1).LineColor("#dddddd");
                col.Item().PaddingVertical(12).Row(row =>
                {
                    // Left: bill to
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("BILL TO").Bold().FontSize(8).FontColor("#888888");
                        c.Item().PaddingTop(4).Text(order.ReceiverName).Bold().FontSize(11);
                        c.Item().Text(order.ReceiverPhone).FontColor("#555555");
                        c.Item().Text(order.ShippingAddress).FontColor("#555555");
                    });

                    // Right: invoice meta
                    row.ConstantItem(200).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Invoice #").FontColor("#888888");
                            r.RelativeItem().AlignRight().Text(order.Id.ToString()[..8].ToUpper()).Bold();
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Date").FontColor("#888888");
                            r.RelativeItem().AlignRight().Text(order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Shop").FontColor("#888888");
                            r.RelativeItem().AlignRight().Text(order.Shop?.ShopName ?? "—");
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Status").FontColor("#888888");
                            r.RelativeItem().AlignRight().Text(order.OrderStatus.ToString()).Bold();
                        });
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Payment").FontColor("#888888");
                            r.RelativeItem().AlignRight().Text(order.PaymentStatus.ToString());
                        });
                    });
                });

                // Items table
                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4); // product
                        cols.RelativeColumn(1); // qty
                        cols.RelativeColumn(2); // unit price
                        cols.RelativeColumn(2); // discount
                        cols.RelativeColumn(2); // final price
                    });

                    // Header row
                    static IContainer HeaderCell(IContainer c) =>
                        c.Background("#1a1a1a").Padding(6);

                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Product").Bold().FontColor("#ffffff").FontSize(9);
                        h.Cell().Element(HeaderCell).AlignCenter().Text("Qty").Bold().FontColor("#ffffff").FontSize(9);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Unit Price").Bold().FontColor("#ffffff").FontSize(9);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Discount").Bold().FontColor("#ffffff").FontSize(9);
                        h.Cell().Element(HeaderCell).AlignRight().Text("Final Price").Bold().FontColor("#ffffff").FontSize(9);
                    });

                    bool isOdd = true;
                    foreach (var item in order.OrderItems.Where(i => i.ItemStatus == Domain.Enums.OrderItemStatus.Active))
                    {
                        string rowBg = isOdd ? "#ffffff" : "#f7f7f7";
                        isOdd = !isOdd;

                        static IContainer DataCell(IContainer c, string bg) =>
                            c.Background(bg).Padding(6);

                        var totalDiscount = item.ShopAllocatedDiscount + item.SystemAllocatedDiscount;

                        table.Cell().Element(c => DataCell(c, rowBg)).Column(inner =>
                        {
                            inner.Item().Text(item.ProductName).SemiBold();
                            if (item.IsCustom)
                                inner.Item().Text("Custom Build").FontSize(8).FontColor("#888888").Italic();
                        });
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignCenter().Text(item.Quantity.ToString());
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight().Text(FormatVnd(item.UnitPrice));
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight()
                            .Text(totalDiscount > 0 ? $"-{FormatVnd(totalDiscount)}" : "—").FontColor("#e74c3c");
                        table.Cell().Element(c => DataCell(c, rowBg)).AlignRight().Text(FormatVnd(item.FinalPrice)).Bold();
                    }
                });

                // Totals
                col.Item().PaddingTop(16).AlignRight().Width(240).Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor("#dddddd");
                    c.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text("Subtotal").FontColor("#555555");
                        r.ConstantItem(100).AlignRight().Text(FormatVnd(order.SubTotal));
                    });
                    if (order.DiscountAmount > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Shop Voucher").FontColor("#e74c3c");
                            r.ConstantItem(100).AlignRight().Text($"-{FormatVnd(order.DiscountAmount)}").FontColor("#e74c3c");
                        });
                    }
                    if (order.SystemDiscountAmount > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Platform Voucher").FontColor("#e74c3c");
                            r.ConstantItem(100).AlignRight().Text($"-{FormatVnd(order.SystemDiscountAmount)}").FontColor("#e74c3c");
                        });
                    }
                    c.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1a1a1a");
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL").Bold().FontSize(12);
                        r.ConstantItem(100).AlignRight().Text(FormatVnd(order.TotalAmount)).Bold().FontSize(12);
                    });
                });

                if (!string.IsNullOrWhiteSpace(order.Note))
                {
                    col.Item().PaddingTop(20).Column(c =>
                    {
                        c.Item().Text("Note").Bold().FontSize(9).FontColor("#888888");
                        c.Item().PaddingTop(2).Text(order.Note).FontColor("#555555");
                    });
                }
            });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.BorderTop(0.5f).BorderColor("#dddddd").PaddingTop(8)
                .Row(row =>
                {
                    row.RelativeItem().Text("Thank you for shopping at AMK Collective.")
                        .FontSize(8).FontColor("#888888").Italic();
                    row.ConstantItem(80).AlignRight()
                        .Text(x =>
                        {
                            x.Span("Page ").FontSize(8).FontColor("#888888");
                            x.CurrentPageNumber().FontSize(8).FontColor("#888888");
                            x.Span(" / ").FontSize(8).FontColor("#888888");
                            x.TotalPages().FontSize(8).FontColor("#888888");
                        });
                });
        }

        private static string FormatVnd(decimal amount) =>
            $"{amount:N0} ₫";
    }
}
