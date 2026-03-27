using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Hubs
{
    /// <summary>
    /// This class is used to map a key (e.g., user ID) to a set of connection IDs. It allows you to manage multiple connections for a single key, which is useful in scenarios where a user may have multiple active connections (e.g., multiple browser tabs or devices). The class provides methods to add, retrieve, and remove connections associated with a specific key.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConnectionMapping<T> where T : notnull
    {
        private static readonly Dictionary<T, HashSet<string>> Connections = new();

        public void Add(T key, string connectionId)
        {
            lock (Connections)
            {
                if (!Connections.TryGetValue(key, out var connections))
                {
                    connections = new HashSet<string>();
                    Connections.Add(key, connections);
                }

                lock (connections)
                {
                    connections.Add(connectionId);
                }
            }
        }

        public IEnumerable<string> GetConnections(T key)
        {
            lock (Connections)
            {
                if (Connections.TryGetValue(key, out var connections)) return connections;
            }

            return Enumerable.Empty<string>();
        }

        public IEnumerable<string> GetConnections(List<T> keys)
        {
            lock (Connections)
            {
                var result = new HashSet<string>();
                foreach (var key in keys)
                    if (Connections.TryGetValue(key, out var connections))
                        result.UnionWith(connections);

                return result;
            }
        }

        public void Remove(T key, string connectionId)
        {
            lock (Connections)
            {
                if (!Connections.TryGetValue(key, out var connections)) return;

                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (!connections.Any()) Connections.Remove(key);
                }
            }
        }
    }
}
