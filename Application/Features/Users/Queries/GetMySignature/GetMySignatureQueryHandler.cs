using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetMySignature
{
    public class GetMySignatureQueryHandler : IRequestHandler<GetMySignatureQuery, string?>
    {
        private readonly IUserSignatureService _signatureService;
        private readonly IUserService _userService;

        public GetMySignatureQueryHandler(IUserSignatureService signatureService, IUserService userService)
        {
            _signatureService = signatureService;
            _userService = userService;
        }

        public Task<string?> Handle(GetMySignatureQuery request, CancellationToken ct) =>
            _signatureService.GetAsync(_userService.UserId);
    }
}
