using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Le namespace vit sous SharedKernel : la dérivation ne préfixe pas le code,
// qui vaut donc PROBE_FAILURE — la forme des codes de module est épinglée par
// ExceptionCodesTest, pas par le contrat HTTP.
public sealed class ProbeFailureException(string reason) : DomainException(
    new() { ["reason"] = reason }
);
