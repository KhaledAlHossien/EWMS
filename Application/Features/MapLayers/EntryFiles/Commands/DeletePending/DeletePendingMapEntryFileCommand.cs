using MediatR;

namespace Application.Features.MapLayers.EntryFiles.Commands.DeletePending
{
    /// <summary>حذف ملف معلّق رفعه المستخدم ولم يحفظه (إلغاء النموذج أو إزالته قبل الحفظ)</summary>
    public record DeletePendingMapEntryFileCommand(int Id) : IRequest<Unit>;
}
