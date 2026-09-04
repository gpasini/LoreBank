using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// Le mécanisme de migration, jumeau du seam : pour chaque module, résoudre le
// DbContext déclaré par DbContextType et le migrer. La politique — quand
// migrer — appartient aux consommateurs : le verbe `migrate` de l'hôte, et le
// harnais d'intégration qui prépare son Testcontainer. Le démarrage de l'API,
// lui, ne migre jamais (ADR 0006).
public static class ModuleMigrator
{
    public static async Task MigrateAsync(
        IServiceProvider services,
        IEnumerable<IHostModule> modules
    )
    {
        using var scope = services.CreateScope();

        foreach (var module in modules) {
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType);

            await dbContext.Database.MigrateAsync();
        }
    }
}
