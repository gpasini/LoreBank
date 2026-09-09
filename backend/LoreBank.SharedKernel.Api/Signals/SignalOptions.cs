namespace LoreBank.SharedKernel.Api.Signals;

// Les réglages du flux de Signaux (ADR 0026), section « Signals » de la
// configuration de l'hôte. Le keep-alive est un commentaire SSE écrit à
// intervalle fixe quand rien ne passe : c'est ce qui empêche un proxy de
// fermer une connexion qu'il croit inactive. Le défaut suffit en
// production ; le harnais le resserre pour l'observer.
public sealed class SignalOptions
{
    public const string SectionName = "Signals";

    public double KeepAliveSeconds { get; set; } = 15;

    public TimeSpan KeepAlive => TimeSpan.FromSeconds(KeepAliveSeconds);
}
