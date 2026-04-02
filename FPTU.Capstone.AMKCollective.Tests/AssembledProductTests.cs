using Xunit;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class AssembledProductTests
    {
        [Fact]
        public void AssembledProduct_Creation_WithBasicProps()
        {
            // Arrange & Act
            var product = new AssembledProduct
            {
                Id = Guid.NewGuid(),
                Name = "Custom Mechanical Keyboard",
                Price = 5000000,
                Description = "Custom built keyboard with RGB lighting",
                View3DUrl = "https://example.com/view3d/kbd1"
            };

            // Assert
            Assert.Equal("Custom Mechanical Keyboard", product.Name);
            Assert.Equal(5000000, product.Price);
            Assert.NotNull(product.View3DUrl);
        }

        [Fact]
        public void AssembledProduct_WithSpecifications_StoresCorrectly()
        {
            // Arrange & Act
            var product = new AssembledProduct
            {
                Id = Guid.NewGuid(),
                Name = "Gaming Keyboard",
                Price = 3500000,
                Layout = "Full Size",
                Mounting = "Gasket",
                PCB = "PCB Type: Hotswap",
                Connection = "Wireless 2.4GHz",
                Battery = "4000mAh Li-Po"
            };

            // Assert
            Assert.Equal("Full Size", product.Layout);
            Assert.Equal("Gasket", product.Mounting);
            Assert.Equal("PCB Type: Hotswap", product.PCB);
        }

        [Fact]
        public void AssembledProduct_WithImages_StoresAllImageUrls()
        {
            // Arrange & Act
            var product = new AssembledProduct
            {
                Name = "Keyboard",
                Price = 4000000,
                Image1 = "https://example.com/img1.jpg",
                Image2 = "https://example.com/img2.jpg",
                Image3 = "https://example.com/img3.jpg"
            };

            // Assert
            Assert.NotNull(product.Image1);
            Assert.NotNull(product.Image2);
            Assert.NotNull(product.Image3);
        }

        [Fact]
        public void AssembledProduct_HasOrderItems_Collection()
        {
            // Arrange
            var product = new AssembledProduct { Name = "Keyboard" };
            product.OrderItems = new List<OrderItem>();

            // Act
            product.OrderItems.Add(new OrderItem());
            product.OrderItems.Add(new OrderItem());

            // Assert
            Assert.Equal(2, product.OrderItems.Count);
        }

        [Fact]
        public void AssembledProduct_HasProductDetails_Collection()
        {
            // Arrange
            var product = new AssembledProduct { Name = "Keyboard" };
            product.ProductAssembledDetails = new List<ProductAssembledDetail>();

            // Act
            var detail = new ProductAssembledDetail { Quantity = 1, SoundUrl = "https://example.com/sound.mp3" };
            product.ProductAssembledDetails.Add(detail);

            // Assert
            Assert.Single(product.ProductAssembledDetails);
        }

        [Theory]
        [InlineData(1000000)]
        [InlineData(5000000)]
        [InlineData(8000000)]
        public void AssembledProduct_Price_VariousAmounts(decimal price)
        {
            // Arrange & Act
            var product = new AssembledProduct { Price = price };

            // Assert
            Assert.Equal(price, product.Price);
        }

        [Fact]
        public void AssembledProduct_WithQuantity_StoresCorrectly()
        {
            // Arrange & Act
            var product = new AssembledProduct
            {
                Name = "Keyboard",
                Quantity = 5
            };

            // Assert
            Assert.Equal(5, product.Quantity);
        }
    }
}
