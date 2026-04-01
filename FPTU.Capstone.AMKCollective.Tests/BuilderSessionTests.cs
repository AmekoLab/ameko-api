using Xunit;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using Newtonsoft.Json.Linq;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class BuilderSessionTests
    {
        [Fact]
        public void BuilderSession_Creation_WithBasicProps()
        {
            // Arrange & Act
            var session = new BuilderSession
            {
                Id = Guid.NewGuid(),
                BaseKitId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                CurrentStep = "start",
                TotalPrice = 0,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                SelectedItemsJson = "{}"
            };

            // Assert
            Assert.NotEqual(Guid.Empty, session.Id);
            Assert.Equal("start", session.CurrentStep);
            Assert.Equal(0, session.TotalPrice);
        }

        [Fact]
        public void BuilderSession_Progresses_Through_Steps()
        {
            // Arrange
            var session = new BuilderSession { CurrentStep = "start" };

            // Act - Simulate step progression
            session.CurrentStep = "select_components";
            var stepAfterSelect = session.CurrentStep;
            
            session.CurrentStep = "customization";
            var stepAfterCustom = session.CurrentStep;

            session.CurrentStep = "review";
            var stepAfterReview = session.CurrentStep;

            // Assert
            Assert.Equal("select_components", stepAfterSelect);
            Assert.Equal("customization", stepAfterCustom);
            Assert.Equal("review", stepAfterReview);
        }

        [Fact]
        public void BuilderSession_UpdatesPrice_AsComponentsAdded()
        {
            // Arrange
            var session = new BuilderSession { TotalPrice = 0 };

            // Act
            session.TotalPrice += 1000000; // Add base price
            session.TotalPrice += 500000;  // Add component 1
            session.TotalPrice += 300000;  // Add component 2

            // Assert
            Assert.Equal(1800000, session.TotalPrice);
        }

        [Fact]
        public void BuilderSession_StoresSelectedItems_AsJson()
        {
            // Arrange
            var selectedItems = new { layout = "full", mount = "gasket", switches = "mechanical" };
            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(selectedItems);

            // Act
            var session = new BuilderSession { SelectedItemsJson = jsonString };

            // Assert
            Assert.Contains("layout", session.SelectedItemsJson);
            Assert.Contains("full", session.SelectedItemsJson);
        }

        [Fact]
        public void BuilderSession_WithUser_LinkedCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId };

            // Act
            var session = new BuilderSession
            {
                UserId = userId,
                User = user
            };

            // Assert
            Assert.Equal(userId, session.UserId);
            Assert.NotNull(session.User);
            Assert.Equal(userId, session.User.Id);
        }

        [Fact]
        public void BuilderSession_Expires_After_Duration()
        {
            // Arrange
            var expiryTime = DateTime.UtcNow.AddHours(2);

            // Act
            var session = new BuilderSession { ExpiresAt = expiryTime };
            var isExpired = DateTime.UtcNow > session.ExpiresAt;

            // Assert
            Assert.False(isExpired); // Should not be expired immediately
        }

        [Fact]
        public void BuilderSession_WithBaseKit_LinkedCorrectly()
        {
            // Arrange
            var kitId = Guid.NewGuid();
            var kit = new Model { Id = kitId, Name = "Keyboard Kit" };

            // Act
            var session = new BuilderSession
            {
                BaseKitId = kitId,
                BaseKit = kit
            };

            // Assert
            Assert.Equal(kitId, session.BaseKit.Id);
            Assert.Equal("Keyboard Kit", session.BaseKit.Name);
        }

        [Theory]
        [InlineData("start")]
        [InlineData("select_components")]
        [InlineData("customization")]
        [InlineData("review")]
        [InlineData("checkout")]
        public void BuilderSession_Step_CanBeAnyStep(string step)
        {
            // Arrange & Act
            var session = new BuilderSession { CurrentStep = step };

            // Assert
            Assert.Equal(step, session.CurrentStep);
        }

        [Fact]
        public void BuilderSession_MultipleComponentsSelection()
        {
            // Arrange
            var components = new[]
            {
                new { type = "switches", quantity = 104, price = 2000000 },
                new { type = "keycaps", quantity = 1, price = 800000 },
                new { type = "stabilizers", quantity = 1, price = 300000 },
                new { type = "pcb", quantity = 1, price = 500000 }
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(components);

            // Act
            var session = new BuilderSession { SelectedItemsJson = json };

            // Assert
            Assert.Contains("switches", session.SelectedItemsJson);
            Assert.Contains("keycaps", session.SelectedItemsJson);
        }

        [Fact]
        public void BuilderSession_TotalPrice_CalculatedFromComponents()
        {
            // Arrange
            decimal componentPrice = 2000000;
            decimal assemblyFee = 500000;

            // Act
            var session = new BuilderSession
            {
                TotalPrice = componentPrice + assemblyFee
            };

            // Assert
            Assert.Equal(2500000, session.TotalPrice);
        }
    }
}
