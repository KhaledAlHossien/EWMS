using Application.DTOs.Request;
using Application.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    /// <summary>نتيجة الدخول: Response عند النجاح، أو LockedUntil إن كان الحساب مقفلاً مؤقتاً، وإلا بيانات خاطئة</summary>
    public sealed record LoginOutcome(LoginResponse? Response, DateTime? LockedUntil)
    {
        public static readonly LoginOutcome Invalid = new(null, null);
    }

    public interface IAuthService
    {
        Task<LoginOutcome> LoginAsync(LoginRequest request);
        Task<bool> RegisterAsync(RegisterRequest request);
        Task<bool> LogoutAsync(string token);
    }
}
