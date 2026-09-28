using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class WorkTaskProfile : Profile
    {
        public WorkTaskProfile()
        {
            CreateMap<WorkTask, WorkTaskResponseDto>()
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : string.Empty))
                .ForMember(d => d.Assignees, o => o.MapFrom(s => s.Assignments));

            CreateMap<UserWorkTask, WorkTaskAssigneeDto>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.User != null && s.User.Department != null ? s.User.Department.Name : string.Empty))
                .ForMember(d => d.OfficeName, o => o.MapFrom(s => s.User != null && s.User.Office != null ? s.User.Office.Name : string.Empty));

            CreateMap<WorkTask, WorkTaskCardDto>()
                .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : string.Empty))
                .ForMember(d => d.AssigneesCount, o => o.MapFrom(s => s.Assignments.Count));
        }
    }
}
