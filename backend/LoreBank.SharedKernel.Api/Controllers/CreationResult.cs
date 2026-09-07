using Microsoft.AspNetCore.Mvc;

namespace LoreBank.SharedKernel.Api.Controllers;

// Le résultat d'une création au bord HTTP : 201, en-tête Location, aucun
// corps. Même raison d'être que CommandResult — le type de retour de l'action
// est l'affirmation que la Description OpenAPI lit.
public sealed class CreationResult(
    string actionName,
    object routeValues
) : CreatedAtActionResult(
    actionName: actionName,
    controllerName: null,
    routeValues: routeValues,
    value: null
);
