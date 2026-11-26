using MediatR;

namespace Application.Features.Users.Queries.GetMySignature
{
    /// <summary>توقيعي (Data URL) أو null</summary>
    public record GetMySignatureQuery : IRequest<string?>;
}
