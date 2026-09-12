using Application.DTOs.Request;
using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Helper.Profiles
{
    public class DepartmentProfile : Profile
    {
        public DepartmentProfile()
        {
            // Entity → Response DTO
            CreateMap<Department, DepartmentResponseDto>()
                .ForMember(dest => dest.BranchName,
                    opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : string.Empty));

            // Request DTO → Entity
            CreateMap<CreateDepartmentRequestDto, Department>();
            CreateMap<UpdateDepartmentRequestDto, Department>();
        }
    }
}
