using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetCartByUserIdAsync(Guid userId, CancellationToken token = default);
        Task<CartItem?> GetItemByIdAsync(Guid itemId, CancellationToken token = default);
        Task<CartItem?> FindStandardItemAsync(Guid cartId, Guid productId, CancellationToken token = default);

        Task CreateCartAsync(Cart cart, CancellationToken token = default); 
        Task<CartItem> AddItemAsync(CartItem item, CancellationToken token = default);
        Task UpdateItemAsync(CartItem item, CancellationToken token = default);
        Task DeleteItemAsync(CartItem item, CancellationToken token = default);

        // TRANSACTION CONTROL
        Task<int> SaveChangesAsync(CancellationToken token = default);
    }
}
