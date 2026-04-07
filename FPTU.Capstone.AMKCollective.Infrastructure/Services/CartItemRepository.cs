using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CartItemRepository : ICartItemRepository
    {
        private readonly ApplicationDbContext _context;

        public CartItemRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CartItem?> GetByIdAsync(Guid id)
        {
            return await _context.CartItems.FirstOrDefaultAsync(ci => ci.Id == id);
        }

        public async Task AddAsync(CartItem cartItem)
        {
            await _context.CartItems.AddAsync(cartItem);
        }

        public void Update(CartItem cartItem)
        {
            _context.CartItems.Update(cartItem);
        }

        public void Remove(CartItem cartItem)
        {
            _context.CartItems.Remove(cartItem);
        }

        public void RemoveRange(IEnumerable<CartItem> cartItems)
        {
            _context.CartItems.RemoveRange(cartItems);
        }

        /// <summary>
        /// Tìm CartItem có commission config chứa QuoteId trong DesignConfig.
        /// Format JSON do AcceptQuoteAsync kiểm soát nên việc LIKE search là an toàn.
        /// Return null = user đã checkout (cart item bị xóa sau khi thanh toán).
        /// </summary>
        public async Task<CartItem?> FindByQuoteIdAsync(Guid quoteId)
        {
            var quoteIdStr = quoteId.ToString();
            return await _context.CartItems
                .FirstOrDefaultAsync(ci =>
                    ci.IsCustom
                    && ci.DesignConfig != null
                    && EF.Functions.Like(ci.DesignConfig, $"%{quoteIdStr}%"));
        }
    }
}