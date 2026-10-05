using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Application.Features.Vacations;
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
                .ForMember(d => d.FirstApprovedByName,
                    o => o.MapFrom(s => s.FirstApprovedByUser != null
                        ? s.FirstApprovedByUser.FullName : null))
                .ForMember(d => d.FinalApprovedByName,
                    o => o.MapFrom(s => s.FinalApprovedByUser != null
                        ? s.FinalApprovedByUser.FullName : null))
                .ForMember(d => d.RequestNumber,
                    o => o.MapFrom(s => VacationRules.RequestNumber(s.Id, s.CreatedAt)))
                .ForMember(d => d.PaymentDecided,
                    o => o.MapFrom(s => s.Status == VacationStatus.Approved))
                .ForMember(d => d.PaymentStatusAr,
                    o => o.MapFrom(s => VacationRules.PaymentAr(s.Status, s.PaidDays, s.UnpaidDays)))
                .ForMember(d => d.CalendarDays,
                    o => o.MapFrom(s => (s.EndVac.Date - s.StartVac.Date).Days + 1))
                .ForMember(d => d.Segments,
                    o => o.MapFrom(s => s.Segments.OrderBy(x => x.StartDate)));

            CreateMap<VacationSegment, VacationSegmentDto>();
            CreateMap<VacationAttachment, VacationAttachmentDto>();
        }

        private static string TranslateStatus(VacationStatus status)
            => VacationRules.StatusAr(status);

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