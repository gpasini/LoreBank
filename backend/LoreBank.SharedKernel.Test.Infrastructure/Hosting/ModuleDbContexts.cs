using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Les DbContext d'un module vivent dans l'assembly de son module Autofac —
// la convention qui permet aux tests du socle de les retrouver sans élargir
// IHostModule à un besoin purement de test.
internal static class ModuleDbContexts
{
    public static IReadOnlyList<Type> Of(IHostModule module) => module.AutofacModule
        .GetType()
        .Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false } && type.IsAssignableTo(typeof(DbContext)))
        .ToList();
}
