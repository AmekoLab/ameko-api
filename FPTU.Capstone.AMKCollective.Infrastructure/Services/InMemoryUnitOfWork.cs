using System;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    // Lightweight in-memory UnitOfWork placeholder for development
    public class InMemoryUnitOfWork : IUnitOfWork, IDisposable
    {
        private bool _disposed;

        public void Commit()
        {
            // no-op for in-memory
        }

        public void Rollback()
        {
            // no-op for in-memory
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // free resources if any
        }
    }
}
