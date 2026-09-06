using LoreBank.SharedKernel.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoreBank.Probe.Infrastructure.Persistence;

// dotnet-ef ne peut pas composer ProbeDbContext via l'hôte : le ProbeModule
// n'est pas dans HostModules.All (ADR 0017). Cette factory ne sert qu'au
// design-time — `migrations add` ne se connecte pas, la chaîne est un
// placeholder — et le dispatcher est neutre : une migration ne produit aucun
// fait métier (même argument que DataMigrationRunner, ADR 0013).
public sealed class ProbeDbContextFactory : IDesignTimeDbContextFactory<ProbeDbContext>
{
    public ProbeDbContext CreateDbContext(string[] args) =>
        new(
            options: new DbContextOptionsBuilder<ProbeDbContext>()
                .UseNpgsql("Host=localhost;Database=design-time-only")
                .Options,
            dispatcher: new NoOpDomainEventDispatcher()
        );
}
