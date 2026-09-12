using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<User, UserResponseDto>()
                .ForMember(dest => dest.RoleName,
                    opt => opt.MapFrom(src => src.Role != null ? src.Role.Name : string.Empty))
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : string.Empty))
                .ForMember(dest => dest.BranchName,
                    opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : string.Empty));
        }
    }
}
