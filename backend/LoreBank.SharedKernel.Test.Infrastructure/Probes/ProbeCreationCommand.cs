using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

public sealed record ProbeCreationCommand(string Label) : ICreationCommand;
