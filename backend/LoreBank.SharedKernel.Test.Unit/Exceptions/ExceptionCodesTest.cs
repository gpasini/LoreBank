using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.Exceptions;

// Le nom d'une exception EST son contrat public (Code en est dérivé) : ces
// tests épinglent les codes publiés pour qu'un renommage fasse échouer la
// suite plutôt que de livrer silencieusement une rupture de contrat.
[TestFixture]
[TestOf(typeof(DomainException))]
public sealed class ExceptionCodesTest
{
    [Test]
    public void Code_ShouldBeInvalidIban_WhenExceptionIsInvalidIbanException()
    {
        new InvalidIbanException("FR76").Code.Should().Be("INVALID_IBAN");
    }

    [Test]
    public void Code_ShouldBeInvalidBic_WhenExceptionIsInvalidBicException()
    {
        new InvalidBicException("BNPAFRPP").Code.Should().Be("INVALID_BIC");
    }

    [Test]
    public void Code_ShouldBeInvalidCurrency_WhenExceptionIsInvalidCurrencyException()
    {
        new InvalidCurrencyException("EUR").Code.Should().Be("INVALID_CURRENCY");
    }

    [Test]
    public void Code_ShouldBeNonPositiveAmount_WhenExceptionIsNonPositiveAmountException()
    {
        var exception = new NonPositiveAmountException(
            amount: -100m,
            currency: "EUR"
        );

        exception.Code.Should().Be("NON_POSITIVE_AMOUNT");
    }

    [Test]
    public void Code_ShouldBeInvalidSignalResource_WhenExceptionIsInvalidSignalResourceException()
    {
        new InvalidSignalResourceException("nope").Code.Should().Be("INVALID_SIGNAL_RESOURCE");
    }

    [Test]
    public void Code_ShouldBeCurrencyMismatch_WhenExceptionIsCurrencyMismatchException()
    {
        var exception = new CurrencyMismatchException(
            left: "EUR",
            right: "USD"
        );

        exception.Code.Should().Be("CURRENCY_MISMATCH");
    }
}
