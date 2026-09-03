global using FluentAssertions;
global using NUnit.Framework;

// Les hôtes de test hébergent des fakes singletons à état mutable, sans
// synchronisation : l'exécution en série n'est pas un hasard de configuration,
// c'est une hypothèse — on la déclare au runner plutôt que d'en hériter.
[assembly: Parallelizable(ParallelScope.None)]
