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
    }
}
