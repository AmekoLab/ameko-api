using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IBuilderSessionRepository
    {
        // Lấy session theo Guid
        Task<BuilderSession?> GetSessionByIdAsync(Guid sessionId);

        // Tạo mới session
        Task CreateSessionAsync(BuilderSession session);

        // Cập nhật session (lưu lựa chọn mới, cập nhật giá tiền)
        Task UpdateSessionAsync(BuilderSession session);

        // Xóa session cũ
        Task DeleteSessionAsync(Guid sessionId);

        Task<BuilderSession?> GetActiveSessionByUserIdAsync(Guid userId, Guid baseKitId);
    }
}
