using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

// Le piège d'aliasing des VO owned, épinglé sous son nom : une même instance
// de VO owned n'a droit qu'à un propriétaire — partagée entre deux, la
// persistance ne peut pas être fidèle. C'est la raison d'être de Money.Copy,
// que toute factory de VO composite doit employer quand elle stocke un VO
// reçu (voir JournalLine.Of, le consommateur de référence).
[TestFixture]
[TestOf(typeof(Money))]
public sealed class OwnedValueObjectAliasingTest
{
    private SqliteConnection _connection = null!;

    private TestTransferDbContext _context = null!;

    [SetUp]
    public async Task OpenConnection()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _context = new TestTransferDbContext(
            options: new DbContextOptionsBuilder<TestTransferDbContext>().UseSqlite(_connection).Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );
        await _context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task CloseConnection()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task SaveChangesAsync_ShouldNotWriteTheSecondOwner_WhenTheSameOwnedInstanceIsShared()
    {
        // Arrange — deux propriétaires du même Money : le piège.

        var shared = Money.Of(
            amount: 10m,
            currency: "EUR"
        );

        _context.Transfers.AddRange(
            new TestTransfer {
                Id = Guid.NewGuid(),
                Debit = shared,
                Credit = Money.Of(
                    amount: 1m,
                    currency: "EUR"
                ),
            },
            new TestTransfer {
                Id = Guid.NewGuid(),
                Debit = shared,
                Credit = Money.Of(
                    amount: 2m,
                    currency: "EUR"
                ),
            }
        );

        // Act

        var act = () => _context.SaveChangesAsync();

        // Assert — le change tracker n'accorde l'instance qu'au premier
        // propriétaire : les colonnes du second partent en NULL. La contrainte
        // NOT NULL du terrain rend l'échec bruyant ; sur une colonne nullable,
        // la même perte serait silencieuse — c'est le piège que Copy ferme.

        await act.Should()
            .ThrowAsync<DbUpdateException>()
            .WithInnerException<DbUpdateException, SqliteException>()
            .WithMessage("*NOT NULL*Debit_Amount*");
    }

    [Test]
    public async Task SaveChangesAsync_ShouldPersistBothOwners_WhenCopyGivesEachItsOwnInstance()
    {
        // Arrange — le remède : Copy donne à chaque propriétaire son instance.

        var shared = Money.Of(
            amount: 10m,
            currency: "EUR"
        );

        var first = new TestTransfer {
            Id = Guid.NewGuid(),
            Debit = shared.Copy(),
            Credit = Money.Of(
                amount: 1m,
                currency: "EUR"
            ),
        };
        var second = new TestTransfer {
            Id = Guid.NewGuid(),
            Debit = shared.Copy(),
            Credit = Money.Of(
                amount: 2m,
                currency: "EUR"
            ),
        };

        _context.Transfers.AddRange(
            first,
            second
        );

        await _context.SaveChangesAsync();

        // Act — relecture sur un contexte neuf : ce que la base a vraiment.

        await using var freshContext = new TestTransferDbContext(
            options: new DbContextOptionsBuilder<TestTransferDbContext>().UseSqlite(_connection).Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );

        var reloaded = await freshContext.Transfers.OrderBy(transfer => transfer.Credit.Amount).ToListAsync();

        // Assert

        reloaded.Should().HaveCount(2);
        reloaded[0].Debit.Should().Be(shared);
        reloaded[1].Debit.Should().Be(shared);
    }
}
