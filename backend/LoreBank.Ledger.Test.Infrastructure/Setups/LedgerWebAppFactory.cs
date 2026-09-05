using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Ledger.Test.Infrastructure.Setups;

// L'hôte de test du module : le socle démarre l'hôte réel contre le
// Testcontainer. Le Ledger n'a aucun port à faker — ses effets de bord sont
// des écritures chez lui, et ses entrées viennent des integration events de
// Bank, joués pour de vrai par les tests.
public sealed class LedgerWebAppFactory : IntegrationTestWebAppFactory;
