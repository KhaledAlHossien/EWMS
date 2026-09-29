using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Map
{
    /// <summary>
    /// خريطة الفرع في لوحة المتابعة (قرار المستخدم 2026-09-29):
    /// الآن للسوبر ادمن فقط ويتصفح أي فرع. لاحقاً: رئيس كل فرع يرى خريطة فرعه —
    /// يكفي عندها إضافة حالة "BranchManager" أدناه (branchId == user.BranchId).
    /// </summary>
    public record GetBranchMapQuery(int BranchId) : IRequest<BranchMapDto>;
    public record GetMapBranchesQuery : IRequest<List<MapBranchOptionDto>>;

    public class GetMapBranchesQueryHandler : IRequestHandler<GetMapBranchesQuery, List<MapBranchOptionDto>>
    {
        private readonly IMapService _mapService;
        private readonly ICurrentUserService _currentUser;

        public GetMapBranchesQueryHandler(IMapService mapService, ICurrentUserService currentUser)
        {
            _mapService = mapService;
            _currentUser = currentUser;
        }

        public async Task<List<MapBranchOptionDto>> Handle(GetMapBranchesQuery request, CancellationToken cancellationToken)
        {
            // التنقل بين الفروع للسوبر ادمن فقط
            if (_currentUser.Role != "SuperAdmin")
                throw new UnauthorizedAccessException("لا تملك صلاحية تصفح خرائط الفروع");
            return await _mapService.GetBranchOptionsAsync();
        }
    }

    public class GetBranchMapQueryHandler : IRequestHandler<GetBranchMapQuery, BranchMapDto>
    {
        private readonly IMapService _mapService;
        private readonly ICurrentUserService _currentUser;

        public GetBranchMapQueryHandler(IMapService mapService, ICurrentUserService currentUser)
        {
            _mapService = mapService;
            _currentUser = currentUser;
        }

        public async Task<BranchMapDto> Handle(GetBranchMapQuery request, CancellationToken cancellationToken)
        {
            var allowed = _currentUser.Role switch
            {
                "SuperAdmin" => true,
                _ => false
            };
            if (!allowed)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض خريطة هذا الفرع");

            return await _mapService.GetBranchMapAsync(request.BranchId)
                ?? throw new KeyNotFoundException("الفرع غير موجود");
        }
    }
}
