using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

public sealed record ProbeCommand(string Label) : ICommand;
