namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Quand purger : à la première passe, puis dès qu'un intervalle s'est écoulé
// depuis la dernière purge due. Séparée du dispatcher pour se prouver sans
// hôte ni horloge — l'instant est passé en argument.
internal sealed class PurgeCadence(TimeSpan interval)
{
    private DateTimeOffset? _lastDue;

    public bool IsDue(DateTimeOffset now)
    {
        if (_lastDue is not null && now - _lastDue.Value < interval) {
            return false;
        }

        _lastDue = now;

        return true;
    }
}
