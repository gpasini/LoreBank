using Autofac;
using Autofac.Extensions.DependencyInjection;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Api.Filters;
using LoreBank.SharedKernel.Api.Handlers;
using LoreBank.SharedKernel.Api.OpenApi;
using LoreBank.SharedKernel.Api.Validation;
using LoreBank.SharedKernel.Application.Behaviors;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var modules = HostModules.All;

// `dotnet LoreBank.Host migrate` : même composition que l'API, mais le process
// migre puis sort sans servir de HTTP. Le verbe est retiré des args pour ne
// pas atteindre le binder de configuration.
var migrateOnly = args.Contains("migrate");

var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "migrate").ToArray());

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => {
        container.RegisterModule(new SharedKernelInfrastructureModule());

        foreach (var module in modules) {
            container.RegisterModule(module.AutofacModule);

            // Les IDomainEventHandler<> s'enregistrent ici et non dans chaque
            // Module Autofac : la ligne recopiée par module était oubliable, et
            // l'oubli silencieux — un event sans handler résolu ne signale rien.
            container
                .RegisterAssemblyTypes(module.DomainAssembly)
                .AsClosedTypesOf(typeof(IDomainEventHandler<>))
                .InstancePerLifetimeScope();

            // Le seam lui-même est exposé au conteneur : l'outbox (publisher et
            // dispatcher) retrouve le DbContext d'un module par son nom.
            container.RegisterInstance(module).As<IHostModule>();

            // Les handlers d'integration events, découverts comme les
            // IDomainEventHandler<> — et déclarés au dispatcher avec leur
            // module, celui dont l'inbox journalisera leurs traitements.
            foreach (var registration in IntegrationEventHandlers.DiscoverIn(module)) {
                container.RegisterInstance(registration);
                container.RegisterType(registration.HandlerType).AsSelf().InstancePerLifetimeScope();
            }
        }
    }
);

var mvc = builder.Services.AddControllers(options => options.Filters.Add<DomainExceptionFilter>());

foreach (var module in modules) {
    mvc.AddApplicationPart(module.ControllerAssembly);
}

// Le 400 automatique d'[ApiController] est le seul ProblemDetails que l'API ne
// fabrique pas elle-même : on le remplace pour qu'il ait la même forme que les
// autres.
builder.Services.Configure<ApiBehaviorOptions>(
    options => options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create
);

// AddProblemDetails ne sert que de filet à UseExceptionHandler, qui exige un
// IProblemDetailsService quand aucun chemin de repli n'est configuré ;
// UnhandledExceptionHandler écrit la réponse lui-même et ne le sollicite jamais.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

// La Description OpenAPI (ADR 0019) : convention et transformers du socle,
// codes d'erreur scannés sur le Domain et l'Application de chaque module
// monté (les NotFoundException vivent dans l'Application). Le
// service est inconditionnel — il n'a aucune surface réseau, et l'émission au
// build (backend/openapi/lorebank.json, via ApiDescription.Server) compose
// l'hôte hors Development. Seul l'endpoint est gardé, plus bas.
builder.Services.AddOpenApiDescription(
    title: "LoreBank",
    assemblies: modules.SelectMany(module => new[] { module.DomainAssembly, module.ApplicationAssembly })
);

builder.Services.AddMediatR(configuration => {
        configuration.RegisterServicesFromAssemblies(
            modules.Select(module => module.ApplicationAssembly).ToArray()
        );
        configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
    }
);

foreach (var module in modules) {
    module.ConfigureDbContext(
        services: builder.Services,
        configuration: builder.Configuration
    );
}

// La livraison des integration events : un seul dépileur pour toutes les
// outbox, cadencé par OutboxOptions. Le verbe migrate sort avant app.Run(),
// donc sans jamais démarrer le hosted service.
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.AddSingleton<OutboxProcessor>();
builder.Services.AddHostedService<OutboxDispatcher>();

var app = builder.Build();

// Le démarrage de l'API ne migre jamais — ni en dev ni ailleurs (ADR 0006) :
// le dev lance `mise run migrate`, et le harnais d'intégration appelle
// ModuleMigrator lui-même. Seul le verbe migre, et il ne sert pas de HTTP.
if (migrateOnly) {
    await ModuleMigrator.MigrateAsync(
        services: app.Services,
        modules: modules
    );

    return;
}

// Volontairement inconditionnel : pas de page d'exception de développement. Le
// contrat HTTP est le même en dev et en prod, et la stack trace part dans les
// logs plutôt que dans la réponse.
app.UseExceptionHandler();

app.MapControllers();

// /openapi/v1.json et /scalar, en Development seulement : la surface de prod
// ne publie pas sa propre Description — le front la lit dans le repo.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();
// Rend la classe Program générée par les top-level statements visible de WebApplicationFactory.
public partial class Program;
