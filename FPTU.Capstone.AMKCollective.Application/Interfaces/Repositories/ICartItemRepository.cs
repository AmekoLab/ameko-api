using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICartItemRepository
    {
        Task<CartItem?> GetByIdAsync(Guid id);
        Task AddAsync(CartItem cartItem);
        void Update(CartItem cartItem);
        void Remove(CartItem cartItem);
        void RemoveRange(IEnumerable<CartItem> cartItems);
        /// <summary>
        /// Tìm CartItem có chứa QuoteId trong DesignConfig JSON.
        /// Dùng để kiểm tra user đã checkout commission chưa (null = đã thanh toán).
        /// </summary>
        Task<CartItem?> FindByQuoteIdAsync(Guid quoteId);
    }
}
