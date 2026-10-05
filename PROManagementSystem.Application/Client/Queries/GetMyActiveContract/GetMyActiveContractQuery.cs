using MediatR;
using PROManagementSystem.Application.Contracts.Queries.GetActiveContract;

namespace PROManagementSystem.Application.Client.Queries.GetMyActiveContract;

public sealed record GetMyActiveContractQuery() : IRequest<GetActiveContractResponseDto?>;