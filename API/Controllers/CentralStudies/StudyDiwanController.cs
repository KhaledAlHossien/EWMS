using API.Uploads;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.CentralStudies;
using Application.Features.CentralStudies.Responses.Commands.RegisterDiwanDispatch;
using Application.Features.CentralStudies.Responses.Commands.RegisterDiwanReply;
using Application.Features.CentralStudies.Responses.Commands.ReviewDiwanReply;
using Application.Features.CentralStudies.Responses.Queries.GetDiwanReplyFile;
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
    public async Task<ActionResult<StudyResponseDto>> Dispatch(int id, [FromBody] StudyDiwanDispatchFormDto input, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterStudyDiwanDispatchCommand(id, input.Number, input.Date, input.RowVersion), ct));

    [HttpPost("Reply")]
    [Authorize(Policy = StudyPermissions.ManageDiwan)]
    [DisableRequestSizeLimit]
    [DisableFormValueModelBinding]
    public async Task<ActionResult<StudyResponseDto>> Reply(int id, [FromQuery] StudyDiwanDispatchFormDto input,
        [FromQuery] StudyDiwanDecision decision, CancellationToken ct)
        => Ok(await mediator.Send(new RegisterStudyDiwanReplyCommand(id, input.Number, input.Date, decision, input.RowVersion,
            new MultipartUploadReader(Request)), ct));

    [HttpPost("Review")]
    [Authorize(Policy = StudyDiwanRules.HeadPermission)]
    public async Task<ActionResult<StudyResponseDto>> Review(int id, [FromBody] StudyDiwanReviewFormDto input, CancellationToken ct)
        => Ok(await mediator.Send(new ReviewStudyDiwanReplyCommand(id, input.Action, input.Note, input.RowVersion), ct));

    [HttpGet("Reply/{replyId:int}/File")]
    public async Task<IActionResult> ReplyFile(int id, int replyId, CancellationToken ct)
    {
        var file = await mediator.Send(new GetStudyDiwanReplyFileQuery(id, replyId), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: true);
    }
}
