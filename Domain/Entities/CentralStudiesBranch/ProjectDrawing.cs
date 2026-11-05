namespace Domain.Entities.CentralStudiesBranch
{
    /// <summary>
    /// المخطط (أب مجرد) — كل نوع صنف مستقل في الكود، وكلها في جدول واحد بعمود تمييز DrawingKind (TPH).
    /// إن احتاج نوع أعمدة خاصة به يُضاف له جدول امتداد 1:1. النوع ثابت بعد الإنشاء.
    /// غير المعماري «مبني على» نسخة معمارية معتمدة (BasedOnVersionId).
    /// </summary>
    public abstract class ProjectDrawing
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public StudyProject Project { get; set; } = null!;

        /// <summary>القسم المُصدِر (قسم من أعدّه)</summary>
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public int? OfficeId { get; set; }
        public Office? Office { get; set; }

        public int PreparedByUserId { get; set; }
        public User PreparedBy { get; set; } = null!;

        public int? BasedOnVersionId { get; set; }
        public ArchitecturalVersion? BasedOnVersion { get; set; }

        public DrawingKind Kind { get; protected set; }
        public string Name { get; set; } = string.Empty;
        public string DrawingNumber { get; set; } = string.Empty;
        public DateTime DrawingDate { get; set; }
        public string? Description { get; set; }
        public DrawingStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        /// <summary>صنف المخطط المناسب لنوعه</summary>
        public static ProjectDrawing Create(DrawingKind kind) => kind switch
        {
            DrawingKind.Architectural => new ArchitecturalDrawing(),
            DrawingKind.Structural => new StructuralDrawing(),
            DrawingKind.Mep => new MepDrawing(),
            DrawingKind.Interior => new InteriorDrawing(),
            DrawingKind.Technical => new TechnicalDrawing(),
            _ => throw new ArgumentException("نوع مخطط غير معروف", nameof(kind))
        };
    }

    /// <summary>المخطط المعماري: يصدر أولاً، وله نسخ (Draft-N) يعتمد أولاها CDA ويُبنى عليها الباقي</summary>
    public class ArchitecturalDrawing : ProjectDrawing
    {
        public ArchitecturalDrawing() => Kind = DrawingKind.Architectural;
        public List<ArchitecturalVersion> Versions { get; set; } = [];
    }

    public class StructuralDrawing : ProjectDrawing
    {
        public StructuralDrawing() => Kind = DrawingKind.Structural;
    }

    /// <summary>كهرباء وميكانيك: قسم واحد يصدر مخططاً واحداً</summary>
    public class MepDrawing : ProjectDrawing
    {
        public MepDrawing() => Kind = DrawingKind.Mep;
    }

    public class InteriorDrawing : ProjectDrawing
    {
        public InteriorDrawing() => Kind = DrawingKind.Interior;
    }

    /// <summary>المخطط التقني (قسم مستقبلي)</summary>
    public class TechnicalDrawing : ProjectDrawing
    {
        public TechnicalDrawing() => Kind = DrawingKind.Technical;
    }

    /// <summary>نسخة المخطط المعماري (Draft-N) — للمعماري فقط؛ رقمها متسلسل داخل المخطط</summary>
    public class ArchitecturalVersion
    {
        public int Id { get; set; }

        public int DrawingId { get; set; }
        public ArchitecturalDrawing Drawing { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public int VersionNumber { get; set; }
        public DateTime VersionDate { get; set; }
        public string? Content { get; set; }
        public DrawingStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public DesignApproval? DesignApproval { get; set; }
    }

    /// <summary>
    /// اعتماد المخططات الأولي (CDA) — واحد لكل نسخة معمارية. يوقّعه القسم المرسل ثم إدارة التصميم ثم ضبط الجودة،
    /// والقرار النهائي يُشتق من تواقيعهم ويُنقل إلى النسخة والمخطط.
    /// </summary>
    public class DesignApproval
    {
        public int Id { get; set; }

        public int VersionId { get; set; }
        public ArchitecturalVersion Version { get; set; } = null!;

        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = null!;

        public string CdaNumber { get; set; } = string.Empty;   // CDA-26-0001
        public DateTime CdaDate { get; set; }
        public string? Content { get; set; }
        public string? TechnicalNotes { get; set; }
        public StudyDecision Decision { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }
}
