using Application.DTOs.Response;
using Application.Features.Notifications.Commands.MarkAllAsRead;
using Application.Features.Notifications.Commands.MarkAsRead;
using Application.Features.Notifications.Queries.GetMy;
using Application.Features.Notifications.Queries.GetUnreadCount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/Notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public NotificationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ========== إشعاراتي (الكل، أو غير المقروءة فقط) ==========
        [HttpGet("My")]
        public async Task<ActionResult<List<NotificationResponseDto>>> GetMy([FromQuery] bool unreadOnly = false)
            => Ok(await _mediator.Send(new GetMyNotificationsQuery(unreadOnly)));

        // ========== عدد الإشعارات غير المقروءة ==========
        [HttpGet("UnreadCount")]
        public async Task<ActionResult<int>> GetUnreadCount()
            => Ok(await _mediator.Send(new GetUnreadNotificationCountQuery()));

        // ========== تعليم إشعار واحد كمقروء ==========
        [HttpPut("MarkAsRead/{id}")]
        public async Task<ActionResult> MarkAsRead(int id)
        {
            await _mediator.Send(new MarkNotificationAsReadCommand(id));
            return Ok(new { message = "تم تعليم الإشعار كمقروء" });
        }

        // ========== تعليم كل الإشعارات كمقروءة ==========
        [HttpPut("MarkAllAsRead")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            await _mediator.Send(new MarkAllNotificationsAsReadCommand());
            return Ok(new { message = "تم تعليم كل الإشعارات كمقروءة" });
        }
    }
}
