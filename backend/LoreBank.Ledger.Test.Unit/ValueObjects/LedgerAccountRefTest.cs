using LoreBank.Ledger.Domain.Exceptions;
using LoreBank.Ledger.Domain.ValueObjects;

namespace LoreBank.Ledger.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(LedgerAccountRef))]
public sealed class LedgerAccountRefTest
{
    [Test]
    public void Cash_ShouldBeTheTechnicalCashAccount()
    {
        LedgerAccountRef.Cash.Value.Should().Be("CASH");
        LedgerAccountRef.Cash.IsBankAccount.Should().BeFalse();
    }

    [Test]
    public void ForBankAccount_ShouldBuildTheCanonicalUppercaseForm()
    {
        var accountId = Guid.Parse("6b29fc40-ca47-1067-b31d-00dd010662da");

        var reference = LedgerAccountRef.ForBankAccount(accountId);

        reference.Value.Should().Be("BANK:6B29FC40-CA47-1067-B31D-00DD010662DA");
        reference.IsBankAccount.Should().BeTrue();
    }

    [Test]
    public void Parse_ShouldNormalizeBeforeValidating()
    {
        LedgerAccountRef.Parse("  cash ").Should().Be(LedgerAccountRef.Cash);
    }

    [TestCase("")]
    [TestCase("CAISSE")]
    [TestCase("BANK:")]
    [TestCase("BANK:pas-un-guid")]
    public void Parse_ShouldReject_WhenTheFormIsUnknown(string value)
    {
        var act = () => LedgerAccountRef.Parse(value);

        act.Should().Throw<InvalidLedgerAccountRefException>();
    }

    // La réhydratation truste la base (ADR 0016) : la valeur stockée est
    // reprise telle quelle, même si elle ne passerait plus Parse.
    [Test]
    public void Hydrate_ShouldAcceptTheStoredValueAsIs()
    {
        LedgerAccountRef.Hydrate("CAISSE").Value.Should().Be("CAISSE");
    }

    [Test]
    public void Equality_ShouldBeByValue()
    {
        var accountId = Guid.NewGuid();

        LedgerAccountRef.ForBankAccount(accountId).Should().Be(LedgerAccountRef.ForBankAccount(accountId));
    }
}
