using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;

namespace Application.Helper.Profiles
{
    public class BranchProfile : Profile
    {
        public BranchProfile()
        {
            CreateMap<CreateBranchRequestDto, Branch>();
            CreateMap<UpdateBranchRequestDto, Branch>();
            CreateMap<Branch, BranchResponseDto>();
        }
    }
}
