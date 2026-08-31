using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using PROManagementSystem.Application.Agent.Commands.AgentUpdateDocument;

namespace PROManagementSystem.Api.Common;

/// <summary>
/// OpenAPI-only operation filter that injects a request body into the
/// Agent PATCH document endpoint. The endpoint intentially parses the
/// raw JSON body manually (to reject unsupported fields with a 400);
/// because there is no [FromBody] controller parameter, Swashbuckle does
/// not derive a request body. This filter documents it without changing
/// any runtime behaviour.
/// </summary>
public sealed class AgentPatchRequestBodyFilter
    : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        // Target ONLY PATCH /api/v1/agent/documents/{documentId}
        var isPatch = string.Equals(
            context.ApiDescription.HttpMethod,
            HttpMethods.Patch,
            System.StringComparison.Ordinal);

        var relativePath =
            context.ApiDescription.RelativePath ?? string.Empty;

        var isAgentDocumentPatch =
            relativePath.StartsWith(
                "api/v1/agent/documents/",
                System.StringComparison.OrdinalIgnoreCase) &&
            relativePath.EndsWith(
                "/documents/{documentId}",
                System.StringComparison.OrdinalIgnoreCase);

        if (!isPatch || !isAgentDocumentPatch)
        {
            return;
        }

        // Reuse the Agent allowed-field DTO as the documented schema.
        var schema =
            context.SchemaGenerator.GenerateSchema(
                typeof(AgentUpdateDocumentRequestDto),
                context.SchemaRepository);

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = schema
                }
            }
        };
    }
}
