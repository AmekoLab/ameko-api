using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CartRepository : ICartRepository
    {
        private readonly ApplicationDbContext _context;

        public CartRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Cart?> GetCartByUserIdAsync(Guid userId, CancellationToken token = default)
        {
            return await _context.Carts
                .AsNoTracking() 
                .Include(c => c.CartItems.Where(ci => !ci.IsDeleted)) 
                    .ThenInclude(ci => ci.Product) 
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsDeleted, token);
        }
        public async Task<CartItem?> GetItemByIdAsync(Guid itemId, CancellationToken token = default)
        {
            return await _context.CartItems
        .Include(ci => ci.Cart) 
        .FirstOrDefaultAsync(ci => ci.Id == itemId, token);
        }

        public async Task<CartItem?> FindStandardItemAsync(Guid cartId, Guid productId, CancellationToken token = default)
        {
            return await _context.CartItems
                .FirstOrDefaultAsync(ci =>
                    ci.CartId == cartId &&
                    ci.ProductId == productId &&
                    !ci.IsCustom &&
                    ci.Status == CartItemStatus.Normal &&
                    !ci.IsDeleted, token);
        }


        public async Task CreateCartAsync(Cart cart, CancellationToken token = default)
        {
            await _context.Carts.AddAsync(cart, token);
        }

        public async Task<CartItem> AddItemAsync(CartItem item, CancellationToken token = default)
        {
            await _context.CartItems.AddAsync(item, token);
            return item; 
        }

        public Task UpdateItemAsync(CartItem item, CancellationToken token = default)
        {
            _context.CartItems.Update(item);
            return Task.CompletedTask;
        }

        public Task DeleteItemAsync(CartItem item, CancellationToken token = default)
        {
            _context.CartItems.Remove(item);

            return Task.CompletedTask;
        }

        // ==========================================================
        // TRANSACTION
        // ==========================================================

        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
    }
}
