using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>عدّاد ترقيم المستندات: الرقم التالي لنوع وسنة، داخل معاملة بقفل السطر فلا يتكرر رقم</summary>
    public interface IStudyNumberService
    {
        Task<int> NextAsync(StudyDocumentType type, int year);
    }
}
