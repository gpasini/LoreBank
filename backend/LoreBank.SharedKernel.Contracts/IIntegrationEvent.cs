namespace LoreBank.SharedKernel.Contracts;

// Un fait métier qu'un module publie hors de ses frontières : type distinct
// du domain event qui l'origine, primitives plates, vivant dans l'assembly
// Contracts du module publieur — la seule surface qu'un autre module a le
// droit de référencer. Suffixé IntegrationEvent, porteur d'un
// [IntegrationEvent("<module>.<fait>")] : le discriminant stable qui
// l'identifie dans l'outbox.
public interface IIntegrationEvent;
