using System.Reflection;
using System.Xml.Linq;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le Gel (ADR 0031) : les Portes se gardent elles-mêmes. Un agent coincé sur
// un avertissement a un chemin plus court que corriger le code — trois mots
// dans .editorconfig, une ligne retirée de BannedSymbols.txt, un step
// supprimé de la CI. Le build redevient vert, la CI aussi, et le diff a l'air
// d'une ligne de configuration. Ces assertions rendent le geste explicite :
// la liste gelée se modifie avec le code qu'elle couvre, ou rien ne passe.
//
// Deux régimes : interdiction nue là où l'état est à zéro et doit le rester,
// liste gelée là où il est non vide et légitime.
//
// Le scan est textuel. La réflexion ne verrait que cette assembly — le
// harnais ne référence pas les projets de test des modules, et ne doit pas :
// ce serait inverser la dépendance. Corollaire, ce fichier porte ses propres
// motifs en littéraux et se signalerait lui-même ; il ne s'exclut donc pas du
// scan, il marque ces lignes-là (« gel:motif ») et elles seules.
//
// Limite assumée : une fixture désactivée en bloc, ou ce fichier supprimé,
// ne peut pas se rattraper elle-même. C'est un diff visible, pas trois mots
// noyés dans 215 lignes — la relecture le tient.
[TestFixture]
public sealed class QualityGateFreezeTest
{
    private const string Marker = "gel:motif";

    private const string Freeze =
        "le Gel (ADR 0031) : desserrer une Porte est légitime, jamais silencieux";

    // Interdiction nue — l'état est à zéro aujourd'hui, aucun franchissement
    // n'a de cas légitime connu.
    private readonly static string[] Disarming = [
        "[Ignore", // gel:motif
        "Assert.Ignore", // gel:motif
        "[Explicit", // gel:motif
    ];

    private readonly static string[] Suppressing = [
        "SuppressMessage", // gel:motif
    ];

    private readonly static string[] LocalSuppression = [
        "#pragma warning disable", // gel:motif
    ];

    private readonly static string[] ProjectSuppression = [
        "NoWarn", // gel:motif
        "WarningsNotAsErrors", // gel:motif
    ];

    // Le résidu d'un cycle TDD interrompu (ADR 0032) : le squelette écrit
    // pour voir le RED, jamais remplacé par son corps. La suite le tient
    // dans le cas courant — le test qu'on vient d'écrire est rouge — mais
    // pas sur un chemin qu'aucun test ne traverse.
    private readonly static string[] Residue = [
        "NotImplementedException", // gel:motif
    ];

    // Liste gelée — chaque ligne dotnet_diagnostic de .editorconfig, avec sa
    // section et sa sévérité. Pas seulement les `none` : IDE0036 et IDE0130
    // sont élevées à `warning`, et les rabaisser à `suggestion` les
    // désarmerait sans qu'un scan des `none` le voie.
    private readonly static string[] FrozenSeverities = [
        "[*.cs] IDE0036 = warning",
        "[*.cs] IDE0130 = warning",
        "[*.cs] CA1711 = none",
        "[*.cs] CA1848 = none",
        "[*.cs] CA1873 = none",
        "[*.cs] CA1859 = none",
        "[backend/*.Test.*/**.cs] CA1707 = none",
        "[backend/*.Test.*/**.cs] CA1001 = none",
        "[backend/*.Test.*/**.cs] CA1000 = none",
        "[backend/*.Test.*/**.cs] CA1051 = none",
        "[backend/*.Test.*/**.cs] CA1710 = none",
        "[backend/*.Test.*/**.cs] CA1822 = none",
        "[backend/LoreBank.SharedKernel.Test.Unit/Fakes/*.cs] IDE0130 = none",
    ];

    // Les trois éléments de coverage.runsettings qui portent une exclusion.
    private readonly static string[] CoverageExclusionElements = [
        "Exclude",
        "ExcludeByFile",
        "ExcludeByAttribute",
    ];

    // La carte de couverture (ADR 0029) : ce qu'aucun test ne vise. « Une
    // exclusion de plus déguiserait la carte » — et passerait inaperçue.
    private readonly static string[] FrozenCoverageExclusions = [
        "Exclude = [LoreBank.Probe.*]*",
        "Exclude = [*.Test.*]*",
        "ExcludeByFile = **/obj/**",
        "ExcludeByFile = **/Persistence/Migrations/**",
        "ExcludeByAttribute = GeneratedCodeAttribute",
        "ExcludeByAttribute = CompilerGeneratedAttribute",
    ];

    // Cinq propriétés d'une ligne dont le desserrage est invisible :
    // AnalysisMode passé à Default éteint une trentaine de règles CA sans
    // que rien ne le dise.
    private readonly static string[] FrozenBuildProperties = [
        "TreatWarningsAsErrors = true",
        "AnalysisMode = Recommended",
        "EnforceCodeStyleInBuild = true",
        "NuGetAuditMode = all",
        "NuGetAuditLevel = low",
    ];

