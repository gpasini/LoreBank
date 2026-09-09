using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoreBank.SharedKernel.Api.Problems;
using LoreBank.SharedKernel.Api.Signals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace LoreBank.SharedKernel.Api.OpenApi;

// Ce que le générateur ne sait pas et que le socle sait : la forme unique
// d'erreur (ApiProblem, docs/erreurs.md) et ses codes (ErrorCode), servis en
// application/problem+json sur toutes les opérations — 400, 404, 409, 422,
// 500, uniformément, la Description ne devine pas ce qu'un handler lève ; l'en-tête
// Location d'un 201 ; un seul media type par sens — application/json, ou
// text/event-stream sur le flux de Signaux (ADR 0026) ; les clés de query
// string en camelCase comme les clés JSON — une ListQuery liée [FromQuery]
// (ADR 0027) est décrite propriété par propriété sous son nom .NET, alors
// que le body de la même surface part en camelCase ; le binding, lui, est
// insensible à la casse, seule la Description change, et c'est ce nom que
// porte une Facette ; et l'en-tête du document —
// un titre, pas de `servers` : la Description décrit une surface, pas un
// déploiement.
public sealed class DescriptionDocumentTransformer(
    string title,
    IReadOnlyList<string> errorCodes
) : IOpenApiDocumentTransformer
{
    public const string ProblemSchemaName = "ApiProblem";

    private const string JsonMediaType = "application/json";

    private readonly static int[] ErrorStatuses = [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status422UnprocessableEntity,
        StatusCodes.Status500InternalServerError,
    ];

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Info.Title = title;
        document.Servers = null;

        document.AddComponent(
            id: ErrorCodes.SchemaName,
            componentToRegister: ErrorCodeSchema()
        );
        document.AddComponent(
            id: ProblemSchemaName,
            componentToRegister: ProblemSchema(document)
        );

        var operations = document.Paths.Values
            .Where(path => path.Operations is not null)
            .SelectMany(path => path.Operations!.Values);

        foreach (var operation in operations) {
            Describe(
                operation: operation,
                document: document
            );
        }

        return Task.CompletedTask;
    }

    private static void Describe(
        OpenApiOperation operation,
        OpenApiDocument document
    )
    {
        operation.Responses ??= [];

        foreach (var parameter in (operation.Parameters ?? []).OfType<OpenApiParameter>()) {
            if (parameter is { In: ParameterLocation.Query, Name: { } name }) {
                parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(name);
            }
        }

        if (operation.RequestBody?.Content is { } requestContent) {
            KeepOnly(
                content: requestContent,
                mediaType: JsonMediaType
            );
        }

        foreach (var response in operation.Responses.Values.OfType<OpenApiResponse>()) {
            if (response.Content is { } content) {
                KeepOnly(
                    content: content,
                    mediaType: content.ContainsKey(SignalStreamResult.ContentType)
                        ? SignalStreamResult.ContentType
                        : JsonMediaType
                );
            }
        }

        if (operation.Responses.TryGetValue(
                key: StatusCodes.Status201Created.ToString(CultureInfo.InvariantCulture),
                value: out var created
            ) && created is OpenApiResponse creation) {
            creation.Headers ??= new Dictionary<string, IOpenApiHeader>();
            creation.Headers["Location"] = new OpenApiHeader {
                Description = "L'URL de la ressource créée : le client la suit pour lire l'état d'après.",
                Required = true,
                Schema = new OpenApiSchema {
                    Type = JsonSchemaType.String,
                    Format = "uri",
                },
            };
        }

        foreach (var status in ErrorStatuses) {
            operation.Responses[status.ToString(CultureInfo.InvariantCulture)] = new OpenApiResponse {
                Description = ReasonPhrases.GetReasonPhrase(status),
                Content = new Dictionary<string, OpenApiMediaType> {
                    [ApiProblem.ContentType] = new() {
                        Schema = new OpenApiSchemaReference(
                            referenceId: ProblemSchemaName,
                            hostDocument: document
                        ),
                    },
                },
            };
        }
    }

    private static void KeepOnly(
        IDictionary<string, OpenApiMediaType> content,
        string mediaType
    )
    {
        foreach (var other in content.Keys.Where(key => key != mediaType).ToArray()) {
            content.Remove(other);
        }
    }

    private OpenApiSchema ErrorCodeSchema() => new() {
        Type = JsonSchemaType.String,
        Enum = errorCodes.Select(code => (JsonNode) JsonValue.Create(code)).ToList(),
    };

    // La forme de docs/erreurs.md : `title`, `status` et `traceId` toujours,
    // `code` et `parameters` sur tout sauf le 500 — d'où les trois requis.
    private static OpenApiSchema ProblemSchema(OpenApiDocument document) => new() {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "title", "status", "traceId" },
        Properties = new Dictionary<string, IOpenApiSchema> {
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            ["code"] = new OpenApiSchemaReference(
                referenceId: ErrorCodes.SchemaName,
                hostDocument: document
            ),
            ["parameters"] = new OpenApiSchema {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema(),
            },
            ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
        },
    };
}
