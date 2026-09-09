namespace LoreBank.SharedKernel.Domain.Exceptions;

// Une page ou une taille de page hors des bornes du socle (ADR 0027) : levée
// par le moteur de Liste, donc la même règle par HTTP et par ISender. Un
// `page=abc` n'arrive pas ici — c'est le 400 de binding.
public sealed class InvalidPagingException(
    int page,
    int pageSize,
    int maxPageSize
) : DomainException(
    new() {
        ["page"] = page,
        ["pageSize"] = pageSize,
        ["maxPageSize"] = maxPageSize,
    }
);
