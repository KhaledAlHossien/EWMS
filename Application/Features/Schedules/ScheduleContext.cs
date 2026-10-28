using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleSupport;

namespace Application.Features.Schedules
{
    /// <summary>
    /// الجدول الزمني للمشروع
    /// تبعيات المعالجات المشتركة وما تشترك فيه من تحميل وحراسة (تركيب لا وراثة): كل حالة استخدام معالج مستقل يأخذ هذا السياق.
    /// </summary>
    public sealed class ScheduleContext(IScheduleService schedules, IProjectService projects, IUserService users, IUserPermissionService permissions)
    {
        public IScheduleService Schedules { get; } = schedules;
        public IProjectService Projects { get; } = projects;
        public IUserService Users { get; } = users;
        public IUserPermissionService Permissions { get; } = permissions;

        public Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(Users, Permissions);

        public async Task<Project> LoadProjectAsync(Viewer viewer, int projectId)
        {
            var project = await Projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            return project;
        }

        public async Task<ProjectSchedule> LoadScheduleAsync(Viewer viewer, int id)
        {
            var schedule = await Schedules.GetByIdAsync(id) ?? throw new KeyNotFoundException("الجدول غير موجود");
            ProjectRules.EnsureView(viewer, schedule.Project);
            return schedule;
        }

        public async Task<ScheduleTask> LoadTaskAsync(Viewer viewer, int id)
        {
            var task = await Schedules.GetTaskAsync(id) ?? throw new KeyNotFoundException("البند غير موجود");
            ProjectRules.EnsureView(viewer, task.Schedule.Project);
            return task;
        }

        public async Task<ScheduleDetailResponseDto> DetailAsync(Viewer viewer, ProjectSchedule schedule)
        {
            var dependencies = await Schedules.GetDependenciesAsync(schedule.Id);
            var startOrder = schedule.Status == ScheduleStatus.Superseded ? null : await Schedules.GetStartOrderAsync(schedule.ProjectId);
            return ToDetailDto(schedule, dependencies, startOrder, viewer, Today);
        }
    }
}
