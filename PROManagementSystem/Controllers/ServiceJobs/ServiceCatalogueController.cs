using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.ServiceCatalogue;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.ServiceJobs;

/// <summary>Admin management of the service catalogue and subscription pricing.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class ServiceCatalogueController : ControllerBase
{
    private readonly ISender _sender;

    public ServiceCatalogueController(ISender sender) => _sender = sender;

    [HttpGet("services")]
    public async Task<IActionResult> GetServices(
        [FromQuery] ServiceCategory? category,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetServiceDefinitionsQuery(category, search, includeInactive), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ServiceDefinitionListItemDto>>.SuccessResponse(result));
    }

    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> GetService(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetServiceDefinitionByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<ServiceDefinitionDto>.SuccessResponse(result));
    }

    [HttpPost("services")]
    public async Task<IActionResult> CreateService(
        [FromBody] ServiceDefinitionInput request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateServiceDefinitionCommand(request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ServiceDefinitionDto>.SuccessResponse(result));
    }

    [HttpPut("services/{id:guid}")]
    public async Task<IActionResult> UpdateService(
        Guid id, [FromBody] ServiceDefinitionInput request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateServiceDefinitionCommand(id, request), cancellationToken);
        return Ok(ApiResponse<ServiceDefinitionDto>.SuccessResponse(result));
    }

    public sealed record SetActiveRequest(bool IsActive);

    [HttpPatch("services/{id:guid}/active")]
    public async Task<IActionResult> SetActive(
        Guid id, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetServiceDefinitionActiveCommand(id, request.IsActive), cancellationToken);
        return Ok(ApiResponse<ServiceDefinitionDto>.SuccessResponse(result));
    }

    // ------------------------------------------------------------ subscription pricing

    [HttpGet("contracts/{contractId:guid}/service-prices")]
    public async Task<IActionResult> GetContractPrices(Guid contractId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetContractServicePricesQuery(contractId), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ContractServicePriceDto>>.SuccessResponse(result));
    }

    public sealed record ContractPriceRequest(decimal ServiceFee, bool IncludedInRetainer);

    [HttpPut("contracts/{contractId:guid}/service-prices/{serviceId:guid}")]
    public async Task<IActionResult> SetContractPrice(
        Guid contractId, Guid serviceId, [FromBody] ContractPriceRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpsertContractServicePriceCommand(contractId, serviceId, request.ServiceFee, request.IncludedInRetainer),
            cancellationToken);
        return Ok(ApiResponse<ContractServicePriceDto>.SuccessResponse(result));
    }

    [HttpDelete("contracts/{contractId:guid}/service-prices/{serviceId:guid}")]
    public async Task<IActionResult> DeleteContractPrice(Guid contractId, Guid serviceId, CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteContractServicePriceCommand(contractId, serviceId), cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { contractId, serviceId }));
    }
}

/// <summary>Read-only active catalogue for every signed-in role (no internal pricing).</summary>
[ApiController]
[Route("api/v1/services")]
[Authorize]
public sealed class PublicServiceCatalogueController : ControllerBase
{
    private readonly ISender _sender;

    public PublicServiceCatalogueController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetCatalogue([FromQuery] ServiceCategory? category, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPublicServiceCatalogueQuery(category), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PublicServiceDto>>.SuccessResponse(result));
    }
}