    // L'horloge bannie (ADR 0024). Les symboles seuls : le message qui suit
    // le `;` est de la prose, on ne gèle pas de la prose.
    private readonly static string[] FrozenBannedSymbols = [
        "P:System.DateTime.Now",
        "P:System.DateTime.UtcNow",
        "P:System.DateTimeOffset.Now",
        "P:System.DateTimeOffset.UtcNow",
    ];

    // Les entrées de `[tasks.check]` (ADR 0028) : une Porte supprimée est le
    // desserrage maximal. La dernière n'est pas une Porte mais l'horodatage
    // que lit le hook Stop (ADR 0033) — `CiGates` les sépare.
    private readonly static string[] FrozenGates = [
        "mise run //backend:build",
        "mise run //backend:format:check",
        "mise run //backend:openapi:check",
        "mise run //backend:test",
        "mise run //frontend:generate",
        "mise run //frontend:typecheck",
        "mise run //frontend:check",
        "mise run //frontend:audit",
        "mkdir -p .claude && touch .claude/.gates-ran",
    ];

    // Les seules entrées qui sont des Portes : celles que la CI doit rejouer.
    // L'horodatage de `check` n'en est pas une (ADR 0033) — il ne vérifie
    // rien, il date le passage.
    private static IEnumerable<string> CiGates => FrozenGates.Where(entry => entry.StartsWith(
            value: "mise run //",
            comparisonType: StringComparison.Ordinal
        )
    );

    private static string EditorConfig => Path.Combine(
        path1: SourceTree.Root,
        path2: ".editorconfig"
    );

    private static string MiseToml => Path.Combine(
        path1: SourceTree.Root,
        path2: "mise.toml"
    );

    private static string CiWorkflow => Path.Combine(
        path1: SourceTree.Root,
        path2: ".github",
        path3: "workflows",
        path4: "ci.yml"
    );

    private static string QualityDoc => Path.Combine(
        path1: SourceTree.Root,
        path2: "docs",
        path3: "qualite.md"
    );

    [Test]
    public void Sources_ShouldCarryNoIgnoredTest_WhenTheSolutionIsScanned() =>
        SourcesContaining(Disarming)
            .Should()
            .BeEmpty(Because(
                rule: "un test qui ne s'exécute pas est une Porte retirée",
                geste: "corriger le code, ou ouvrir l'interdiction dans QualityGateFreezeTest avec son pourquoi"
            ));

