namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUnitOfWork
    {
        void Commit();
        void Rollback();
    }
}
