using Xunit;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class WalletTests
    {
        [Fact]
        public void Wallet_Creation_InitializesCorrectly()
        {
            // Arrange & Act
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Balance = 1000000,
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(1000000, wallet.Balance);
            Assert.NotEqual(Guid.Empty, wallet.Id);
        }

        [Fact]
        public void Wallet_UpdateBalance_ChangesCorrectly()
        {
            // Arrange
            var wallet = new Wallet { Balance = 1000000 };

            // Act
            wallet.Balance += 500000; // Deposit

            // Assert
            Assert.Equal(1500000, wallet.Balance);
        }

        [Fact]
        public void Wallet_DeductBalance_ChangesCorrectly()
        {
            // Arrange
            var wallet = new Wallet { Balance = 1000000 };

            // Act
            wallet.Balance -= 300000; // Withdrawal

            // Assert
            Assert.Equal(700000, wallet.Balance);
        }

        [Fact]
        public void WithdrawalRequest_Creation_SetsPendingStatus()
        {
            // Arrange & Act
            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Amount = 500000,
                BankName = "Vietcombank",
                BankAccountNumber = "1234567890",
                BankAccountName = "John Doe",
                Status = WithdrawalStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(WithdrawalStatus.Pending, withdrawal.Status);
            Assert.Equal(500000, withdrawal.Amount);
            Assert.Null(withdrawal.ProcessedAt);
        }

        [Fact]
        public void WithdrawalRequest_Approval_UpdatesStatus()
        {
            // Arrange
            var withdrawal = new WithdrawalRequest { Status = WithdrawalStatus.Pending };
            var adminId = Guid.NewGuid();
            var processTime = DateTime.UtcNow;

            // Act
            withdrawal.Status = WithdrawalStatus.Approved;
            withdrawal.AdminId = adminId;
            withdrawal.ProcessedAt = processTime;

            // Assert
            Assert.Equal(WithdrawalStatus.Approved, withdrawal.Status);
            Assert.Equal(adminId, withdrawal.AdminId);
            Assert.NotNull(withdrawal.ProcessedAt);
        }

        [Fact]
        public void WithdrawalRequest_Rejection_StorsBankDetails()
        {
            // Arrange & Act
            var withdrawal = new WithdrawalRequest
            {
                Status = WithdrawalStatus.Rejected,
                AdminMessage = "Account verification failed",
                BankAccountName = "Jane Smith"
            };

            // Assert
            Assert.Equal(WithdrawalStatus.Rejected, withdrawal.Status);
            Assert.NotNull(withdrawal.AdminMessage);
        }

        [Theory]
        [InlineData(WithdrawalStatus.Pending)]
        [InlineData(WithdrawalStatus.Approved)]
        [InlineData(WithdrawalStatus.Rejected)]
        public void WithdrawalStatus_EnumValues_AreValid(WithdrawalStatus status)
        {
            // Arrange & Act
            var withdrawal = new WithdrawalRequest { Status = status };

            // Assert
            Assert.Equal(status, withdrawal.Status);
        }

        [Fact]
        public void Transaction_CreatedForWithdrawal()
        {
            // Arrange & Act
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = Guid.NewGuid(),
                Amount = 500000,
                Type = TransactionType.Withdrawal,
                Description = "Bank transfer to Vietcombank",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(TransactionType.Withdrawal, transaction.Type);
            Assert.Equal(500000, transaction.Amount);
        }

        [Theory]
        [InlineData(TransactionType.OrderPayment)]
        [InlineData(TransactionType.OrderRefund)]
        [InlineData(TransactionType.SalesRevenue)]
        [InlineData(TransactionType.Deposit)]
        [InlineData(TransactionType.Withdrawal)]
        [InlineData(TransactionType.SalesPending)]
        public void Transaction_AllTypes_AreValid(TransactionType type)
        {
            // Arrange & Act
            var transaction = new Transaction { Type = type };

            // Assert
            Assert.Equal(type, transaction.Type);
        }

        [Fact]
        public void Transaction_WithPositiveAmount_IsValid()
        {
            // Arrange & Act
            var transaction = new Transaction
            {
                Amount = 1000000,
                Type = TransactionType.Deposit
            };

            // Assert
            Assert.True(transaction.Amount > 0);
        }

        [Fact]
        public void Wallet_CanHaveMultipleTransactions()
        {
            // Arrange
            var wallet = new Wallet();
            wallet.Transactions = new List<Transaction>();

            // Act
            wallet.Transactions.Add(new Transaction { Type = TransactionType.Deposit });
            wallet.Transactions.Add(new Transaction { Type = TransactionType.Withdrawal });

            // Assert
            Assert.Equal(2, wallet.Transactions.Count);
        }
    }
}
