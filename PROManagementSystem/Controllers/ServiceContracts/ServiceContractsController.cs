using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;
using PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;
using PROManagementSystem.Application.Contracts.Queries.GetActiveContract;
using PROManagementSystem.Application.Contracts.Queries.GetClientContracts;
using PROManagementSystem.Application.Contracts.Queries.GetServiceContract;

namespace PROManagementSystem.Api.Controllers.ServiceContracts;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public sealed class ServiceContractsController : ControllerBase
{
    private readonly ISender _sender;

    public ServiceContractsController(
        ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("clients/{id:guid}/contracts")]
    public async Task<IActionResult> Create(
        Guid id,
        [FromBody] CreateServiceContractRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new CreateServiceContractCommand(
                id,
                request.ContractNumber,
                request.StartDate,
                request.RetainerAmount,
                request.Terms);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<CreateServiceContractResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("clients/{id:guid}/contracts")]
    public async Task<IActionResult> GetHistory(
    Guid id,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var result =
            await _sender.Send(
                new GetClientContractsQuery(id, page, pageSize),
                cancellationToken);

        return Ok(
            ApiResponse<GetClientContractsResponseDto>
                .SuccessResponse(result));
    }

    [HttpGet("clients/{id:guid}/contracts/active")]
    public async Task<IActionResult> GetActive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result =
            await _sender.Send(
                new GetActiveContractQuery(id),
                cancellationToken);

        return Ok(
            ApiResponse<GetActiveContractResponseDto?>
                .SuccessResponse(result));
    }

    [HttpGet("contracts/{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result =
            await _sender.Send(
                new GetServiceContractQuery(id),
                cancellationToken);

        return Ok(
            ApiResponse<GetServiceContractResponseDto>
                .SuccessResponse(result));
    }

    [HttpPatch("contracts/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateServiceContractRequest request,
        CancellationToken cancellationToken)
    {
        var command =
            new UpdateServiceContractCommand(
                id,
                request.StartDate,
                request.EndDate,
                request.RetainerAmount,
                request.Terms);

        var result =
            await _sender.Send(
                command,
                cancellationToken);

        return Ok(
            ApiResponse<UpdateServiceContractResponseDto>
                .SuccessResponse(result));
    }
}