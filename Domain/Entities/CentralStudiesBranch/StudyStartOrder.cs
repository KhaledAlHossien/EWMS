namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// أمر بدء دراسة مشروع (PIO) — واحد لكل رد (ResponseId فريد)، ويرتبط بالرد فقط؛ كود المشروع يُعرض من التكليف.
    /// يوقّعه رئيس القسم المختص ثم رئيس الفرع. المهندسون المخصصون مستخدمون محددون من النظام (فريق الدراسة).
    /// </summary>
    public class StudyStartOrder : IStudyDocument
    {
        public int Id { get; set; }

        public int ResponseId { get; set; }
        public StudyAssignmentResponse Response { get; set; } = null!;

        public int CompetentDepartmentId { get; set; }
        public Department CompetentDepartment { get; set; } = null!;

        public int ProjectManagerUserId { get; set; }
        public User ProjectManager { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string OrderNumber { get; set; } = string.Empty;   // PIO-26-0001
        public DateTime OrderDate { get; set; }
        public DateTime StudyStartDate { get; set; }
        public DateTime StudyDeadline { get; set; }

        public StudyDocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public List<StartOrderEngineer> Engineers { get; set; } = [];
        public List<StartOrderResource> Resources { get; set; } = [];

        public StudyProject? Project { get; set; }
    }

    /// <summary>مهندس مخصص للدراسة (المهندس مرة واحدة لكل أمر)</summary>
    public class StartOrderEngineer
    {
        public int StartOrderId { get; set; }
        public int EngineerUserId { get; set; }
        public User Engineer { get; set; } = null!;
        public int DisciplineId { get; set; }
        public EngineeringDiscipline Discipline { get; set; } = null!;
    }

    /// <summary>مورد مخصص للدراسة</summary>
    public class StartOrderResource
    {
        public int Id { get; set; }
        public int StartOrderId { get; set; }
        public string Item { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }
}
