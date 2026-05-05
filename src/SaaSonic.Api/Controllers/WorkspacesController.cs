using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSonic.Application.Workspaces.Commands;
using SaaSonic.Application.Workspaces.Queries;
using System.Security.Claims;

namespace SaaSonic.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
[Authorize]
public sealed class WorkspacesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkspacesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWorkspaces(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyWorkspacesQuery(GetCurrentUserId()), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWorkspace(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWorkspaceQuery(GetCurrentUserId(), id), ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateWorkspaceCommand(GetCurrentUserId(), request.Name, request.Slug), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateWorkspaceCommand(GetCurrentUserId(), id, request.Name, request.Slug), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteWorkspace(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWorkspaceCommand(GetCurrentUserId(), id), ct);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}

// --- Request DTOs ---

public sealed record CreateWorkspaceRequest(string Name, string Slug);
public sealed record UpdateWorkspaceRequest(string? Name, string? Slug);
