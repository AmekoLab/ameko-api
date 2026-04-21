using FPTU.Capstone.AMKCollective.Application.DTOs.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ISearchHistoryQueue
    {
        bool TryQueueSearch(SearchLogEvent evt);
        ValueTask<SearchLogEvent> DequeueAsync(CancellationToken cancellationToken);
        bool TryDequeue(out SearchLogEvent evt);
    }
}
