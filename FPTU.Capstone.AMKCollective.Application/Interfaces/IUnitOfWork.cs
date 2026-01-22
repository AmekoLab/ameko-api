using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        Task CommitAsync();
        void Rollback();
    }
}
