using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class ProjectProfile : Profile
    {
        public ProjectProfile()
        {
            CreateMap<CreateProjectRequestDto, Project>();
            CreateMap<UpdateProjectRequestDto, Project>();

            CreateMap<Project, ProjectResponseDto>()
                .ForMember(dest => dest.CreatedByName,
                    opt => opt.MapFrom(src => src.User != null ? src.User.FullName : string.Empty))
                .ForMember(dest => dest.CurrentDepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : string.Empty))
                .ForMember(dest => dest.Assignments, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.Ignore())
                .ForMember(dest => dest.Transfers, opt => opt.Ignore());

            CreateMap<ProjectAssignments, ProjectAssignmentResponseDto>()
                .ForMember(dest => dest.AssignedUserName,
                    opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.FullName : string.Empty))
                .ForMember(dest => dest.AssignedByUserName,
                    opt => opt.MapFrom(src => src.AssignedByUser != null ? src.AssignedByUser.FullName : string.Empty));

            CreateMap<ProjectFile, ProjectFileResponseDto>()
                .ForMember(dest => dest.UploadedByName,
                    opt => opt.MapFrom(src => src.User != null ? src.User.FullName : string.Empty));

            CreateMap<ProjectTransfers, ProjectTransferResponseDto>()
                .ForMember(dest => dest.FromDepartmentName,
                    opt => opt.MapFrom(src => src.FromDepartment != null ? src.FromDepartment.Name : string.Empty))
                .ForMember(dest => dest.ToDepartmentName,
                    opt => opt.MapFrom(src => src.ToDepartment != null ? src.ToDepartment.Name : string.Empty))
                .ForMember(dest => dest.TransferredByName,
                    opt => opt.MapFrom(src => src.TransferredByUser != null ? src.TransferredByUser.FullName : string.Empty));
        }
    }
}
