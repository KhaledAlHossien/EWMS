namespace Application.Interfaces
{
    public interface ICurrentUserService
    {
        int UserId { get; }
        int DepartmentId { get; }
        int BranchId { get; }
        string Role { get; }
        bool IsAuthenticated { get; }
    }
}
