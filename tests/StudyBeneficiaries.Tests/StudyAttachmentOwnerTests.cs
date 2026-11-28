using Application.Features.CentralStudies.Attachments;
using Domain.Entities.CentralStudiesBranch;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyAttachmentOwnerTests
{
    public static TheoryData<StudyEntityType> Owners => new(StudyAttachmentRules.Owners);

    // كل صاحب مرفق مدعوم يجب أن يُعرَف نوعه ورقمه، وإلا فشل تنزيل المرفق وحذفه
    [Theory]
    [MemberData(nameof(Owners))]
    public void Every_supported_owner_is_resolved(StudyEntityType type)
    {
        var a = new StudyAttachment();
        switch (type)
        {
            case StudyEntityType.Request: a.RequestId = 7; break;
            case StudyEntityType.Assignment: a.AssignmentId = 7; break;
            case StudyEntityType.Response: a.ResponseId = 7; break;
            case StudyEntityType.StartOrder: a.StartOrderId = 7; break;
            case StudyEntityType.Project: a.ProjectId = 7; break;
            case StudyEntityType.Drawing: a.DrawingId = 7; break;
            case StudyEntityType.Version: a.VersionId = 7; break;
            case StudyEntityType.DiwanTransaction: a.DiwanTransactionId = 7; break;
            default: Assert.Fail($"صاحب مرفق جديد بلا اختبار: {type}"); break;
        }

        Assert.Equal(type, StudyAttachmentSupport.OwnerTypeOf(a));
        Assert.Equal(7, StudyAttachmentSupport.OwnerIdOf(a));
    }
}
