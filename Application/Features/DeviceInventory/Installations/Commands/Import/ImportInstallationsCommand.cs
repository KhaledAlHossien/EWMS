using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Commands.Import
{
    /// <summary>DepartmentId اختياري: القسم الذي تُسجَّل باسمه الأسطر (لمن يملك نظرة المؤسسة أو ViewBranchDevices)</summary>
    public record ImportInstallationsCommand(Stream File, bool DryRun, int? DepartmentId = null) : IRequest<InstallationImportReportDto>;
}
