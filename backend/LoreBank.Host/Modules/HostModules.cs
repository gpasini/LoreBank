using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.Host.Modules;

// La seule source de vérité des modules montés : Program.cs boucle sur cette
// liste et ModuleCompositionTest itère la même — un module déclaré ici est
// forcément monté et testé, un module absent n'est ni l'un ni l'autre.
public static class HostModules
{
    public readonly static IHostModule[] All = [new BankModule(), new LedgerModule()];
}
