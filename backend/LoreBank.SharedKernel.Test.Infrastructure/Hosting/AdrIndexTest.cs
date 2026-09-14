using System.Globalization;
using System.Text.RegularExpressions;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// L'index des ADR (ADR 0037) : docs/adr/README.md est une carte écrite à la
// main — une ligne par ADR avec son statut et son thème, une table thème →
// ADR — et ce test la tient vraie. Sans lui, la carte ment dès le prochain
// ADR ajouté sans sa ligne, ou remplacé sans que la colonne Statut bouge.
//
// Il tient aussi ce qui fait du numéro un identifiant : les fichiers sont
// contigus de 0001 à N, et toute mention « ADR NNNN » dans le repo désigne
// un fichier — un ADR ne se supprime pas, il se marque remplacé.
//
// Le gabarit (section « Écrire le prochain » de l'index) est vérifié à
// partir de 0037 : les trente-six premiers sont d'avant lui, et on ne les
// retouche pas pour ça. La ligne Statut, qu'ils ont tous, se vérifie partout.
[TestFixture]
public sealed class AdrIndexTest
{
    private const string Map =
        "l'index des ADR (ADR 0037) : une carte écrite à la main, tenue vraie plutôt qu'espérée";

    // Les ADR d'avant le gabarit gardent leur forme ; ceux-ci le suivent.
    private const int FirstTemplatedAdr = 37;

    private readonly static Regex FileName = new(
        pattern: @"^(\d{4})-[a-z0-9-]+\.md$",
        options: RegexOptions.CultureInvariant
    );

    private readonly static Regex StatusLine = new(
        pattern: @"^> Statut : accepté — \d{4}-\d{2}-\d{2}",
        options: RegexOptions.CultureInvariant
    );

    private readonly static Regex Superseded = new(
        pattern: @"remplacé par l'ADR (\d{4})",
        options: RegexOptions.CultureInvariant
    );

    private readonly static Regex IndexRow = new(
        pattern: @"^\| \[(\d{4})\]\(([^)]+)\) \| (.+?) \| (.+?) \| (.+?) \|$",
        options: RegexOptions.CultureInvariant
    );

    private readonly static Regex ThemeRow = new(
        pattern: @"^\| ([^|\[]+?) \| (\d{4}(?:, \d{4})*) \|$",
        options: RegexOptions.CultureInvariant
    );

    private readonly static Regex Citation = new(
        pattern: @"ADR[ -](\d{4})",
        options: RegexOptions.CultureInvariant
    );

    private readonly static string[] TemplateSections = [
        "## La décision",
        "## Garde-fous",
        "## Options écartées",
    ];

    private static string AdrDirectory => Path.Combine(
        path1: SourceTree.Root,
        path2: "docs",
        path3: "adr"
    );

    private static string Index => Path.Combine(
        path1: AdrDirectory,
        path2: "README.md"
    );

    [Test]
    public void Directory_ShouldHoldOnlyAdrsAndTheIndex_WhenItIsRead() =>
        Directory.EnumerateFiles(AdrDirectory)
            .Select(Path.GetFileName)
            .Where(name => name != "README.md" && !FileName.IsMatch(name!))
            .Should()
            .BeEmpty(Because(
                rule: "docs/adr/ porte des ADR nommés NNNN-titre-en-kebab.md et l'index, rien d'autre — un fichier hors du motif échapperait à la carte",
                geste: "nommer le fichier NNNN-titre.md, ou le déplacer hors de docs/adr/"
            ));

    [Test]
    public void Files_ShouldBeNumberedContiguously_WhenTheDirectoryIsRead() =>
        Adrs()
            .Select(adr => adr.Number)
            .Should()
            .Equal(
                Enumerable.Range(
                    start: 1,
                    count: Adrs().Count
                ),
                because: Because(
                    rule: "les numéros vont de 0001 à N sans trou ni doublon — le numéro est un identifiant, cité partout dans le repo",
                    geste: "un ADR ne se supprime pas, il se marque « remplacé par l'ADR NNNN » ; un doublon se renumérote avant d'être cité"
                )
            );

