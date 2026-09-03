using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Les données de test se créent via les vrais use cases : le DbSetup d'un
// module dérive cette base et ajoute une classe partielle par agrégat
// (CreateXxxAsync(), GetLastXxxId()) qui passe par Sender.
public abstract class DbSetupBase(IServiceProvider serviceProvider)
{
    protected ISender Sender => serviceProvider.GetRequiredService<ISender>();
}
