namespace Application.DTOs.Response
{
    /// <summary>بطاقة مهمة على لوحة المهام</summary>
    public class AssignedTaskCardDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;   // Low | Normal | High | Urgent
        public string PriorityAr { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;     // Todo | InProgress | Done
        public string StatusAr { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public bool IsOverdue { get; set; }

        public string TargetType { get; set; } = string.Empty; // Department | Office | User
        public string TargetName { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty; // الفرع / القسم / المكتب

        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;

        public int? ParentTaskId { get; set; }
        public string? ParentTitle { get; set; }
        public int SubTasksTotal { get; set; }
        public int SubTasksDone { get; set; }
        public int CommentsCount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>هل يستطيع المستخدم الحالي سحبها بين الأعمدة (هو الجهة المسؤولة عن تنفيذها)</summary>
        public bool CanChangeStatus { get; set; }
    }

    public class AssignedTaskDetailDto : AssignedTaskCardDto
    {
        public string Description { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanComment { get; set; }
        /// <summary>تفويض جزء منها لجهة أدنى (رئيس القسم ← مكاتبه، رئيس المكتب ← موظفيه)</summary>
        public bool CanDelegate { get; set; }

        public List<AssignedTaskCardDto> SubTasks { get; set; } = [];
        public List<AssignedTaskActivityDto> Activities { get; set; } = [];
    }

    public class AssignedTaskActivityDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // Created | StatusChanged | Comment | Delegated | Edited
        public string Text { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>جهة يمكن للمستخدم الحالي إسناد مهمة إليها</summary>
    public class TaskTargetOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeadNames { get; set; } = string.Empty; // من سيتولاها (رئيس القسم/المكتب)
    }

    public class TaskTargetTypeDto
    {
        public string Value { get; set; } = string.Empty; // Department | Office | User
        public string Label { get; set; } = string.Empty;
    }

    public class TaskBoardDto
    {
        public string Mode { get; set; } = string.Empty;   // incoming | outgoing | scope
        public bool CanCreate { get; set; }
        public string TargetTypeLabel { get; set; } = string.Empty; // "قسم" / "مكتب" / "موظف"
        /// <summary>كل أنواع الإسناد المتاحة لي (حسب صلاحياتي)</summary>
        public List<TaskTargetTypeDto> TargetTypes { get; set; } = [];
        /// <summary>يظهر تبويب "كل مهام نطاقي"</summary>
        public bool HasScope { get; set; }
        public List<AssignedTaskCardDto> Tasks { get; set; } = [];
    }
}
