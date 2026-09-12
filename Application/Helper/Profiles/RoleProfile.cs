using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class RoleProfile : Profile
    {
        public RoleProfile()
        {
            CreateMap<Role, RoleResponseDto>()
                .ForMember(dest => dest.Permissions, opt => opt.Ignore());

            CreateMap<Permission, PermissionResponseDto>();
        }
    }
}
