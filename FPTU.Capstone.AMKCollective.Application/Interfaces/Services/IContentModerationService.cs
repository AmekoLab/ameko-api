using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IContentModerationService
    {
        Task<ModerationResult> ValidateContentAsync(string content, CancellationToken cancellationToken = default);
        Task<string> ProcessContentAsync(Guid userId, string content, string actionType, int limitPerMinute, CancellationToken ct = default);
        Task<string> MaskBadWordsAsync(string content, CancellationToken ct = default);
    }
}
