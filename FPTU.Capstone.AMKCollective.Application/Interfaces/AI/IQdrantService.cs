using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.AI
{
    public interface IQdrantService
    {
        /// <summary>
        /// Upserts a point (id + vector + payload) into a Qdrant collection.
        /// </summary>
        Task UpsertPointAsync(string collectionName, Guid id, float[] vector, Dictionary<string, object> payload);

        /// <summary>
        /// Searches for the most similar points in a collection.
        /// </summary>
        /// <param name="collectionName">The collection to search in.</param>
        /// <param name="vector">The query vector.</param>
        /// <param name="limit">Max results.</param>
        /// <param name="shopId">Optional filter by ShopId.</param>
        /// <returns>A list of IDs (Guids) of the matching points.</returns>
        Task<List<Guid>> SearchAsync(string collectionName, float[] vector, int limit = 5, Guid? shopId = null);

        /// <summary>
        /// Deletes a point from a collection.
        /// </summary>
        Task DeletePointAsync(string collectionName, Guid id);

        /// <summary>
        /// Ensures a collection exists with the specified vector size.
        /// </summary>
        Task EnsureCollectionExistsAsync(string collectionName, ulong vectorSize);

        /// <summary>
        /// Deletes an entire collection.
        /// </summary>
        Task DeleteCollectionAsync(string collectionName);
    }
}
