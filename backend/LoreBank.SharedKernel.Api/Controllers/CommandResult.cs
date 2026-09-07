using Microsoft.AspNetCore.Mvc;

namespace LoreBank.SharedKernel.Api.Controllers;

// Le résultat d'une commande au bord HTTP : 204, aucun corps. Un type du socle
// plutôt que le NoContentResult nu pour que le type de retour de l'action
// affirme ce qu'elle sert — c'est cette affirmation que la Description
// OpenAPI lit (DescriptionConvention), sans qu'un module écrive un seul
// [ProducesResponseType].
public sealed class CommandResult : NoContentResult;
