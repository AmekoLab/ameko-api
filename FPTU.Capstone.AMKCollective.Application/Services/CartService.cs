using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepo;
        private readonly IModelRepository _productRepo;
        private readonly IMapper _mapper;

        public CartService(ICartRepository cartRepo, IModelRepository productRepo, IMapper mapper)
        {
            _cartRepo = cartRepo;
            _productRepo = productRepo;
            _mapper = mapper;
        }
        public async Task<CartDto> GetCartAsync(Guid userId, CancellationToken token = default)
        {
            var cart = await _cartRepo.GetCartByUserIdAsync(userId, token);
            if (cart == null)
            {
                return new CartDto { UserId = userId, Items = new List<CartItemDto>() };
            }
            return _mapper.Map<CartDto>(cart);
        }

        public async Task AddToCartAsync(Guid userId, AddToCartDto request, CancellationToken token = default)
        {
            var product = await _productRepo.GetByIdAsync(request.ProductId);
            if (product == null) throw new KeyNotFoundException("Item not found");

            var cart = await _cartRepo.GetCartByUserIdAsync(userId, token);
            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                await _cartRepo.CreateCartAsync(cart, token);
                await _cartRepo.SaveChangesAsync(token); 
            }

            if (!request.IsCustom)
            {
                var existingItem = await _cartRepo.FindStandardItemAsync(cart.Id, request.ProductId, token);

                if (existingItem != null)
                {
                    existingItem.Quantity += request.Quantity;
                    existingItem.UnitPrice = product.Price;
                    await _cartRepo.UpdateItemAsync(existingItem, token);
                }
                else
                {
                    var newItem = new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        UnitPrice = product.Price,
                        IsCustom = false,
                        Status = CartItemStatus.Normal
                    };
                    await _cartRepo.AddItemAsync(newItem, token);
                }
            }
            else
            {
                decimal finalUnitPrice = product.Price; 

                if (request.ComponentIds != null && request.ComponentIds.Count > 0)
                {                
                    foreach (var compId in request.ComponentIds)
                    {                    
                        var component = await _productRepo.GetByIdAsync(compId);

                        if (component != null)
                        {
                            finalUnitPrice += component.Price;
                        }
                    }
                }

                var configJson = request.ComponentIds != null
                    ? JsonSerializer.Serialize(request.ComponentIds)
                    : null;

                var customItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = request.ProductId, // BaseKit ID
                    Quantity = request.Quantity,
                    UnitPrice = finalUnitPrice,
                    IsCustom = true,
                    CustomConfig = configJson,
                    Status = CartItemStatus.Normal
                };
                await _cartRepo.AddItemAsync(customItem, token);
            }

            await _cartRepo.SaveChangesAsync(token);
        }

        public async Task RemoveItemAsync(Guid userId, Guid cartItemId, CancellationToken token = default)
        {
            var item = await _cartRepo.GetItemByIdAsync(cartItemId, token);
            if (item.Cart.UserId != userId)
            {
                throw new UnauthorizedAccessException("You do not have permission to do this");
            }
            await _cartRepo.DeleteItemAsync(item, token);
            await _cartRepo.SaveChangesAsync(token);
        }

        public async Task UpdateQuantityAsync(Guid userId, Guid cartItemId, int quantity, CancellationToken token = default)
        {
            var item = await _cartRepo.GetItemByIdAsync(cartItemId, token);
            if (item == null) throw new KeyNotFoundException("Item not found");

            if (quantity <= 0)
            {
                await _cartRepo.DeleteItemAsync(item, token);
            }
            else
            {
                item.Quantity = quantity;
                await _cartRepo.UpdateItemAsync(item, token);
            }
            await _cartRepo.SaveChangesAsync(token);
        }
    }

}