    [Test]
    public void Files_ShouldOpenWithTheStatusLine_WhenTheyAreRead() =>
        Adrs()
            .Where(adr => !StatusLine.IsMatch(adr.Lines.ElementAtOrDefault(2) ?? string.Empty))
            .Select(adr => adr.File)
            .Should()
            .BeEmpty(Because(
                rule: "la ligne 3 de tout ADR est « > Statut : accepté — AAAA-MM-JJ » — c'est d'elle que l'index dérive sa colonne Statut",
                geste: "écrire la ligne Statut sous le titre, séparée par une ligne vide"
            ));

    [Test]
    public void Files_ShouldFollowTheTemplate_WhenTheyComeAfterIt()
    {
        var missing = Adrs()
            .Where(adr => adr.Number >= FirstTemplatedAdr)
            .SelectMany(adr => TemplateSections
                .Where(section => !adr.Lines.Contains(
                    value: section,
                    comparer: StringComparer.Ordinal
                ))
                .Select(section => $"{adr.File} : {section}")
            )
            .ToList();

        missing
            .Should()
            .BeEmpty(Because(
                rule: $"à partir de {FirstTemplatedAdr:0000}, un ADR porte « La décision », « Garde-fous » et « Options écartées » — le gabarit de l'index, sans quoi le prochain dérive encore",
                geste: "ajouter la section manquante, vide de garde-fou s'il n'y en a aucun, mais en disant pourquoi (ADR 0035)"
            ));
    }

    [Test]
    public void Index_ShouldListEveryFileOnce_WhenItIsRead() =>
        IndexRows()
            .Select(row => (row.Number, row.Link))
            .Should()
            .Equal(
                Adrs().Select(adr => (adr.Number, adr.File)),
                because: Because(
                    rule: "l'index a une ligne par fichier, dans l'ordre, dont le lien est le nom du fichier",
                    geste: "ajouter la ligne de l'ADR au tableau « Par numéro » de docs/adr/README.md, ou corriger son lien"
                )
            );

    [Test]
    public void Index_ShouldCarryTheTitleOfEachFile_WhenItIsRead() =>
        Mismatches(
                expected: adr => adr.Title,
                actual: row => row.Title
            )
            .Should()
            .BeEmpty(Because(
                rule: "le titre d'une ligne de l'index est le « # » du fichier",
                geste: "recopier le titre du fichier dans docs/adr/README.md"
            ));

    [Test]
    public void Index_ShouldCarryTheStatusOfEachFile_WhenItIsRead() =>
        Mismatches(
                expected: adr => adr.Status,
                actual: row => row.Status
            )
            .Should()
            .BeEmpty(Because(
                rule: "le statut d'une ligne de l'index est dérivé de la ligne Statut du fichier : « accepté », ou « remplacé par NNNN » quand elle dit « remplacé par l'ADR NNNN »",
                geste: "corriger la colonne Statut dans docs/adr/README.md — un ADR remplacé se lit comme tel sans ouvrir le fichier"
            ));

    [Test]
    public void ThemeTable_ShouldPartitionTheAdrs_WhenItIsRead() =>
        ThemeRows()
            .SelectMany(row => row.Numbers.Select(number => (number, row.Theme)))
            .OrderBy(entry => entry.number)
            .Should()
            .Equal(
                IndexRows().Select(row => (row.Number, row.Theme)),
                because: Because(
                    rule: "la table « Par thème » est une partition : chaque ADR y figure une fois, sous le thème de sa ligne — un seul thème par ADR",
                    geste: "ajouter le numéro à la ligne de son thème dans docs/adr/README.md, ou aligner les deux tables"
                )
            );

