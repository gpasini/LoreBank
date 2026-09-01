namespace LoreBank.SharedKernel.Domain.Exceptions;

// Marqueur : le filtre d'exceptions en déduit un 404 sans connaître aucun module.
public abstract class NotFoundException(Dictionary<string, object> parameters) : DomainException(parameters);
