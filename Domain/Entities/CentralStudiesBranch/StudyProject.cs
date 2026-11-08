namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// المشروع — واحد لكل أمر بدء (StartOrderId فريد)، محور المرحلة التنفيذية. نسبة التقدم تُحسب من المراحل ولا تُخزَّن.
    /// الإنجاز النهائي يعتمده إدارة المشاريع ثم رئيس الفرع (StudyApprovals على المشروع).
    /// </summary>
    public class StudyProject
    {
        public int Id { get; set; }

        public int StartOrderId { get; set; }
        public StudyStartOrder StartOrder { get; set; } = null!;

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>
        /// موقع المشروع في سجل المواقع المشترك — منسوخ من طلب الدراسة عند إنشاء المشروع (موقع الطلب لا يتغير بعد اعتماده)
        /// ليقرأه مزوّد الخريطة بلا المرور بسلسلة الطلب ← التكليف ← الرد ← أمر البدء. قد يكون فارغاً (طلب بلا موقع).
        /// </summary>
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int ManagerUserId { get; set; }
        public User Manager { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        /// <summary>كود المشروع (منسوخ من التكليف عند الإنشاء)</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public DateTime PlannedStartDate { get; set; }
        public DateTime PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }

        public StudyProjectStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public List<ProjectDepartment> Departments { get; set; } = [];
        public List<ProjectStage> Stages { get; set; } = [];
    }

    /// <summary>قسم مشارك في المشروع — تضيفه إدارة المشاريع (القسم مرة واحدة لكل مشروع)</summary>
    public class ProjectDepartment
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public StudyProject Project { get; set; } = null!;

        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public int AddedByUserId { get; set; }
        public User AddedBy { get; set; } = null!;
        public DateTime AddedAt { get; set; }

        public List<ProjectMember> Members { get; set; } = [];
    }

    /// <summary>عضو فريق المشروع — يعيّنه رئيس القسم المشارك من موظفي قسمه</summary>
    public class ProjectMember
    {
        public int ProjectDepartmentId { get; set; }
        public ProjectDepartment ProjectDepartment { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string? RoleTitle { get; set; }

        public int AssignedByUserId { get; set; }
        public User AssignedBy { get; set; } = null!;
        public DateTime AssignedAt { get; set; }
    }

    /// <summary>
    /// مرحلة المشروع: متتالية، تغلقها إدارة المشاريع (لا تُغلق مرحلة قبل ما قبلها). نسبة تقدمها = متوسط مهامها الموزون.
    /// </summary>
    public class ProjectStage
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public StudyProject Project { get; set; } = null!;

        public int StageTypeId { get; set; }
        public StudyStageType StageType { get; set; } = null!;

        public int? ResponsibleDepartmentId { get; set; }
        public Department? ResponsibleDepartment { get; set; }

        public int? ClosedByUserId { get; set; }
        public User? ClosedBy { get; set; }

        public int SortOrder { get; set; }
        public decimal Weight { get; set; }

        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }

        public StudyStageStatus Status { get; set; }
        public DateTime? ClosedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public List<StageTask> Tasks { get; set; } = [];
    }

    /// <summary>
    /// مهمة داخل مرحلة — منفصلة عن لوحة مهام EWMS وبنفس آلية الإسناد: عمود إسناد واحد فقط مملوء حسب TargetType (قيد CHECK).
    /// </summary>
    public class StageTask
    {
        public int Id { get; set; }

        public int StageId { get; set; }
        public ProjectStage Stage { get; set; } = null!;

        public StudyTaskTarget TargetType { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? OfficeId { get; set; }
        public Office? Office { get; set; }

        public int? AssigneeUserId { get; set; }
        public User? Assignee { get; set; }

        public int AssignedByUserId { get; set; }
        public User AssignedBy { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }

        /// <summary>نسبة الإنجاز 0–100</summary>
        public decimal Progress { get; set; }
        /// <summary>وزن المهمة داخل مرحلتها</summary>
        public decimal Weight { get; set; }
        public StudyTaskStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
