using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Un exemplaire de chaque scalaire admis dans les paramètres d'une exception.
public sealed class ScalarFailureException() : DomainException(
    new() {
        ["text"] = "iban",
        ["amount"] = 20.50m,
        ["count"] = 3,
        ["big"] = 3L,
        ["ratio"] = 0.5,
        ["flag"] = true,
        ["id"] = Guid.Empty,
        ["at"] = DateTimeOffset.UnixEpoch,
        ["day"] = DateOnly.MinValue,
        ["time"] = TimeOnly.MinValue,
        ["kind"] = DayOfWeek.Monday,
    }
);
