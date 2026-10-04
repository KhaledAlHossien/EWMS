namespace Application.Interfaces
{
    public interface IUserSignatureService
    {
        /// <summary>صورة التوقيع (Data URL) أو null إن لم يرفع المستخدم توقيعاً</summary>
        Task<string?> GetAsync(int userId);

        /// <summary>حفظ التوقيع، أو حذفه إن كانت الصورة null</summary>
        Task SetAsync(int userId, string? image);
    }
}
