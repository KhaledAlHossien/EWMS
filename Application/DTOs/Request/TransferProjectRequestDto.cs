using Domain.Enums;

namespace Application.DTOs.Request
{
    public class TransferProjectRequestDto
    {
        public int ToDepartmentId { get; set; }
        public TransferType TransferType { get; set; } = TransferType.Forward;
        public string Notes { get; set; } = string.Empty;
    }
}
