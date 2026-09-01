using LoreBank.SharedKernel.Domain.Exceptions;

// Namespace volontairement différent du dossier Fakes/ : c'est le seul moyen
// d'exercer la branche "module" de la dérivation sans faire dépendre ce projet
// d'un vrai module. Ne pas aligner ce namespace sur l'emplacement du fichier.
namespace LoreBank.FakeModule.Domain.Exceptions;

public sealed class ModuleFailureException(string label) : DomainException(
    new() { ["label"] = label }
);
