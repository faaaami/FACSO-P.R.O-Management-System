using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Customers;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Api.Controllers.ServiceJobs;

/// <summary>Counter operations: walk-in registration, lookup, customer profile and persons.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    public CustomersController(ISender sender) => _sender = sender;

    [HttpPost("customers/walk-in")]
    public async Task<IActionResult> CreateWalkIn(
        [FromBody] CreateWalkInCustomerCommand request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<WalkInCustomerCreatedDto>.SuccessResponse(result));
    }

    [HttpGet("customers/lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] string q, [FromQuery] CustomerType? customerType, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new LookupCustomersQuery(q ?? string.Empty, customerType), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CustomerLookupItemDto>>.SuccessResponse(result));
    }

    [HttpGet("customers/{clientCompanyId:guid}/profile")]
    public async Task<IActionResult> GetProfile(Guid clientCompanyId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerProfileQuery(clientCompanyId), cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.SuccessResponse(result));
    }

    public sealed record CustomerProfileRequest(
        CustomerType CustomerType, string? TaxRegistrationNumber, string? ContactPersonName, string? Notes);

    [HttpPut("customers/{clientCompanyId:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(
        Guid clientCompanyId, [FromBody] CustomerProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateCustomerProfileCommand(clientCompanyId, request.CustomerType,
            request.TaxRegistrationNumber, request.ContactPersonName, request.Notes), cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.SuccessResponse(result));
    }

    [HttpGet("customers/{clientCompanyId:guid}/persons")]
    public async Task<IActionResult> GetPersons(
        Guid clientCompanyId, [FromQuery] PersonType? personType, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCustomerPersonsQuery(clientCompanyId, personType), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PersonDto>>.SuccessResponse(result));
    }

    [HttpGet("persons/{id:guid}")]
    public async Task<IActionResult> GetPerson(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPersonByIdQuery(id), cancellationToken);
        return Ok(ApiResponse<PersonDto>.SuccessResponse(result));
    }

    [HttpPost("entities/{clientEntityId:guid}/persons")]
    public async Task<IActionResult> CreatePerson(
        Guid clientEntityId, [FromBody] PersonInput request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreatePersonCommand(clientEntityId, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PersonDto>.SuccessResponse(result));
    }

    [HttpPut("persons/{id:guid}")]
    public async Task<IActionResult> UpdatePerson(Guid id, [FromBody] PersonInput request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdatePersonCommand(id, request), cancellationToken);
        return Ok(ApiResponse<PersonDto>.SuccessResponse(result));
    }
}
