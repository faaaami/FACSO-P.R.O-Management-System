using FluentValidation;

namespace PROManagementSystem.Application.Users.Queries.GetMyProfile;

public sealed class GetMyProfileQueryValidator
    : AbstractValidator<GetMyProfileQuery>
{
    public GetMyProfileQueryValidator()
    {
        // No request fields to validate.
    }
}