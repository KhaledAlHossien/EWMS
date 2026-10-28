using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Projects.ProjectSupport;

namespace Application.Features.Projects
{
    /// <summary>
    /// المشاريع
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ProjectContext(IProjectService projects, IAuditLogService audit, IUserService users, IUserPermissionService permissions,
            IDepartmentService departments, ISiteService sites, INotificationService notifications)
    {
        public IProjectService Projects { get; } = projects;
        public IAuditLogService Audit { get; } = audit;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;
        public IDepartmentService Departments { get; } = departments;
        public ISiteService Sites { get; } = sites;
        public INotificationService Notifications { get; } = notifications;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        /// <summary>المشروع المحمَّل بتفاصيله بعد التحقق من أنه ضمن حدّ المستخدم</summary>
        public async Task<Project> LoadVisibleAsync(Viewer viewer, int id)
        {
            var project = await Projects.GetByIdAsync(id) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        /// <summary>القسم المختار يتبع فرع المشروع، ويعيد اسمه للأثر (null إن لم يُختر)</summary>
        public async Task<string?> ResolveDepartmentAsync(int? departmentId, int branchId)
        {
            if (departmentId is not int id) return null;
            var department = await Departments.GetByIdAsync(id) ?? throw new KeyNotFoundException("القسم غير موجود");
            if (department.BranchId != branchId)
                throw new InvalidOperationException("القسم المختار يتبع فرعاً آخر غير فرع المشروع");
            return department.Name;
        }

        public async Task<string?> ResolveSiteAsync(int? siteId)
        {
            if (siteId is not int id) return null;
            var site = await Sites.GetByIdAsync(id) ?? throw new KeyNotFoundException("الموقع غير موجود");
            return site.Name;
        }
    }
}
