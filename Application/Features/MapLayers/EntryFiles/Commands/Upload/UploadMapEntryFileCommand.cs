using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Commands.Upload
{
    /// <summary>رفع ملف لحقل «ملفات» — يبقى معلّقاً حتى يُحفظ السجل بمعرّفه في قيمة الحقل</summary>
    public record UploadMapEntryFileCommand(int FieldId, UploadedFileDto File) : IRequest<MapEntryFileDto>;
}
