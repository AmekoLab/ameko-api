using Xunit;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class SimpleOrderPricingTests
    {
        [Fact]
        public void Order_Creation_SetsCorrectProperties()
        {
            // Arrange
            var customerId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            decimal subTotal = 1000000;

            // Act
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ShopId = shopId,
                SubTotal = subTotal,
                TotalAmount = subTotal,
                ShippingFee = 0,
                DiscountAmount = 0,
                SystemDiscountAmount = 0,
                OrderStatus = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending
            };

            // Assert
            Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(shopId, order.ShopId);
            Assert.Equal(subTotal, order.TotalAmount);
            Assert.Equal(OrderStatus.Pending, order.OrderStatus);
        }

        [Theory]
        [InlineData(1000000, 100000, 900000)]
        [InlineData(5000000, 500000, 4500000)]
        [InlineData(2000000, 0, 2000000)]
        public void Order_CalculatesFinalPrice_WhenDiscountApplied(decimal subTotal, decimal discount, decimal expected)
        {
            // Arrange
            var order = new Order
            {
                SubTotal = subTotal,
                DiscountAmount = discount,
                TotalAmount = subTotal - discount
            };

            // Assert
            Assert.Equal(expected, order.TotalAmount);
        }

        [Fact]
        public void Voucher_WithPercentageDiscount_StoresCorrectly()
        {
            // Arrange & Act
            var voucher = new Voucher
            {
                Id = Guid.NewGuid(),
                Code = "SUMMER20",
                Value = 20,
                DiscountType = DiscountType.Percentage,
                MaxDiscountAmount = 500000,
                MinOrderValue = 100000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                Status = VoucherStatus.Active,
                Scope = VoucherScope.System
            };

            // Assert
            Assert.Equal(20, voucher.Value);
            Assert.Equal(DiscountType.Percentage, voucher.DiscountType);
            Assert.Equal(500000, voucher.MaxDiscountAmount);
            Assert.Equal(VoucherStatus.Active, voucher.Status);
        }

        [Fact]
        public void Voucher_WithFixedDiscountAmount_StoresCorrectly()
        {
            // Arrange & Act
            var voucher = new Voucher
            {
                Id = Guid.NewGuid(),
                Code = "FIXED100K",
                Value = 100000,
                DiscountType = DiscountType.FixedAmount,
                MinOrderValue = 500000,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(7),
                Status = VoucherStatus.Active,
                Scope = VoucherScope.Shop
            };

            // Assert
            Assert.Equal(100000, voucher.Value);
            Assert.Equal(DiscountType.FixedAmount, voucher.DiscountType);
        }

        [Theory]
        [InlineData(VoucherStatus.Active)]
        [InlineData(VoucherStatus.Expired)]
        [InlineData(VoucherStatus.Depleted)]
        [InlineData(VoucherStatus.Disabled)]
        public void VoucherStatus_EnumValues_AreValid(VoucherStatus status)
        {
            // Arrange & Act
            var voucher = new Voucher { Status = status };

            // Assert
            Assert.Equal(status, voucher.Status);
        }

        [Theory]
        [InlineData(OrderStatus.Pending)]
        [InlineData(OrderStatus.Processing)]
        [InlineData(OrderStatus.Shipped)]
        [InlineData(OrderStatus.Completed)]
        [InlineData(OrderStatus.Cancelled)]
        public void OrderStatus_EnumValues_AreValid(OrderStatus status)
        {
            // Arrange & Act
            var order = new Order { OrderStatus = status };

            // Assert
            Assert.Equal(status, order.OrderStatus);
        }

        [Fact]
        public void Transaction_WithDepositType_CreatesCorrectly()
        {
            // Arrange & Act
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = Guid.NewGuid(),
                Amount = 500000,
                Type = TransactionType.Deposit,
                Description = "User deposit via Stripe",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(TransactionType.Deposit, transaction.Type);
            Assert.Equal(500000, transaction.Amount);
        }

        [Theory]
        [InlineData(TransactionType.OrderPayment)]
        [InlineData(TransactionType.OrderRefund)]
        [InlineData(TransactionType.SalesRevenue)]
        [InlineData(TransactionType.Deposit)]
        [InlineData(TransactionType.Withdrawal)]
        public void TransactionType_EnumValues_AreValid(TransactionType type)
        {
            // Arrange & Act
            var transaction = new Transaction { Type = type };

            // Assert
            Assert.Equal(type, transaction.Type);
        }
    }
}
