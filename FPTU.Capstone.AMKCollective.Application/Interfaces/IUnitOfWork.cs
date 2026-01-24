using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        ICategoryRepository Categories { get; }
        IModelRepository Models { get; }
        IKitDesignOptionRepository KitDesignOptions { get; }
        IBuilderSessionRepository BuilderSessions { get; }
        IShopRepository Shops { get; }
        Task CommitAsync();
        void Rollback();
    }
}
