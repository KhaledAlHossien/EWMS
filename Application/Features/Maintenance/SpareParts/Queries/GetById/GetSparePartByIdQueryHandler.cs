using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetById
{
    public class GetSparePartByIdQueryHandler : IRequestHandler<GetSparePartByIdQuery, SparePartResponseDto>
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly SparePartSupport _support;

        public GetSparePartByIdQueryHandler(IUserService userService, IUserPermissionService permissions, SparePartSupport support)
        {
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<SparePartResponseDto> Handle(GetSparePartByIdQuery request, CancellationToken ct) =>
            SparePartRules.ToDto(await _support.LoadInScopeAsync(request.Id, await ViewerAsync()));

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
