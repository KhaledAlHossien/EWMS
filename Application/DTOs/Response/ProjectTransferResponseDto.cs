using Domain.Enums;

namespace Application.DTOs.Response
{
    public class ProjectTransferResponseDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int FromDepartmentId { get; set; }
        public string FromDepartmentName { get; set; } = string.Empty;
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public int TransferredById { get; set; }
        public string TransferredByName { get; set; } = string.Empty;
        public TransferType TransferType { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime TransferredAt { get; set; }
        public bool IsActive { get; set; }
    }
}
