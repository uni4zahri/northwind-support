using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    [HttpPost("{id:int}/escalate")]
    public async Task<IActionResult> Escalate(int id, EscalateRequest request,
        [FromServices] SupportDesk.Application.Abstractions.ITicketTriage triage, CancellationToken ct)
    {
        if (request.Reason?.Trim().Length is not (>= 5 and <= 500) || string.IsNullOrWhiteSpace(request.EscalatedBy) || request.EscalatedBy.Length > 100)
            return BadRequest(new ProblemDetails { Detail = "Reason must be 5–500 characters and actor 1–100 characters." });
        return Ok(await triage.EscalateAsync(id, request.Reason.Trim(), request.EscalatedBy, ct));
    }

    [HttpGet("{id:int}/escalations")]
    public Task<IReadOnlyList<SupportDesk.Domain.Aggregates.Tickets.TicketEscalation>> History(int id,
        [FromServices] SupportDesk.Application.Abstractions.ITicketTriage triage, CancellationToken ct) => triage.HistoryAsync(id, ct);

    public sealed record EscalateRequest(string? Reason, string? EscalatedBy);

    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(int id, [FromServices] GetTicketQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it when no agent is given.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);
}
