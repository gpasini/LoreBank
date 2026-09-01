using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Nom volontairement porteur d'un acronyme : sert à épingler que la dérivation
// sépare un acronyme du mot capitalisé qui le suit (IBANFailure → IBAN_FAILURE).
public sealed class IBANFailureException : DomainException;
