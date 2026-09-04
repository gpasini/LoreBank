using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

[TestFixture]
[TestOf(typeof(DataMigrations))]
public sealed class DataMigrationsTest
{
    [Test]
    public void IdOf_ShouldComposeTimestampAndClassName_WhenTheAttributeIsPresent()
    {
        DataMigrations
            .IdOf(typeof(StampedDataMigration))
            .Should()
            .Be("20260101000000_StampedDataMigration");
    }

    [Test]
    public void IdOf_ShouldThrow_WhenTheAttributeIsMissing()
    {
        var act = () => DataMigrations.IdOf(typeof(UnstampedDataMigration));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*UnstampedDataMigration*[DataMigration]*");
    }

    [Test]
    public void DiscoverIn_ShouldFindEveryConcreteDataMigration_WhenScanningAnAssembly()
    {
        var discovered = DataMigrations.DiscoverIn(typeof(StampedDataMigration).Assembly);

        discovered.Should().Contain(typeof(StampedDataMigration));
        discovered.Should().Contain(typeof(UnstampedDataMigration));
        discovered.Should().NotContain(typeof(DataMigration), "la base abstraite n'est pas une migration");
    }
}
