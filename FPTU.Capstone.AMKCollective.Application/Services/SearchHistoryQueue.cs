using FPTU.Capstone.AMKCollective.Application.DTOs.Search;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class SearchHistoryQueue : ISearchHistoryQueue
    {
        private readonly Channel<SearchLogEvent> _channel;

        public SearchHistoryQueue(int capacity = 5000)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            };
            _channel = Channel.CreateBounded<SearchLogEvent>(options);
        }

        public bool TryQueueSearch(SearchLogEvent evt) => _channel.Writer.TryWrite(evt);

        public ValueTask<SearchLogEvent> DequeueAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);

        public bool TryDequeue(out SearchLogEvent evt) => _channel.Reader.TryRead(out evt);
    }
}
