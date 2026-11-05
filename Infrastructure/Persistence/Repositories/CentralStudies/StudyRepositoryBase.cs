using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// أساس مستودعات فرع الدراسات: الحفظ مع تحويل أخطاء قاعدة البيانات المتوقعة إلى رسائل عربية واضحة
    /// (تعديل متزامن، قيمة فريدة مكررة من إضافتين في اللحظة نفسها، قيد علاقة أو CHECK).
    /// </summary>
    public abstract class StudyRepositoryBase
    {
        protected readonly DataContext _context;

        protected StudyRepositoryBase(DataContext context)
        {
            _context = context;
        }

        /// <summary>يجعل نسخة السجل التي رآها المستخدم هي المرجع، فيفشل الحفظ إن عدّله غيره بعدها</summary>
        protected void ExpectVersion<T>(T entity, byte[]? expectedRowVersion) where T : class
        {
            if (expectedRowVersion is { Length: > 0 })
                _context.Entry(entity).Property("RowVersion").OriginalValue = expectedRowVersion;
        }

        protected async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا السجل بعد أن فتحته — أعد تحميله ثم أعد المحاولة");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new InvalidOperationException("القيمة مستخدمة في سجل آخر (رقم أو كود أو اسم مكرر) — راجع البيانات وأعد المحاولة");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                throw new InvalidOperationException("لا يمكن الحفظ: السجل مرتبط بسجلات أخرى أو يخالف قيود البيانات");
            }
        }
    }
}
