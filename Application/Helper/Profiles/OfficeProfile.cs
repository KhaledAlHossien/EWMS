using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class OfficeProfile : Profile
    {
        public OfficeProfile()
        {
            // Entity → Response DTO
            CreateMap<Office, OfficeResponseDto>()
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : string.Empty))
                .ForMember(dest => dest.BranchId,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.BranchId : 0))
                .ForMember(dest => dest.BranchName,
                    opt => opt.MapFrom(src => src.Department != null && src.Department.Branch != null
                        ? src.Department.Branch.Name
                        : string.Empty));

            // Request DTO → Entity
            CreateMap<CreateOfficeRequestDto, Office>();
            CreateMap<UpdateOfficeRequestDto, Office>();
        }
    }
}
