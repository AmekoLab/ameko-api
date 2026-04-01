using Xunit;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class OrderIssueTests
    {
        [Fact]
        public void OrderIssue_Creation_WithReturnRequest_SetsCorrectly()
        {
            // Arrange & Act
            var orderIssue = new OrderIssue
            {
                Id = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Type = OrderIssueType.ReturnRequest,
                Status = OrderIssueStatus.Pending,
                Reason = "Product defective",
                Description = "Item stopped working after 2 days",
                EvidenceUrl = "https://example.com/evidence.jpg",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(OrderIssueType.ReturnRequest, orderIssue.Type);
            Assert.Equal(OrderIssueStatus.Pending, orderIssue.Status);
            Assert.NotNull(orderIssue.Reason);
        }

        [Fact]
        public void OrderIssue_WithCancelRequest_CreatesCorrectly()
        {
            // Arrange & Act
            var orderIssue = new OrderIssue
            {
                Id = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Type = OrderIssueType.CancelRequest,
                Status = OrderIssueStatus.Pending,
                Reason = "Changed mind",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(OrderIssueType.CancelRequest, orderIssue.Type);
        }

        [Fact]
        public void OrderIssueLog_Creation_WithAction()
        {
            // Arrange & Act
            var issueLog = new OrderIssueLog
            {
                Id = Guid.NewGuid(),
                OrderIssueId = Guid.NewGuid(),
                ActionById = Guid.NewGuid(),
                ActionByRole = RoleType.Customer,
                Action = OrderIssueAction.Create,
                Comment = "Customer created return request",
                CreatedAt = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(OrderIssueAction.Create, issueLog.Action);
            Assert.Equal(RoleType.Customer, issueLog.ActionByRole);
        }

        [Theory]
        [InlineData(OrderIssueType.CancelRequest)]
        [InlineData(OrderIssueType.ReturnRequest)]
        [InlineData(OrderIssueType.WarrantyClaim)]
        public void OrderIssueType_EnumValues_AreValid(OrderIssueType type)
        {
            // Arrange & Act
            var issue = new OrderIssue { Type = type };

            // Assert
            Assert.Equal(type, issue.Type);
        }

        [Theory]
        [InlineData(OrderIssueStatus.Pending)]
        [InlineData(OrderIssueStatus.InProgress)]
        [InlineData(OrderIssueStatus.ShopAccepted)]
        [InlineData(OrderIssueStatus.Rejected)]
        [InlineData(OrderIssueStatus.Completed)]
        public void OrderIssueStatus_EnumValues_AreValid(OrderIssueStatus status)
        {
            // Arrange & Act
            var issue = new OrderIssue { Status = status };

            // Assert
            Assert.Equal(status, issue.Status);
        }

        [Theory]
        [InlineData(OrderIssueAction.Create)]
        [InlineData(OrderIssueAction.ShopApprove)]
        [InlineData(OrderIssueAction.ShopReject)]
        [InlineData(OrderIssueAction.AdminDecision)]
        [InlineData(OrderIssueAction.UserCancel)]
        public void OrderIssueAction_EnumValues_AreValid(OrderIssueAction action)
        {
            // Arrange & Act
            var log = new OrderIssueLog { Action = action };

            // Assert
            Assert.Equal(action, log.Action);
        }

        [Fact]
        public void OrderIssue_StatusProgression_Pending_To_ShopAccepted()
        {
            // Arrange
            var issue = new OrderIssue { Status = OrderIssueStatus.Pending };

            // Act
            issue.Status = OrderIssueStatus.ShopAccepted;

            // Assert
            Assert.Equal(OrderIssueStatus.ShopAccepted, issue.Status);
        }

        [Fact]
        public void OrderIssue_WithMultipleIssues_CollectionWorks()
        {
            // Arrange
            var order = new Order
            {
                OrderIssues = new List<OrderIssue>()
            };

            var issue1 = new OrderIssue { Type = OrderIssueType.ReturnRequest };
            var issue2 = new OrderIssue { Type = OrderIssueType.WarrantyClaim };

            // Act
            order.OrderIssues.Add(issue1);
            order.OrderIssues.Add(issue2);

            // Assert
            Assert.Equal(2, order.OrderIssues.Count);
        }
    }
}
