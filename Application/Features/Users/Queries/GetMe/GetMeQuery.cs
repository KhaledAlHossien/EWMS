using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Users.Queries.GetMe
{
    // بيانات المستخدم الحالي (من التوكن) — متاحة لأي مستخدم مسجّل دخوله
    public record GetMeQuery : IRequest<UserResponseDto>;
}