    // Le scan textuel ne verrait pas `[NUnit.Framework.Ignore]` ; la
    // réflexion le voit, mais seulement ici — c'est l'assembly qui porte la
    // garde, donc celle qui compte le plus.
    [Test]
    public void TheHarness_ShouldCarryNoIgnoredTest_WhenItsOwnAssemblyIsRead() =>
        typeof(QualityGateFreezeTest).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<IgnoreAttribute>() is not null
                             || method.GetCustomAttribute<ExplicitAttribute>() is not null)
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .Should()
            .BeEmpty(Because(
                rule: "un test du harnais qui ne s'exécute pas est une Porte retirée",
                geste: "corriger le code, ou ouvrir l'interdiction dans QualityGateFreezeTest avec son pourquoi"
            ));

    [Test]
    public void Sources_ShouldCarryNoAttributeSuppression_WhenTheSolutionIsScanned() =>
        SourcesContaining(Suppressing)
            .Should()
            .BeEmpty(Because(
                rule: "une règle supprimée par attribut échappe à .editorconfig et au recensement",
                geste: "corriger le code, ou exempter la règle dans .editorconfig avec son pourquoi"
            ));

    [Test]
    public void Sources_ShouldConfineEveryPragmaToGeneratedMigrations_WhenTheSolutionIsScanned() =>
        SourcesContaining(LocalSuppression)
            .Where(path => !path.Contains(
                value: "Persistence/Migrations/",
                comparisonType: StringComparison.Ordinal
            ))
            .Should()
            .BeEmpty(Because(
                rule: "hors du code généré par EF, un avertissement supprimé sur place est un desserrage local invisible",
                geste: "corriger le code, ou exempter la règle dans .editorconfig avec son pourquoi"
            ));

    [Test]
    public void Projects_ShouldCarryNoWarningSuppression_WhenTheProjectFilesAreRead() =>
        ProjectFilesContaining(ProjectSuppression)
            .Should()
            .BeEmpty(Because(
                rule: "un projet qui se soustrait aux avertissements sort de la doctrine « build sans warning »",
                geste: "corriger le code, ou exempter la règle dans .editorconfig avec son pourquoi"
            ));

    [Test]
    public void Sources_ShouldCarryNoUnimplementedResidue_WhenTheSolutionIsScanned() =>
        SourcesContaining(Residue)
            .Should()
            .BeEmpty(Because(
                rule: "un squelette laissé derrière le RED est un cycle TDD interrompu (ADR 0032), et la suite ne le voit que si un test le traverse",
                geste: "écrire le corps que le test attend, ou supprimer le squelette"
            ));

    [Test]
    public void EditorConfig_ShouldDeclareTheFrozenSeverities_WhenItIsRead() =>
        DeclaredSeverities()
            .Should()
            .BeEquivalentTo(
                FrozenSeverities,
                because: Because(
                    rule: "une exemption d'analyseur, ou une élévation rabaissée, change ce que le build refuse",
                    geste: "éditer .editorconfig avec son pourquoi, puis QualityGateFreezeTest, puis docs/qualite.md"
                )
            );

    // Une sévérité de catégorie vise d'un coup toutes les règles d'une
    // famille ; la doctrine est règle par règle, avec son pourquoi.
    [Test]
    public void EditorConfig_ShouldCarryNoCategorySeverity_WhenItIsRead() =>
        File.ReadAllLines(EditorConfig)
            .Where(line => line.TrimStart().StartsWith(
                value: "dotnet_analyzer_diagnostic",
                comparisonType: StringComparison.Ordinal
            ))
            .Should()
            .BeEmpty(Because(
                rule: "une sévérité de catégorie désarme une famille entière en une ligne",
                geste: "exempter règle par règle dans .editorconfig, avec son pourquoi"
            ));

    [Test]
    public void CoverageSettings_ShouldDeclareTheFrozenExclusions_WhenTheyAreRead() =>
        DeclaredCoverageExclusions()
            .Should()
            .BeEquivalentTo(
                FrozenCoverageExclusions,
                because: Because(
                    rule: "une exclusion de plus déguiserait la carte de couverture (ADR 0029)",
                    geste: "éditer coverage.runsettings avec son pourquoi, puis QualityGateFreezeTest"
                )
            );

    [Test]
    public void BuildProperties_ShouldDeclareTheFrozenValues_WhenTheyAreRead() =>
        DeclaredBuildProperties()
            .Should()
            .BeEquivalentTo(
                FrozenBuildProperties,
                because: Because(
                    rule: "ces cinq propriétés décident de ce que le build refuse, et leur desserrage tient sur une ligne",
                    geste: "éditer Directory.Build.props avec son pourquoi, puis QualityGateFreezeTest"
                )
            );

    [Test]
    public void BannedSymbols_ShouldDeclareTheFrozenSymbols_WhenTheyAreRead() =>
        DeclaredBannedSymbols()
            .Should()
            .BeEquivalentTo(
                FrozenBannedSymbols,
                because: Because(
                    rule: "un symbole retiré rend l'horloge de nouveau lisible depuis le Domain (ADR 0024)",
                    geste: "éditer BannedSymbols.txt avec son pourquoi, puis QualityGateFreezeTest"
                )
            );

    [Test]
    public void QualityGates_ShouldDeclareTheFrozenTasks_WhenTheCheckTaskIsRead() =>
        DeclaredGates()
            .Should()
            .BeEquivalentTo(
                FrozenGates,
                because: Because(
                    rule: "une Porte retirée de `mise run check` cesse d'être rejouée sur le poste",
                    geste: "éditer mise.toml, puis QualityGateFreezeTest, puis .github/workflows/ci.yml"
                )
            );

    // Le pendant de la ligne « par construction » de la table Garde-fous de
    // l'ADR 0028 : elle devient un test.
    [TestCaseSource(nameof(CiGates))]
    public void QualityGates_ShouldHaveACiStep_WhenTheTaskIsDeclared(string task)
    {
        var command = task
            .Replace(
                oldValue: "//backend:",
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            )
            .Replace(
                oldValue: "//frontend:",
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            );

        File.ReadAllText(CiWorkflow).Should().Contain(
            expected: command,
            because: Because(
                rule: $"la Porte {task} doit avoir son step de CI — même commande sur le poste et sur le runner (ADR 0028)",
                geste: "éditer .github/workflows/ci.yml, puis mise.toml, puis QualityGateFreezeTest"
            )
        );
    }

    // La liste gelée vit en C# et docs/qualite.md la redit en prose : cette
    // assertion empêche la prose de mentir. Elle ne s'étend pas au reste du
    // Gel — énumérer les symboles bannis et les tâches dans la doc
    // reviendrait à recopier deux fichiers dans un troisième.
    [Test]
    public void Exemptions_ShouldBeDocumented_WhenTheFreezeIsRead()
    {
        var documentation = File.ReadAllText(QualityDoc);

        var undocumented = FrozenSeverities
            .Select(entry => entry.Split(' ')[1])
            .Distinct(StringComparer.Ordinal)
            .Where(code => !documentation.Contains(
                value: code,
                comparisonType: StringComparison.Ordinal
            ))
            .ToList();

        undocumented.Should().BeEmpty(Because(
            rule: "une exemption que docs/qualite.md ne nomme pas est une exemption dont personne ne rend compte",
            geste: "ajouter le code à la section « Les exemptions » de docs/qualite.md, avec son pourquoi"
        ));
    }

    private static string Because(
        string rule,
        string geste
    ) => $"{Freeze} — {rule} ; le geste : {geste}";

    // Ce fichier est scanné comme les autres : seules ses lignes marquées
    // sortent, pour que ses propres motifs ne se signalent pas eux-mêmes.
    private static IReadOnlyList<string> SourcesContaining(IReadOnlyCollection<string> needles) =>
        FilesContaining(
            paths: SourceTree.BackendSources("*.cs"),
            needles: needles
        );

    private static IReadOnlyList<string> ProjectFilesContaining(IReadOnlyCollection<string> needles) =>
        FilesContaining(
            paths: SourceTree.BackendSources("*.csproj")
                .Concat(SourceTree.BackendSources("*.props"))
                .Concat(SourceTree.BackendSources("*.targets")),
            needles: needles
        );

    private static IReadOnlyList<string> FilesContaining(
        IEnumerable<string> paths,
        IReadOnlyCollection<string> needles
    ) => paths
        .Where(path => File.ReadLines(path)
            .Where(line => !line.Contains(
                value: Marker,
                comparisonType: StringComparison.Ordinal
            ))
            .Any(line => needles.Any(needle => line.Contains(
                value: needle,
                comparisonType: StringComparison.Ordinal
            )))
        )
        .Select(Relative)
        .Order(StringComparer.Ordinal)
        .ToList();

    private static string Relative(string path) => Path
        .GetRelativePath(
            relativeTo: SourceTree.Root,
            path: path
        )
        .Replace(
            oldChar: Path.DirectorySeparatorChar,
            newChar: '/'
        );

    private static IReadOnlyList<string> DeclaredSeverities()
    {
        var section = string.Empty;
        var declared = new List<string>();

        foreach (var raw in File.ReadAllLines(EditorConfig)) {
            var line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']')) {
                section = line;
                continue;
            }

            if (!line.StartsWith(
                    value: "dotnet_diagnostic.",
                    comparisonType: StringComparison.Ordinal
                )) {
                continue;
            }

            var parts = line.Split(
                separator: '=',
                count: 2
            );
            var rule = parts[0].Trim()
                .Replace(
                    oldValue: "dotnet_diagnostic.",
                    newValue: string.Empty,
                    comparisonType: StringComparison.Ordinal
                )
                .Replace(
                    oldValue: ".severity",
                    newValue: string.Empty,
                    comparisonType: StringComparison.Ordinal
                );

            declared.Add($"{section} {rule} = {parts[1].Trim()}");
        }

        return declared;
    }

    private static IReadOnlyList<string> DeclaredCoverageExclusions()
    {
        var configuration = XDocument
            .Load(Path.Combine(
                path1: SourceTree.Backend,
                path2: "coverage.runsettings"
            ))
            .Descendants("Configuration")
            .Single();

        return CoverageExclusionElements
            .SelectMany(name => configuration
                .Elements(name)
                .SelectMany(element => element.Value.Split(','))
                .Select(value => $"{name} = {value.Trim()}")
            )
            .ToList();
    }

    private static IReadOnlyList<string> DeclaredBuildProperties()
    {
        var frozen = FrozenBuildProperties
            .Select(entry => entry.Split(' ')[0])
            .ToHashSet(StringComparer.Ordinal);

        return XDocument
            .Load(Path.Combine(
                path1: SourceTree.Backend,
                path2: "Directory.Build.props"
            ))
            .Descendants()
            .Where(element => frozen.Contains(element.Name.LocalName))
            .Select(element => $"{element.Name.LocalName} = {element.Value.Trim()}")
            .ToList();
    }

    private static IReadOnlyList<string> DeclaredBannedSymbols() => File
        .ReadAllLines(Path.Combine(
            path1: SourceTree.Backend,
            path2: "BannedSymbols.txt"
        ))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => line.Split(';')[0].Trim())
        .ToList();

    private static IReadOnlyList<string> DeclaredGates()
    {
        var inside = false;
        var gates = new List<string>();

        foreach (var raw in File.ReadAllLines(MiseToml)) {
            var line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']')) {
                inside = line == "[tasks.check]";
                continue;
            }

            if (inside && line.StartsWith('"')) {
                gates.Add(line.Split('"')[1]);
            }
        }

        return gates;
    }
}
