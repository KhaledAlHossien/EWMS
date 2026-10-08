using Application.DTOs.Response;

namespace Application.Interfaces
{
    /// <summary>ملف Excel لمهام لوحة المهام (ClosedXML في Infrastructure)</summary>
    public interface IAssignedTaskSpreadsheet
    {
        byte[] Export(IEnumerable<AssignedTaskCardDto> tasks);
    }
}
