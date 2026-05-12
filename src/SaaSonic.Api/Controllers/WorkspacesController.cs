using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSonic.Application.Invitations.Commands;
using SaaSonic.Application.Members.Commands;
using SaaSonic.Application.Members.Queries;
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

    // ── Workspaces ────────────────────────────────────────────────────────────

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

    // ── Members ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWorkspaceMembersQuery(id, GetCurrentUserId()), ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeMemberRole(Guid id, Guid userId, [FromBody] ChangeMemberRoleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangeMemberRoleCommand(id, GetCurrentUserId(), userId, request.RoleId), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveMemberCommand(id, GetCurrentUserId(), userId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipRequest request, CancellationToken ct)
    {
        await _mediator.Send(new TransferOwnershipCommand(id, GetCurrentUserId(), request.NewOwnerUserId), ct);
        return NoContent();
    }

    // ── Invitations ───────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType(typeof(InvitationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteMember(Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new InviteMemberCommand(id, GetCurrentUserId(), request.Email, request.RoleId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/invitations/{invitationId:guid}/resend")]
    [ProducesResponseType(typeof(InvitationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResendInvitation(Guid id, Guid invitationId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResendInvitationCommand(id, GetCurrentUserId(), invitationId), ct);
        return Ok(result);
    }

    // No workspace ID needed — the token identifies the invitation
    [HttpPost("invitations/accept")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AcceptInvitationCommand(GetCurrentUserId(), request.Token), ct);
        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.Parse(value!);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateWorkspaceRequest(string Name, string Slug);
public sealed record UpdateWorkspaceRequest(string? Name, string? Slug);
public sealed record InviteMemberRequest(string Email, Guid RoleId);
public sealed record AcceptInvitationRequest(string Token);
public sealed record ChangeMemberRoleRequest(Guid RoleId);
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);
