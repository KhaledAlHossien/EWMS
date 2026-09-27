using Application.DTOs.Response;
using Application.Features.VacationTypes.Commands.Create;
using Application.Features.VacationTypes.Commands.Update;
using AutoMapper;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Helper.Profiles
{
    public class VacationTypeProfile : Profile
    {
        public VacationTypeProfile()
        {
            CreateMap<VacationType, VacationTypeResponseDto>();
            CreateMap<CreateVacationTypeCommand, VacationType>();
            CreateMap<UpdateVacationTypeCommand, VacationType>();
        }
    }
}
