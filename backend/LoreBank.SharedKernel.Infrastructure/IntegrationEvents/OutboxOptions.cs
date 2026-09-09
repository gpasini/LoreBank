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

    // Le bail de la Réservation (ADR 0021) : une passe s'approprie son lot
    // pour cette durée, hors verrou ; une instance qui disparaît rend ses
    // lignes à l'expiration. Doit couvrir le traitement d'un lot entier —
    // un bail expiré en cours de route rend la ligne à une autre instance,
    // doublon absorbé par l'inbox mais compté en tentative.
    public double ReservationSeconds { get; set; } = 300;

    // La Rétention (ADR 0021) : une ligne d'outbox livrée ou d'inbox traitée
    // plus vieille que ça est purgée. Une seule durée pour les deux tables —
    // l'inbox n'a de sens que tant que son outbox peut rejouer. Les lignes
    // en attente et les lignes poison ne sont jamais purgées. Pas d'opt-out :
    // la rétention infinie est le bug, pas une option.
    public int RetentionDays { get; set; } = 7;

    // La cadence de la purge, à part de celle du polling : une passe par
    // seconde livre, une passe par heure purge — dès le démarrage, puis à
    // l'intervalle.
    public double PurgeIntervalSeconds { get; set; } = 3600;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingSeconds);

    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);

    public TimeSpan PurgeInterval => TimeSpan.FromSeconds(PurgeIntervalSeconds);

    public TimeSpan ReservationDuration => TimeSpan.FromSeconds(ReservationSeconds);

    // Backoff exponentiel : 1er échec → BackoffSeconds, puis doublement à
    // chaque tentative — un consommateur en panne n'est pas martelé.
    public double BackoffDelaySecondsFor(int attempts) =>
        BackoffSeconds * Math.Pow(
            x: 2,
            y: attempts - 1
        );
}
