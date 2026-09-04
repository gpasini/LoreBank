using LoreBank.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.SharedKernel.Api.Controllers;

// La base des controllers d'un module : le CQS au bord HTTP devient un fait
// du système de types — SendAsync n'accepte qu'une ICommand et ne renvoie
// rien (204), CreateAsync qu'une ICreationCommand et ne renvoie que Location
// (201, corps vide). Une query ne peut emprunter aucun des deux chemins. Pas
// de QueryAsync : ce serait un pass-through, les lectures gardent leur
// Sender.Send + mapping. [ApiController] est hérité — une annotation de moins
// à recopier par module.
[ApiController]
public abstract class ModuleController(ISender sender) : ControllerBase
{
    protected ISender Sender => sender;

    protected async Task<ActionResult> SendAsync(
        ICommand command,
        CancellationToken cancellationToken
    )
    {
        await sender.Send(
            request: command,
            cancellationToken: cancellationToken
        );

        return NoContent();
    }

    // Le seul retour admis à une commande — l'identifiant créé — sert à
    // construire Location, jamais un corps. Le client qui veut l'état d'après
    // suit Location.
    protected async Task<ActionResult> CreateAsync(
        ICreationCommand command,
        string actionName,
        CancellationToken cancellationToken
    )
    {
        var id = await sender.Send(
            request: command,
            cancellationToken: cancellationToken
        );

        return CreatedAtAction(
            actionName: actionName,
            routeValues: new { id },
            value: null
        );
    }
}
