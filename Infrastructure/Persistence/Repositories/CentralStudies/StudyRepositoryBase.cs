using Domain.Entities.CentralStudiesBranch;
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

        // إلغاء أمر البدء وإعادة تفعيل مشروعه يأخذان القفل نفسه قبل التحقق من حالة السجل المرتبط.
        protected Task<StudyDocumentStatus> LockStartOrderStatusAsync(int id, CancellationToken ct) =>
            _context.StudyStartOrders
                .FromSqlInterpolated($"SELECT * FROM [StudyStartOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .Select(o => o.Status)
                .SingleAsync(ct);

        // إلغاء الطلب وإعادة تفعيل مشروع الفرع الجغرافي (بلا أمر بدء) يأخذان قفل سطر الطلب
        protected Task<StudyDocumentStatus> LockRequestStatusAsync(int id, CancellationToken ct) =>
            _context.StudyRequests
                .FromSqlInterpolated($"SELECT * FROM [StudyRequests] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
                .Select(r => r.Status)
                .SingleAsync(ct);

        protected async Task SaveChangesAsync(CancellationToken ct = default)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا السجل بعد أن فتحته — أعد تحميله ثم أعد المحاولة");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                throw new InvalidOperationException(DuplicateMessage(sql.Message));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                throw new InvalidOperationException("لا يمكن الحفظ: السجل مرتبط بسجلات أخرى أو يخالف قيود البيانات");
            }
        }

        /// <summary>
        /// رسالة القيمة المكررة بحسب الفهرس الفريد الذي رفضها، مع القيمة نفسها.
        /// الفحص قبل الحفظ يغطي الحالة العادية؛ هذا لحفظين متزامنين بالقيمة نفسها.
        /// </summary>
        public static string DuplicateMessage(string sqlMessage)
        {
            var value = System.Text.RegularExpressions.Regex.Match(sqlMessage, @"duplicate key value is \((.*)\)\.?") is { Success: true } m
                ? $" «{m.Groups[1].Value}»" : string.Empty;
            if (sqlMessage.Contains("IX_StudyRequests_RequestNumber"))
                return $"رقم طلب الدراسة{value} مستخدم في طلب آخر — تحقق من الرقم الوارد من الديوان المركزي";
            if (sqlMessage.Contains("IX_StudyAssignments_ProjectCode") || sqlMessage.Contains("IX_StudyProjects_Code"))
                return $"كود المشروع{value} مستخدم في تكليف أو مشروع آخر — تحقق من رقم أرشيف ديوان الإدارة وتاريخ التكليف";
            if (sqlMessage.Contains("IX_StudyDiwanTransactions_ResponseId"))
                return "لإشعار الرد معاملة ديوان مسبقاً — معاملة واحدة لكل رد";
            if (sqlMessage.Contains("IX_StudyProjects_RequestId"))
                return "للدراسة مشروع مسبقاً — مشروع واحد لكل طلب";
            if (sqlMessage.Contains("IX_StudyBranches_BranchId_Code"))
                return "رمز فرع الدراسة مستخدم لفرع دراسة آخر في الفرع نفسه";
            if (sqlMessage.Contains("IX_StudyBranches_BranchId_Name"))
                return "اسم فرع الدراسة مستخدم لفرع دراسة آخر في الفرع نفسه";
            return "القيمة مستخدمة في سجل آخر (رقم أو كود أو اسم مكرر) — راجع البيانات وأعد المحاولة";
        }
    }
}
