using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class StudyLookupService : StudyRepositoryBase, IStudyLookupService
    {
        public StudyLookupService(DataContext context) : base(context) { }

        // ----- أنواع المراحل -----
        public async Task<List<StudyStageType>> GetStageTypesAsync(bool activeOnly) =>
            await _context.StudyStageTypes.Where(x => !activeOnly || x.IsActive)
                .OrderBy(x => x.DefaultOrder).ThenBy(x => x.Name).AsNoTracking().ToListAsync();

        public async Task<StudyStageType?> GetStageTypeAsync(int id) =>
            await _context.StudyStageTypes.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<bool> StageTypeNameExistsAsync(string name, int? excludeId = null) =>
            await _context.StudyStageTypes.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> StageTypeInUseAsync(int id) =>
            await _context.ProjectStages.AnyAsync(s => s.StageTypeId == id);

        // ----- أنواع المرفقات -----
        public async Task<List<StudyAttachmentType>> GetAttachmentTypesAsync(bool activeOnly) =>
            await _context.StudyAttachmentTypes.Where(x => !activeOnly || x.IsActive)
                .OrderBy(x => x.Name).AsNoTracking().ToListAsync();

        public async Task<StudyAttachmentType?> GetAttachmentTypeAsync(int id) =>
            await _context.StudyAttachmentTypes.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<bool> AttachmentTypeNameExistsAsync(string name, int? excludeId = null) =>
            await _context.StudyAttachmentTypes.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> AttachmentTypeInUseAsync(int id) =>
            await _context.StudyAttachments.AnyAsync(a => a.AttachmentTypeId == id);

        // ----- الاختصاصات -----
        public async Task<List<EngineeringDiscipline>> GetDisciplinesAsync(bool activeOnly) =>
            await _context.EngineeringDisciplines.Where(x => !activeOnly || x.IsActive)
                .OrderBy(x => x.Name).AsNoTracking().ToListAsync();

        public async Task<EngineeringDiscipline?> GetDisciplineAsync(int id) =>
            await _context.EngineeringDisciplines.FirstOrDefaultAsync(x => x.Id == id);

        public async Task<bool> DisciplineNameExistsAsync(string name, int? excludeId = null) =>
            await _context.EngineeringDisciplines.AnyAsync(x => x.Name == name && x.Id != excludeId);

        public async Task<bool> DisciplineInUseAsync(int id) =>
            await _context.ResponseStaffEstimates.AnyAsync(s => s.DisciplineId == id)
            || await _context.StartOrderEngineers.AnyAsync(e => e.DisciplineId == id);

        public async Task<int> CountActiveDisciplinesAsync(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            return await _context.EngineeringDisciplines.CountAsync(d => list.Contains(d.Id) && d.IsActive);
        }

        // ----- مشترك -----
        public async Task AddAsync<T>(T entity) where T : class
        {
            _context.Add(entity);
            await SaveChangesAsync();
        }

        public async Task RemoveAsync<T>(T entity) where T : class
        {
            _context.Remove(entity);
            await SaveChangesAsync();
        }

        public Task SaveAsync() => SaveChangesAsync();
    }

    /// <summary>
    /// عدّاد الترقيم: UPDATE بقفل السطر (UPDLOCK, HOLDLOCK) داخل معاملة، وإن لم يوجد سطر للسنة يُنشأ —
    /// فطلبان في اللحظة نفسها لا يأخذان الرقم نفسه.
    /// </summary>
    public class StudyNumberService : IStudyNumberService
    {
        private const string Sql = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            BEGIN TRAN;
            DECLARE @n int;
            UPDATE [StudyNumberSequences] WITH (UPDLOCK, HOLDLOCK)
               SET @n = [LastNumber] = [LastNumber] + 1
             WHERE [DocumentType] = @type AND [Year] = @year;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO [StudyNumberSequences] ([DocumentType], [Year], [LastNumber]) VALUES (@type, @year, 1);
                SET @n = 1;
            END
            COMMIT;
            SELECT @n AS [Value];
            """;

        private readonly DataContext _context;

        public StudyNumberService(DataContext context)
        {
            _context = context;
        }

        public async Task<int> NextAsync(StudyDocumentType type, int year)
        {
            var result = await _context.Database
                .SqlQueryRaw<int>(Sql,
                    new Microsoft.Data.SqlClient.SqlParameter("@type", (int)type),
                    new Microsoft.Data.SqlClient.SqlParameter("@year", year))
                .ToListAsync();
            return result.Single();
        }
    }
}
