using Infrastructure.Persistence.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// حفظ توثيق الأجهزة برسائل واضحة بدل خطأ 500: تعديل متزامن (RowVersion) أو تكرار فريد متزامن (فهرس فريد).
    /// </summary>
    internal static class DeviceInventorySave
    {
        public static async Task SaveAsync(DataContext context, string duplicateMessage)
        {
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("عدّل مستخدم آخر هذا السجل (أو حذفه) منذ فتحته — أعد التحميل ثم كرّر التعديل");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new InvalidOperationException(duplicateMessage);
            }
        }
    }
}
