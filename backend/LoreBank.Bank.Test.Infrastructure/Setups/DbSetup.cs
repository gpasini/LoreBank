using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public sealed partial class DbSetup(IServiceProvider serviceProvider) : DbSetupBase(serviceProvider);
