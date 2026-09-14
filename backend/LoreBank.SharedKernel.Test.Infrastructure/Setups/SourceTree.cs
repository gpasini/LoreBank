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

    // Tout le repo, hors ce qui n'est pas source : le dépôt git, les
    // dépendances installées, les sorties de build et de mesure. Pour les
    // gardes qui lisent ce que les humains et les agents écrivent partout —
    // les citations d'ADR (ADR 0037) traversent CLAUDE.md, les skills, les
    // csproj, le front.
    public static IEnumerable<string> RepositoryFiles() => EnumerateBelow(new DirectoryInfo(Root));

    private readonly static string[] NotSource = [
        ".git",
        "node_modules",
        "bin",
        "obj",
        "dist",
        "coverage",
    ];

    private static IEnumerable<string> EnumerateBelow(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles()) {
            yield return file.FullName;
        }

        foreach (var child in directory.EnumerateDirectories()) {
            if (NotSource.Contains(
                    value: child.Name,
                    comparer: StringComparer.Ordinal
                )) {
                continue;
            }

            foreach (var path in EnumerateBelow(child)) {
                yield return path;
            }
        }
    }

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
