using LoreBank.Bank.Application.Queries.ListBankAccounts;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

// La base est partagée avec les fixtures qui écrivent pour de vrai
// (CqsContractTest) : la Liste peut contenir d'autres comptes que ceux
// arrangés ici, les assertions ciblent les nôtres — par la recherche sur
// l'IBAN, ou par l'ordre relatif. La mécanique de la Liste (bornes, facettes
// disjonctives, jokers) est prouvée par le socle (ListContractTest) ; ici,
// que le module l'emprunte vraiment.
public sealed class ListBankAccountsTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    private readonly static DateTimeOffset Instant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    [Test]
    public async Task ListBankAccounts_ShouldMapEveryColumn_WhenAccountsExist()
    {
        // Arrange

        Factory.TimeProvider.Instant = Instant;

        await DbSetup
            .CreateBankAccount(account => account.WithIban("FR7630006000011234567890189").WithCurrency("EUR"))
            .Deposit(deposit => deposit.WithAmount(42.50m))
            .RunAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        var page = await Sender.Send(new ListBankAccountsQuery { Search = "FR7630006000011234567890189" });

        // Assert

        var account = page.Items.Should().ContainSingle(item => item.Id == accountId.Value).Subject;

        account.Iban.Should().Be("FR7630006000011234567890189");
        account.Balance.Should().Be(42.50m);
        account.Currency.Should().Be("EUR");
        account.IsClosed.Should().BeFalse();
        account.OpenedAt.Should().Be(Instant);
    }

    // Deux scénarios, pour tenir l'id de chaque compte : un geste comble son
    // prérequis depuis le dernier créé, la clôture vise donc bien le second.
    [Test]
    public async Task ListBankAccounts_ShouldOrderByIban_AndReportClosures()
    {
        // Arrange

        await DbSetup.CreateBankAccount(account => account.WithIban("NL91ABNA0417164300")).RunAsync();

        var later = DbSetup.GetLastBankAccountId();

        await DbSetup.CreateBankAccount(account => account.WithIban("BE68539007547034")).Close().RunAsync();

        var earlier = DbSetup.GetLastBankAccountId();

        // Act

        var page = await Sender.Send(new ListBankAccountsQuery { PageSize = ListBankAccountsQuery.MaxPageSize });

        // Assert

        var ibans = page.Items.Select(item => item.Iban).ToList();

        ibans.IndexOf("BE68539007547034").Should().BeLessThan(ibans.IndexOf("NL91ABNA0417164300"));
        page.Items.Single(item => item.Id == earlier.Value).IsClosed.Should().BeTrue();
        page.Items.Single(item => item.Id == later.Value).IsClosed.Should().BeFalse();
    }

    // La Liste des comptes est recherchée sur l'IBAN et facettée sur la devise
    // et la clôture, sous le nom des filtres de la query : la recherche isole
    // le compte arrangé, les facettes le comptent seul.
    [Test]
    public async Task ListBankAccounts_ShouldSearchTheIban_AndFacetCurrencyAndClosure()
    {
        // Arrange

        await DbSetup
            .CreateBankAccount(account => account.WithIban("CH9300762011623852957").WithCurrency("EUR"))
            .RunAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        var page = await Sender.Send(new ListBankAccountsQuery { Search = "ch93007620116" });

        // Assert

        page.TotalCount.Should().Be(1);
        page.Items.Single().Id.Should().Be(accountId.Value);
        page.Facets.Select(facet => facet.Name).Should().Equal(
            "currency",
            "isClosed"
        );
        page.Facets.Single(facet => facet.Name == "currency").Values.Should().ContainSingle()
            .Which.Should().Be(new SharedKernel.Application.FacetValue(
                    Value: "EUR",
                    Count: 1
                )
            );
        page.Facets.Single(facet => facet.Name == "isClosed").Values.Should().ContainSingle()
            .Which.Should().Be(new SharedKernel.Application.FacetValue(
                    Value: "false",
                    Count: 1
                )
            );
    }

    [Test]
    public async Task ListBankAccounts_ShouldFilterByClosure_WhenTheFilterIsPosed()
    {
        // Arrange

        await DbSetup.CreateBankAccount(account => account.WithIban("AT611904300234573201")).Close().RunAsync();

        // Act

        var page = await Sender.Send(new ListBankAccountsQuery {
            Search = "AT611904300234573201",
            IsClosed = [false],
        }
        );

        // Assert

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    // La règle des bornes est celle du moteur : elle vaut par ISender comme
    // par HTTP.
    [Test]
    public async Task ListBankAccounts_ShouldThrowInvalidPaging_WhenThePageIsOutOfBounds()
    {
        var act = () => Sender.Send(new ListBankAccountsQuery { Page = 0 });

        await act.Should().ThrowAsync<InvalidPagingException>();
    }
}
