using Application.DTOs.Response;
using Application.Features.Map;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>خريطة سوريا التفاعلية — بيانات كل فرع كطبقات (الصلاحية داخل GetBranchMapQuery)</summary>
    [ApiController]
    [Route("api/Map")]
    [Authorize]
    public class MapController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MapController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("Branches")]
        public async Task<ActionResult<List<MapBranchOptionDto>>> Branches()
            => Ok(await _mediator.Send(new GetMapBranchesQuery()));

        [HttpGet("Branch/{branchId}")]
        public async Task<ActionResult<BranchMapDto>> Branch(int branchId)
            => Ok(await _mediator.Send(new GetBranchMapQuery(branchId)));
    }
}
