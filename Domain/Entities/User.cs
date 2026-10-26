using System;
using System.Collections.Generic;
using System.Text;
using Domain.Enums;

namespace Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        // الرقم الذاتي للموظف (قد يحوي حروفاً مثل M201160) — فريد، واختياري للحسابات القديمة
        public string? PersonalIdNumber { get; set; }
        public required string FullName { get; set; }

        /// <summary>نوع الشخص: حساب نظام، أو موظف بلا دخول، أو عامل مياومة — UserKind</summary>
        public UserKind Kind { get; set; } = UserKind.Account;

        // البريد وكلمة المرور والدور لحساب النظام وحده (UserKind.Account)، وفارغة لمن لا يدخل النظام
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }

        // رقم التواصل (نص كي لا يضيع الصفر الأول): هاتف سوري، اختياري — PhoneRules
        public string? PhoneNumber { get; set; }

        public int? RoleId { get; set; }
        public Role? Role { get; set; }

        // مكان الموظف في الهيكل (كلها اختيارية): المكتب يحدد القسم، والقسم يحدد الفرع — UserRules.EnsureUserReferencesAsync
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? OfficeId { get; set; }
        public Office? Office { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public bool IsActive { get; set; }=true;

        /// <summary>محاولات الدخول الخاطئة المتتالية (تُصفَّر بالدخول الصحيح أو عند القفل) — LoginRules</summary>
        public int FailedLoginCount { get; set; }
        /// <summary>الدخول موقوف حتى هذا الوقت (UTC) بعد محاولات خاطئة متكررة؛ null = غير مقفل</summary>
        public DateTime? LockoutUntil { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>يدخل النظام (حساب نظام): وحده يظهر في قوائم الإسناد والاختيار</summary>
        public bool HasLogin => Kind == UserKind.Account;


    }
}

