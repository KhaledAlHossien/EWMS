using Application.DTOs.Response;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Enums.Queries.GetEnums
{
    public class GetStudyEnumsQueryHandler : IRequestHandler<GetStudyEnumsQuery, Dictionary<string, List<StudyEnumValueDto>>>
    {
        public Task<Dictionary<string, List<StudyEnumValueDto>>> Handle(GetStudyEnumsQuery request, CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<StudyEnumValueDto>>
            {
                ["documentStatus"] = Values<StudyDocumentStatus>(StudyRules.Name),
                ["urgency"] = Values<StudyUrgency>(StudyRules.Name),
                ["workType"] = Values<StudyWorkType>(StudyRules.Name),
                ["priority"] = Values<StudyPriority>(StudyRules.Name),
                ["recommendation"] = Values<StudyRecommendation>(StudyRules.Name),
                ["buildingType"] = Values<StudyBuildingType>(StudyRules.Name),
                ["projectStatus"] = Values<StudyProjectStatus>(StudyRules.Name),
                ["stageStatus"] = Values<StudyStageStatus>(StudyRules.Name),
                ["taskStatus"] = Values<StudyTaskStatus>(StudyRules.Name),
                ["taskTarget"] = Values<StudyTaskTarget>(StudyRules.Name),
                ["drawingKind"] = Values<DrawingKind>(StudyRules.Name),
                ["drawingStatus"] = Values<DrawingStatus>(StudyRules.Name),
                ["decision"] = Values<StudyDecision>(StudyRules.Name),
                ["signerRole"] = Values<StudySignerRole>(StudyRules.Name),
                ["linkedDocument"] = Values<StudyLinkedDocument>(StudyRules.Name),
                ["entityType"] = Values<StudyEntityType>(StudyRules.Name),
                ["logAction"] = Values<StudyLogAction>(StudyRules.Name),
            });

        // ----- التعدادات -----
        private static List<StudyEnumValueDto> Values<T>(Func<T, string> name) where T : struct, Enum =>
            Enum.GetValues<T>().Select(v => new StudyEnumValueDto(Convert.ToInt32(v), name(v))).ToList();
    }
}
