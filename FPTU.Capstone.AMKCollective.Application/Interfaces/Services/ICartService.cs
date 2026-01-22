using FPTU.Capstone.AMKCollective.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(Guid userId, CancellationToken token = default);
        Task AddToCartAsync(Guid userId, AddToCartDto request, CancellationToken token = default);
        Task RemoveItemAsync(Guid userId, Guid cartItemId, CancellationToken token = default);
        Task UpdateQuantityAsync(Guid userId, Guid cartItemId, int quantity, CancellationToken token = default);
    }
}
