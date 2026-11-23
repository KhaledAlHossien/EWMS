using API.Uploads;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Responses;
using Domain.Entities.CentralStudiesBranch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.CentralStudies;

[ApiController]
[Route("api/StudyResponses/Diwan/{id:int}")]
[Authorize(Policy = StudyPermissions.ViewResponses)]
public sealed class StudyDiwanController(IMediator mediator) : ControllerBase
{
    [HttpPost("Dispatch")]
    [Authorize(Policy = StudyPermissions.ManageDiwan)]
    public async Task<ActionResult<StudyResponseDto>> Dispatch(int id, [FromBody] DispatchInput input, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterStudyDiwanDispatch(id, input.Number, input.Date, input.RowVersion), ct));

    [HttpPost("Reply")]
    [Authorize(Policy = StudyPermissions.ManageDiwan)]
    [DisableRequestSizeLimit]
    [DisableFormValueModelBinding]
    public async Task<ActionResult<StudyResponseDto>> Reply(int id, [FromQuery] DispatchInput input,
        [FromQuery] StudyDiwanDecision decision, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterStudyDiwanReply(id, input.Number, input.Date, decision, input.RowVersion,
            new MultipartUploadReader(Request)), ct));

    [HttpPost("Review")]
    [Authorize(Policy = StudyDiwanRules.HeadPermission)]
    public async Task<ActionResult<StudyResponseDto>> Review(int id, [FromBody] ReviewInput input, CancellationToken ct)
        => Ok(await mediator.Send(new ReviewStudyDiwanReply(id, input.Action, input.Note, input.RowVersion), ct));

    [HttpGet("Reply/{replyId:int}/File")]
    public async Task<IActionResult> ReplyFile(int id, int replyId, CancellationToken ct)
    {
        var file = await mediator.Send(new GetStudyDiwanReplyFile(id, replyId), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    public record DispatchInput(string Number, DateOnly Date, string RowVersion);
    public record ReviewInput(StudyDiwanReviewAction Action, string? Note, string RowVersion);
}
