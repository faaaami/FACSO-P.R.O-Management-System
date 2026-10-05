using MediatR;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;

namespace PROManagementSystem.Application.Client.Queries.GetMyCompany;

public sealed record GetMyCompanyQuery() : IRequest<GetClientCompanyByIdResponseDto?>;