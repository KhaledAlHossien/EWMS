using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Helper.Profiles
{
    public class VacationProfile : Profile
    {
        public VacationProfile()
        {
            CreateMap<Vacation, VacationResponseDto>()
                .ForMember(d => d.VacationTypeName,
                    o => o.MapFrom(s => s.VacationType != null
                        ? s.VacationType.Name : string.Empty))
                .ForMember(d => d.UserName,
                    o => o.MapFrom(s => s.User != null
                        ? s.User.FullName : string.Empty))
                .ForMember(d => d.DepartmentName,
                    o => o.MapFrom(s => s.Department != null
                        ? s.Department.Name : string.Empty))
                .ForMember(d => d.BranchName,
                    o => o.MapFrom(s => s.Branch != null
                        ? s.Branch.Name : string.Empty))
                .ForMember(d => d.Status,
                    o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.StatusAr,
                    o => o.MapFrom(s => TranslateStatus(s.Status)))
                .ForMember(d => d.CurrentStage,
                    o => o.MapFrom(s => GetCurrentStage(s.Status)))
                .ForMember(d => d.RejectedByName,
                    o => o.MapFrom(s => s.RejectedByUser != null
                        ? s.RejectedByUser.FullName : null))
                .ForMember(d => d.IsPaid, o => o.MapFrom(s => s.IsPaid));
        }

        private static string TranslateStatus(VacationStatus status) => status switch
        {
            VacationStatus.PendingManager => "بانتظار رئيس القسم",
            VacationStatus.PendingBranchManager => "بانتظار رئيس الفرع",
            VacationStatus.Approved => "معتمدة",
            VacationStatus.Rejected => "مرفوضة",
            VacationStatus.Cancelled => "ملغاة",
            _ => "غير معروفة"
        };

        private static string GetCurrentStage(VacationStatus status) => status switch
        {
            VacationStatus.PendingManager => "Manager",
            VacationStatus.PendingBranchManager => "BranchManager",
            VacationStatus.Approved => "Done",
            VacationStatus.Rejected => "Rejected",
            VacationStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }
}