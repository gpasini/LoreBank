using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

[TestFixture]
[TestOf(typeof(MigrationTimeline))]
public sealed class MigrationTimelineTest
{
    [Test]
    public void Merge_ShouldInterleaveDataBetweenSchemaSteps_WhenTheirTimestampsFallBetween()
    {
        // Le scénario canonique de l'ADR 0013 : ajouter (schéma), backfiller
        // (données), resserrer (schéma) — l'ordre est celui des timestamps,
        // pas celui des flux d'origine.
        var timeline = MigrationTimeline.Merge(
            schemaMigrationIds: ["20260101000000_AddColumns", "20260301000000_TightenColumns"],
            dataMigrationIds: ["20260201000000_Backfill"]
        );

        timeline.Should().Equal(
            new MigrationStep(
                Id: "20260101000000_AddColumns",
                IsData: false
            ),
            new MigrationStep(
                Id: "20260201000000_Backfill",
                IsData: true
            ),
            new MigrationStep(
                Id: "20260301000000_TightenColumns",
                IsData: false
            )
        );
    }

    [Test]
    public void Merge_ShouldSortOrdinally_WhenIdsShareATimestamp()
    {
        // Comparaison ordinale, pas de règle spéciale : à timestamp égal,
        // c'est le suffixe qui départage — un cas à éviter en pratique, mais
        // le comportement doit être déterministe.
        var timeline = MigrationTimeline.Merge(
            schemaMigrationIds: ["20260101000000_Schema"],
            dataMigrationIds: ["20260101000000_Backfill"]
        );

        timeline.Select(step => step.Id).Should().Equal(
            "20260101000000_Backfill",
            "20260101000000_Schema"
        );
    }

    [Test]
    public void Merge_ShouldReturnSchemaOnly_WhenNoDataMigrationIsPending()
    {
        var timeline = MigrationTimeline.Merge(
            schemaMigrationIds: ["20260101000000_Init"],
            dataMigrationIds: []
        );

        timeline.Should().ContainSingle().Which.IsData.Should().BeFalse();
    }
}
