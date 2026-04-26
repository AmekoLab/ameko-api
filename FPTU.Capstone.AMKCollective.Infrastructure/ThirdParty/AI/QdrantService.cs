using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using static Qdrant.Client.Grpc.Conditions;

namespace FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty.AI
{
    public class QdrantService : IQdrantService
    {
        private readonly QdrantClient _client;

        public QdrantService(IOptions<AISettings> options)
        {
            var settings = options.Value;
            if (string.IsNullOrEmpty(settings.Qdrant.Url)) throw new ArgumentNullException("Qdrant URL is not configured.");
            if (string.IsNullOrEmpty(settings.Qdrant.ApiKey)) throw new ArgumentNullException("Qdrant API Key is not configured.");

            // Qdrant Cloud usually requires port 6334 for gRPC. 
            // If the URL doesn't already have a port, we append it.
            var uriBuilder = new UriBuilder(settings.Qdrant.Url);
            if (uriBuilder.Port == -1 || uriBuilder.Port == 443 || uriBuilder.Port == 80)
            {
                uriBuilder.Port = settings.Qdrant.Port;
            }

            _client = new QdrantClient(uriBuilder.Uri, apiKey: settings.Qdrant.ApiKey);
        }

        public async Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize)
        {
            var collections = await _client.ListCollectionsAsync();
            if (!collections.Contains(collectionName))
            {
                await _client.CreateCollectionAsync(collectionName, new VectorParams
                {
                    Size = vectorSize,
                    Distance = Distance.Cosine
                });

                // Create payload index for shop_id to allow filtering
                await _client.CreatePayloadIndexAsync(collectionName, "shop_id", PayloadSchemaType.Keyword);
            }
        }

        public async Task UpsertPointAsync(string collectionName, Guid id, float[] vector, Dictionary<string, object> payload)
        {
            var point = new PointStruct
            {
                Id = id,
                Vectors = vector,
            };

            foreach (var kvp in payload)
            {
                point.Payload.Add(kvp.Key, kvp.Value.ToString());
            }

            await _client.UpsertAsync(collectionName, new[] { point });
        }

        public async Task<List<Guid>> SearchAsync(string collectionName, float[] vector, int limit = 5, Guid? shopId = null, float? scoreThreshold = null)
        {
            Filter? filter = null;
            if (shopId.HasValue)
            {
                // Qdrant.Client 1.17: Use MatchKeyword for string field matching
                filter = new Filter
                {
                    Must = { MatchKeyword("shop_id", shopId.Value.ToString()) }
                };
            }

            var results = await _client.SearchAsync(
                collectionName: collectionName,
                vector: vector,
                filter: filter,
                limit: (ulong)limit,
                scoreThreshold: scoreThreshold
            );

            // Qdrant.Client 1.17: PointId uses .Uuid (string) property
            return results
                .Where(r => r.Id != null && r.Id.HasUuid)
                .Select(r => Guid.Parse(r.Id.Uuid))
                .ToList();
        }

        public async Task DeletePointAsync(string collectionName, Guid id)
        {
            await _client.DeleteAsync(collectionName, new PointId { Uuid = id.ToString() });
        }

        public async Task DeleteCollectionAsync(string collectionName)
        {
            var collections = await _client.ListCollectionsAsync();
            if (collections.Contains(collectionName))
            {
                await _client.DeleteCollectionAsync(collectionName);
            }
        }
    }
}
