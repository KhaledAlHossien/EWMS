using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Queries.GetByOwner
{
    // مرفقات فرع الدراسات — جدول واحد لكل المالكين (طلب، تكليف، رد، أمر بدء، مشروع، مخطط، نسخة معمارية).
    // بلا صلاحية جديدة: العرض بصلاحية عرض المالك والرفع والحذف بصلاحية تعديله (المخطط ونسخه لقسمه فقط)، داخل فرعه.
    // النوع يُكشف من محتوى الملف (MapFileRules): صور، PDF، Office، مخططات DWG/DXF، نصوص، أرشيف — والبرامج والسكربتات ممنوعة دائماً.
    // المحتوى في مجلد المرفقات على الخادم دائماً وبلا سقف للحجم (ملفات المخططات قد تبلغ جيجابايت — قرار المستخدم 2026-11-05):
    // يُرفع تدفقاً من جسم الطلب إلى ملف مؤقت في المجلد، يُفحص ثم يُعتمد، ولا يمر بالذاكرة.
    public record GetStudyAttachmentsQuery(int OwnerType, int OwnerId) : IRequest<List<StudyAttachmentDto>>;
}
