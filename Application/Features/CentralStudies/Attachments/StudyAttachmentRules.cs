using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Attachments
{
    public static class StudyAttachmentRules
    {
        /// <summary>لا سقف لحجم الملف (المساحة الحرة في المجلد هي الحد)؛ العدد لكل سجل محدود كي تبقى القوائم معقولة</summary>
        public const int MaxFilesPerOwner = 100;
        public static readonly string[] AllowedKinds = ["images", "pdf", "office", "cad", "text", "archives"];

        public static readonly StudyEntityType[] Owners =
        [
            StudyEntityType.Request, StudyEntityType.Assignment, StudyEntityType.Response, StudyEntityType.StartOrder,
            StudyEntityType.Project, StudyEntityType.Drawing, StudyEntityType.Version
        ];
    }
}