    [Test]
    public void Citations_ShouldResolveToAFile_WhenTheRepositoryIsScanned()
    {
        var known = Adrs().Select(adr => adr.Number).ToHashSet();

        var dangling = SourceTree.RepositoryFiles()
            .SelectMany(path => File.ReadLines(path)
                .SelectMany(line => Citation.Matches(line).Select(match => int.Parse(
                    s: match.Groups[1].Value,
                    provider: CultureInfo.InvariantCulture
                )))
                .Where(number => !known.Contains(number))
                .Distinct()
                .Select(number => $"{Relative(path)} → ADR {number:0000}")
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        dangling
            .Should()
            .BeEmpty(Because(
                rule: "toute mention « ADR NNNN » du repo désigne un fichier de docs/adr/ — le numéro est un identifiant, il ne se renumérote pas et un ADR ne se supprime pas",
                geste: "corriger la citation, ou restaurer l'ADR et le marquer remplacé"
            ));
    }

    private static IReadOnlyList<string> Mismatches(
        Func<Adr, string> expected,
        Func<Row, string> actual
    ) => Adrs()
        .Join(
            inner: IndexRows(),
            outerKeySelector: adr => adr.Number,
            innerKeySelector: row => row.Number,
            resultSelector: (adr, row) => (adr, row)
        )
        .Where(pair => expected(pair.adr) != actual(pair.row))
        .Select(pair => $"{pair.adr.File} : « {actual(pair.row)} » dans l'index, « {expected(pair.adr)} » dans le fichier")
        .ToList();

    private static IReadOnlyList<Adr> Adrs() => Directory.EnumerateFiles(AdrDirectory)
        .Select(path => (path, match: FileName.Match(Path.GetFileName(path))))
        .Where(entry => entry.match.Success)
        .Select(entry => Adr.Read(
            path: entry.path,
            number: int.Parse(
                s: entry.match.Groups[1].Value,
                provider: CultureInfo.InvariantCulture
            )
        ))
        .OrderBy(adr => adr.Number)
        .ToList();

    private static IReadOnlyList<Row> IndexRows() => IndexLines()
        .Select(line => IndexRow.Match(line))
        .Where(match => match.Success)
        .Select(match => new Row(
            Number: int.Parse(
                s: match.Groups[1].Value,
                provider: CultureInfo.InvariantCulture
            ),
            Link: match.Groups[2].Value,
            Title: match.Groups[3].Value,
            Status: match.Groups[4].Value,
            Theme: match.Groups[5].Value
        ))
        .ToList();

    private static IReadOnlyList<(string Theme, IReadOnlyList<int> Numbers)> ThemeRows() => IndexLines()
        .Select(line => ThemeRow.Match(line))
        .Where(match => match.Success)
        .Select(match => (
            match.Groups[1].Value,
            (IReadOnlyList<int>) match.Groups[2].Value
                .Split(", ")
                .Select(number => int.Parse(
                    s: number,
                    provider: CultureInfo.InvariantCulture
                ))
                .ToList()
        ))
        .ToList();

    private static string[] IndexLines() => File.Exists(Index) ? File.ReadAllLines(Index) : [];

    private static string Because(
        string rule,
        string geste
    ) => $"{Map} — {rule} ; le geste : {geste}";

    private static string Relative(string path) => Path
        .GetRelativePath(
            relativeTo: SourceTree.Root,
            path: path
        )
        .Replace(
            oldChar: Path.DirectorySeparatorChar,
            newChar: '/'
        );

    private sealed record Row(
        int Number,
        string Link,
        string Title,
        string Status,
        string Theme
    );

    private sealed record Adr(
        int Number,
        string File,
        string Title,
        string Status,
        IReadOnlyList<string> Lines
    )
    {
        public static Adr Read(
            string path,
            int number
        )
        {
            var lines = System.IO.File.ReadAllLines(path);

            // La ligne Statut peut se poursuivre sur les lignes de citation
            // qui la suivent : « remplacé par » peut tomber sur la deuxième.
            var status = string.Join(
                separator: ' ',
                values: lines.Skip(2).TakeWhile(line => line.StartsWith('>'))
            );

            var superseded = Superseded.Match(status);

            return new Adr(
                Number: number,
                File: Path.GetFileName(path),
                Title: lines.Length > 0 && lines[0].StartsWith(
                    value: "# ",
                    comparisonType: StringComparison.Ordinal
                )
                    ? lines[0][2..]
                    : string.Empty,
                Status: superseded.Success ? $"remplacé par {superseded.Groups[1].Value}" : "accepté",
                Lines: lines
            );
        }
    }
}
