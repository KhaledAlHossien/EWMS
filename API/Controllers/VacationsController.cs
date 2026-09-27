using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Vacations.Commands.Approve;
using Application.Features.Vacations.Commands.Cancel;
using Application.Features.Vacations.Commands.Create;
using Application.Features.Vacations.Queries.GetByUser;
using Application.Features.Vacations.Query.GetAll;
using Application.Features.Vacations.Query.GetById;
using Application.Features.Vacations.Query.GetPendingForMe;
using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Vacations")]
    [Authorize]
    public class VacationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserService _userService;

        public VacationsController(IMediator mediator, IUserService userService)
        {
            _mediator = mediator;
            _userService = userService;
        }

        // ========== تقديم إجازة ==========
        // قد ترجع أكثر من إجازة (مثلاً: أيام مدفوعة + أيام غير مدفوعة عند تجاوز الحد الشهري)
        [HttpPost("Create")]
        [Authorize(Policy = "CreateVacation")]
        public async Task<ActionResult<List<VacationResponseDto>>> Create(
            [FromForm] CreateVacationRequestDto dto)
        {
            var currentUserId = _userService.UserId;
            return Ok(await _mediator.Send(new CreateVacationCommand(dto, currentUserId)));
        }

        // ========== إلغاء إجازة (من صاحبها، قبل اعتماد رئيس الفرع نهائياً) ==========
        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CreateVacation")]
        public async Task<ActionResult> Cancel(int id)
        {
            await _mediator.Send(new CancelVacationCommand(id));
            return Ok(new { message = "تم إلغاء الإجازة بنجاح" });
        }

        // ========== الموافقة / الرفض ==========
        [HttpPut("Approve/{id}")]
        [Authorize(Policy = "ApproveVacation")]
        public async Task<ActionResult> Approve(
            int id, [FromBody] ApproveVacationRequestDto dto)
        {
            await _mediator.Send(new ApproveVacationCommand(id, dto));
            return Ok(new { message = "تم تحديث حالة الإجازة بنجاح" });
        }

        // ========== الإجازات المعلقة للموافق الحالي ==========
        [HttpGet("PendingForMe")]
        [Authorize(Policy = "ApproveVacation")]
        public async Task<ActionResult<List<VacationResponseDto>>> GetPendingForMe()
            => Ok(await _mediator.Send(new GetPendingVacationsForMeQuery()));

        // ========== تفاصيل إجازة (بعد التحقق من الملكية/النطاق داخل الـ Handler) ==========
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "ViewVacations")]
        public async Task<ActionResult<VacationResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetVacationByIdQuery(id)));

        // ========== الإجازات ضمن نطاقي: SuperAdmin=الكل، BranchManager=فرعه، Manager=قسمه، غير ذلك=إجازاتي ==========
        [HttpGet("GetAll")]
        [Authorize(Policy = "ViewVacations")]
        public async Task<ActionResult<List<VacationResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllVacationsQuery()));

        // ========== إجازات مستخدم معين (بعد التحقق من الملكية/النطاق داخل الـ Handler) ==========
        [HttpGet("User/{userId}")]
        [Authorize(Policy = "ViewVacations")]
        public async Task<ActionResult<List<VacationResponseDto>>> GetByUser(int userId)
            => Ok(await _mediator.Send(new GetVacationsByUserQuery(userId)));

        // ========== إجازاتي أنا ==========
        [HttpGet("My")]
        [Authorize(Policy = "ViewVacations")]
        public async Task<ActionResult<List<VacationResponseDto>>> GetMy()
        {
            var currentUserId = _userService.UserId;
            return Ok(await _mediator.Send(new GetVacationsByUserQuery(currentUserId)));
        }
    }
}
