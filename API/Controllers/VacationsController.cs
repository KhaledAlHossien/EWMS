using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Vacations.Commands.Approve;
using Application.Features.Vacations.Commands.Cancel;
using Application.Features.Vacations.Commands.Create;
using Application.Features.Vacations.Holidays;
using Application.Features.Vacations.Queries.Print;
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

        // ========== معاينة المدة في نموذج الطلب ==========
        // أيام العمل بعد استثناء الجمعة والعطل الرسمية (لا يحتاج ViewHolidays — يخدم من يقدّم إجازة)
        [HttpGet("PreviewDays")]
        [Authorize(Policy = "CreateVacation")]
        public async Task<ActionResult<VacationDaysPreviewDto>> PreviewDays([FromQuery] DateTime start, [FromQuery] DateTime end)
            => Ok(await _mediator.Send(new PreviewVacationDaysQuery(start, end)));

        // ========== تقديم إجازة ==========
        // ترجع قائمة بعنصر واحد (بقي الشكل كما هو للتوافق مع الواجهة — كان الطلب يُقسَّم سابقاً عند التقديم)
        [HttpPost("Create")]
        [Authorize(Policy = "CreateVacation")]
        public async Task<ActionResult<List<VacationResponseDto>>> Create(
            [FromForm] CreateVacationRequestDto dto)
        {
            var currentUserId = _userService.UserId;
            return Ok(await _mediator.Send(new CreateVacationCommand(dto, currentUserId)));
        }

        // ========== إلغاء إجازة (من صاحبها، قبل الاعتماد النهائي) ==========
        [HttpPut("Cancel/{id}")]
        [Authorize(Policy = "CancelVacation")]
        public async Task<ActionResult> Cancel(int id)
        {
            await _mediator.Send(new CancelVacationCommand(id));
            return Ok(new { message = "تم إلغاء الإجازة بنجاح" });
        }

        // ========== الموافقة / الرفض ==========
        [HttpPut("Approve/{id}")]
        [Authorize(Policy = "AnyVacationApprove")]
        // المرحلة (الأولى أو النهائية) وحدّ الفرع يفحصهما الـ handler
        public async Task<ActionResult> Approve(
            int id, [FromBody] ApproveVacationRequestDto dto)
        {
            await _mediator.Send(new ApproveVacationCommand(id, dto));
            return Ok(new { message = "تم تحديث حالة الإجازة بنجاح" });
        }

        // ========== الإجازات المعلقة للموافق الحالي ==========
        [HttpGet("PendingForMe")]
        [Authorize(Policy = "AnyVacationApprove")]
        // المرحلة (الأولى أو النهائية) وحدّ الفرع يفحصهما الـ handler
        public async Task<ActionResult<List<VacationResponseDto>>> GetPendingForMe()
            => Ok(await _mediator.Send(new GetPendingVacationsForMeQuery()));

        // ========== نموذج الطباعة (طلب الإجازة الورقي) — حدّه ما يستطيع عرضه ==========
        [HttpGet("Print/{id}")]
        [Authorize(Policy = "PrintVacation")]
        public async Task<ActionResult<VacationPrintDto>> Print(int id)
            => Ok(await _mediator.Send(new GetVacationPrintQuery(id)));

        // ========== تفاصيل إجازة (بعد التحقق من الملكية/النطاق داخل الـ Handler) ==========
        [HttpGet("Get/{id}")]
        [Authorize(Policy = "AnyVacationView")]
        // ViewVacations / ViewDepartmentVacations / ViewBranchVacations / صلاحيات الموافقة — حدّ كل واحدة في VacationAccess
        public async Task<ActionResult<VacationResponseDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetVacationByIdQuery(id)));

        // ========== الإجازات التي تخصني حسب صلاحياتي ==========
        [HttpGet("GetAll")]
        [Authorize(Policy = "AnyVacationView")]
        // إجازاتي + قسمي (ViewDepartmentVacations) + فرعي (ViewBranchVacations أو الموافقة) — في الـ handler
        public async Task<ActionResult<List<VacationResponseDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllVacationsQuery()));

        // ========== إجازات مستخدم معين (بعد التحقق من الملكية/النطاق داخل الـ Handler) ==========
        [HttpGet("User/{userId}")]
        [Authorize(Policy = "AnyVacationView")]
        // حسب VacationAccess.CanViewUser
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
