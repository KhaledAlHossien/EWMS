using Domain.Entities.CentralStudiesBranch;

namespace Application.Interfaces
{
    /// <summary>المخططات (جدول واحد TPH) ونسخ المعماري واعتماد المخططات الأولي (CDA)</summary>
    public interface IStudyDrawingService
    {
        /// <summary>مع المشروع والقسم والمكتب والمعدّ والنسخة الأساس، ونسخ المعماري واعتماداتها</summary>
        Task<ProjectDrawing?> GetDrawingAsync(int id);
        Task<List<ProjectDrawing>> GetByProjectAsync(int projectId, DrawingKind? kind);
        Task<bool> DrawingNumberExistsAsync(int projectId, string number, int? excludeId = null);
        Task<bool> HasVersionsAsync(int drawingId);
        Task AddDrawingAsync(ProjectDrawing drawing);
        Task UpdateDrawingAsync(ProjectDrawing drawing, byte[]? expectedRowVersion = null);
        Task DeleteDrawingAsync(ProjectDrawing drawing);

        /// <summary>مع المخطط ومشروعه ومنشئها واعتمادها</summary>
        Task<ArchitecturalVersion?> GetVersionAsync(int id);
        Task<int> NextVersionNumberAsync(int drawingId);
        /// <summary>مخططات أخرى مبنية على هذه النسخة</summary>
        Task<bool> VersionUsedAsBasisAsync(int versionId);
        Task AddVersionAsync(ArchitecturalVersion version);
        Task UpdateVersionAsync(ArchitecturalVersion version, byte[]? expectedRowVersion = null);
        Task DeleteVersionAsync(ArchitecturalVersion version);

        /// <summary>مع النسخة ومخططها ومشروعه ومنشئه</summary>
        Task<DesignApproval?> GetDesignApprovalAsync(int id);
        Task AddDesignApprovalAsync(DesignApproval approval);
        Task UpdateDesignApprovalAsync(DesignApproval approval, byte[]? expectedRowVersion = null);
        Task DeleteDesignApprovalAsync(DesignApproval approval);

        Task SaveAsync();
    }
}
