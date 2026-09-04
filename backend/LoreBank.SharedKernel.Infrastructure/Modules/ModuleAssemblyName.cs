namespace LoreBank.SharedKernel.Infrastructure.Modules;

// La lecture unique, côté socle, de la convention de nommage
// <Racine>.<Module>.<Couche> : HostModule<TDbContext> en dérive les assemblies
// du module. DomainException lit la même convention de son côté (2e segment du
// namespace) pour préfixer ses codes — c'est pourquoi la racine est
// mono-segment : trois segments exactement, sinon les deux lectures divergent.
internal static class ModuleAssemblyName
{
    private const string InfrastructureLayer = "Infrastructure";

    internal static (string Root, string Module) Parse(string assemblyName)
    {
        var segments = assemblyName.Split('.');

        if (segments.Length != 3) {
            throw new InvalidOperationException(
                $"L'assembly « {assemblyName} » ne suit pas la convention <Racine>.<Module>.Infrastructure : "
                + "trois segments exactement, racine mono-segment — la même convention que lit "
                + "DomainException (2e segment du namespace) pour préfixer ses codes d'erreur."
            );
        }

        if (segments[2] != InfrastructureLayer) {
            throw new InvalidOperationException(
                $"L'assembly « {assemblyName} » ne se termine pas par .Infrastructure : "
                + "l'ancre de HostModule<TDbContext> est le DbContext du module, qui vit dans son projet Infrastructure."
            );
        }

        return (segments[0], segments[1]);
    }
}
