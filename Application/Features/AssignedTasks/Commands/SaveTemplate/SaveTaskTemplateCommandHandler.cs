using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SaveTemplate
{
    public class SaveTaskTemplateCommandHandler : IRequestHandler<SaveTaskTemplateCommand, TaskTemplateDto>
    {
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskTemplatesSupport _support;

        public SaveTaskTemplateCommandHandler(IAssignedTaskPlanningService planning, TaskTemplatesSupport support)
        {
            _planning = planning;
            _support = support;
        }

        public async Task<TaskTemplateDto> Handle(SaveTaskTemplateCommand request, CancellationToken ct)
        {
            var viewer = await _support.AssignerAsync();
            var dto = request.Dto;
            var name = dto.Name.Trim();
            var items = dto.Items.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();

            if (await _planning.TemplateNameExistsAsync(viewer.Id, name, request.Id))
                throw new InvalidOperationException("لديك قالب بهذا الاسم");

            var now = DateTime.UtcNow;
            AssignedTaskTemplate template;
            if (request.Id is int id)
            {
                template = await _planning.GetTemplateAsync(id) ?? throw new KeyNotFoundException("القالب غير موجود");
                if (template.OwnerUserId != viewer.Id) throw new UnauthorizedAccessException("القالب لصاحبه فقط");
            }
            else
            {
                if ((await _planning.GetTemplatesAsync(viewer.Id)).Count >= TaskPlanningRules.MaxTemplates)
                    throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxTemplates} قالباً");
                template = new AssignedTaskTemplate { OwnerUserId = viewer.Id, CreatedAt = now };
            }

            template.Name = name;
            template.Title = dto.Title.Trim();
            template.Description = dto.Description.Trim();
            template.Priority = (AssignedTaskPriority)dto.Priority;
            template.DefaultDueDays = dto.DefaultDueDays;
            template.UpdatedAt = now;

            if (request.Id == null)
            {
                var order = 0;
                foreach (var text in items) template.Items.Add(new AssignedTaskTemplateItem { Text = text, SortOrder = order++ });
                await _planning.AddTemplateAsync(template);
            }
            else
            {
                await _planning.ReplaceTemplateItemsAsync(template, items);
            }
            return TaskPlanningRules.ToDto(template);
        }
    }
}
