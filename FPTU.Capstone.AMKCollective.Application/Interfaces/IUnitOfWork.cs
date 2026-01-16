namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        void Commit();
        void Rollback();
    }
}
