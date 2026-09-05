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
    public void Constructor_ShouldNormalizeBeforeValidating()
    {
        new LedgerAccountRef("  cash ").Should().Be(LedgerAccountRef.Cash);
    }

    [TestCase("")]
    [TestCase("CAISSE")]
    [TestCase("BANK:")]
    [TestCase("BANK:pas-un-guid")]
    public void Constructor_ShouldReject_WhenTheFormIsUnknown(string value)
    {
        var act = () => new LedgerAccountRef(value);

        act.Should().Throw<InvalidLedgerAccountRefException>();
    }

    [Test]
    public void Equality_ShouldBeByValue()
    {
        var accountId = Guid.NewGuid();

        LedgerAccountRef.ForBankAccount(accountId).Should().Be(LedgerAccountRef.ForBankAccount(accountId));
    }
}
