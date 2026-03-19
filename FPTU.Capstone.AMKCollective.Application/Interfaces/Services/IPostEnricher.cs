using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IPostEnricher
    {
        Task EnrichAsync(IEnumerable<PostFeedResponse> posts, CancellationToken cancellationToken = default);
    }
}
