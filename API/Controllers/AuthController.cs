using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Users.Queries.GetMe;
using Application.Features.Users.Queries.GetMyPermissions;
using Application.Features.Users.Signature;
using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IMediator _mediator;

        public AuthController(IAuthService authService, IMediator mediator)
        {
            _authService = authService;
            _mediator = mediator;
        }

        // ========== بيانات المستخدم الحالي (الفرع/القسم/المكتب/الدور) ==========
        [HttpGet("Me")]
        [Authorize]
        public async Task<ActionResult<UserResponseDto>> Me()
            => Ok(await _mediator.Send(new GetMeQuery()));

        // صلاحيات دوري الآن (لتحديث الواجهة بعد تعديل الدور بلا إعادة دخول)
        [HttpGet("Permissions")]
        [Authorize]
        public async Task<ActionResult<MyPermissionsDto>> Permissions()
            => Ok(await _mediator.Send(new GetMyPermissionsQuery()));

        // ========== توقيعي (ManageMySignature): يُحفظ مع القرارات الموقَّعة ويُطبع على الأوراق ==========
        [HttpGet("Signature")]
        [Authorize(Policy = "ManageMySignature")]
        public async Task<IActionResult> GetSignature()
            => Ok(new { image = await _mediator.Send(new GetMySignatureQuery()) });

        [HttpPut("Signature")]
        [Authorize(Policy = "ManageMySignature")]
        public async Task<IActionResult> SetSignature([FromBody] SignatureRequestDto dto)
        {
            await _mediator.Send(new SetMySignatureCommand(dto.Image, dto.Password));
            return Ok(new { message = string.IsNullOrWhiteSpace(dto.Image) ? "تم حذف التوقيع" : "تم حفظ التوقيع" });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            if (result == null)
                return Unauthorized(new { message = "البريد أو كلمة المرور غير صحيحة" });

            return Ok(result);
        }

        [HttpPost("register")]
        [Authorize(Policy = "CreateUser")]
        
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result)
                return BadRequest(new { message = "البريد مستخدم مسبقاً أو فشل التسجيل" });

            return Ok(new { message = "تم التسجيل بنجاح" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            await _authService.LogoutAsync(token);
            return Ok(new { message = "تم تسجيل الخروج بنجاح" });
        }
    }
}
