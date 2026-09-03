using Autofac;
using Autofac.Extensions.DependencyInjection;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Api.Filters;
using LoreBank.SharedKernel.Api.Handlers;
using LoreBank.SharedKernel.Api.Validation;
using LoreBank.SharedKernel.Application.Behaviors;
using LoreBank.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var modules = HostModules.All;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => {
        container.RegisterModule(new SharedKernelInfrastructureModule());

        foreach (var module in modules) {
            container.RegisterModule(module.AutofacModule);
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

var app = builder.Build();

// Volontairement inconditionnel : pas de page d'exception de développement. Le
// contrat HTTP est le même en dev et en prod, et la stack trace part dans les
// logs plutôt que dans la réponse.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment()) {
    using var scope = app.Services.CreateScope();

    foreach (var module in modules) {
        await module.MigrateAsync(scope.ServiceProvider);
    }
}

app.MapControllers();

app.Run();
// Rend la classe Program générée par les top-level statements visible de WebApplicationFactory.
public partial class Program;
