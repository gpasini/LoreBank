namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Les tests qui gardent le repo lui-même — la Télémétrie sur les csproj, le
// Gel sur les fichiers de politique — tournent depuis bin/ et doivent
// remonter jusqu'aux sources. Un seul ancrage, la solution, et un seul
// walker dans le harnais : le second aurait divergé du premier.
public static class SourceTree
{
    // Le dossier qui porte LoreBank.slnx.
    public static string Backend { get; } = FindBackend();

    // Son parent : .editorconfig, mise.toml et .github/ vivent un cran
    // au-dessus du backend.
    public static string Root { get; } = Directory.GetParent(Backend)?.FullName
                                         ?? throw new InvalidOperationException($"Le dossier {Backend} n'a pas de parent.");

    // Les fichiers de source du backend, hors sortie de build : les scans du
    // Gel ne doivent jamais tomber sur une copie dans obj/ ou bin/.
    public static IEnumerable<string> BackendSources(string searchPattern) => Directory
        .EnumerateFiles(
            path: Backend,
            searchPattern: searchPattern,
            searchOption: SearchOption.AllDirectories
        )
        .Where(path => !path.Contains(
                   value: $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                   comparisonType: StringComparison.Ordinal
               )
               && !path.Contains(
                   value: $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                   comparisonType: StringComparison.Ordinal
               )
        );

    private static string FindBackend()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(
                   path1: directory.FullName,
                   path2: "LoreBank.slnx"
               ))) {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("LoreBank.slnx introuvable au-dessus du dossier de test.");
    }
}
