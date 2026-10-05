using System.Linq.Expressions;
using FluentValidation;

namespace PROManagementSystem.Application.Common.FileValidation;

/// <summary>
/// Shared FluentValidation rules for document upload commands. Both the
/// admin and agent extract commands carry the same upload payload shape
/// (FileName, ContentType, FileSize, FileStream), so the rules are applied
/// once here and referenced by each command's own validator. When a stream
/// selector is supplied the declared content type is also verified against
/// the file's magic bytes (see <see cref="FileSignatureValidator"/>); only a
/// few leading bytes are inspected and seekable streams are left at the exact
/// position they were found so the downstream flow is unaffected.
/// </summary>
public static class UploadFileValidationRules
{
    public static void ApplyUploadFileRules<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, string>> fileNameSelector,
        Expression<Func<T, string>> contentTypeSelector,
        Expression<Func<T, long>> fileSizeSelector,
        Expression<Func<T, Stream>>? fileStreamSelector = null)
        where T : class
    {
        var getFileName = fileNameSelector.Compile();
        var getContentType = contentTypeSelector.Compile();

        validator.RuleFor(fileNameSelector)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(FileUploadPolicy.MaxFileNameLength)
            .WithMessage(
                $"File name must be at most " +
                $"{FileUploadPolicy.MaxFileNameLength} characters.");

        validator.RuleFor(fileNameSelector)
            .Must(name => FileUploadPolicy.IsExtensionAllowed(name))
            .When(x => !string.IsNullOrWhiteSpace(getFileName(x)))
            .WithMessage("File extension is not allowed.");

        validator.RuleFor(contentTypeSelector)
            .NotEmpty()
            .WithMessage("Content type is required.")
            .Must(contentType => FileUploadPolicy.IsContentTypeAllowed(contentType))
            .When(x => !string.IsNullOrWhiteSpace(getContentType(x)))
            .WithMessage("Content type is not allowed.");

        validator.RuleFor(fileSizeSelector)
            .InclusiveBetween(
                FileUploadPolicy.MinFileSizeBytes,
                FileUploadPolicy.MaxFileSizeBytes)
            .WithMessage(
                $"File size must be between " +
                $"{FileUploadPolicy.MinFileSizeBytes} and " +
                $"{FileUploadPolicy.MaxFileSizeBytes} bytes.");

        validator.RuleFor(x => x)
            .Must(x =>
                FileUploadPolicy.IsMimeExtensionPairValid(
                    getContentType(x),
                    getFileName(x)))
            .When(x =>
                !string.IsNullOrWhiteSpace(getContentType(x)) &&
                !string.IsNullOrWhiteSpace(getFileName(x)))
            .WithName("File")
            .WithMessage(
                "File extension does not correspond to the content type.");

        if (fileStreamSelector is not null)
        {
            var getFileStream = fileStreamSelector.Compile();

            validator.RuleFor(x => x)
                .Must(x =>
                    FileSignatureValidator.IsSignatureValid(
                        getContentType(x),
                        getFileStream(x)))
                .When(x =>
                    FileUploadPolicy.IsContentTypeAllowed(
                        getContentType(x)))
                .WithName("File")
                .WithMessage(FileSignatureValidator.MismatchMessage);
        }
    }
}