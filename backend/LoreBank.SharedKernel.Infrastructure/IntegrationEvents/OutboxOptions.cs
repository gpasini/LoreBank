namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Les réglages de la livraison : cadence de polling, retries et backoff.
// Liés à la section « IntegrationEvents » de la configuration de l'hôte ;
// les défauts suffisent en production, le harnais de test les resserre pour
// piloter le processor déterministiquement.
public sealed class OutboxOptions
{
    public const string SectionName = "IntegrationEvents";

    public double PollingSeconds { get; set; } = 1;

    // Après ce nombre d'échecs, la ligne est marquée poison et sort de la
    // file : un event irrécupérable ne doit pas bloquer ceux d'après — il
    // reste visible en base, avec sa dernière erreur, pour un humain.
    public int MaxAttempts { get; set; } = 5;

    public double BackoffSeconds { get; set; } = 2;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingSeconds);

    // Backoff exponentiel : 1er échec → BackoffSeconds, puis doublement à
    // chaque tentative — un consommateur en panne n'est pas martelé.
    public double BackoffDelaySecondsFor(int attempts) =>
        BackoffSeconds * Math.Pow(
            x: 2,
            y: attempts - 1
        );
}
