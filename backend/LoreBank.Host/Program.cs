using Autofac;
using Autofac.Extensions.DependencyInjection;
using LoreBank.Bank.Api.Controllers;
using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Infrastructure;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Api.Filters;
using LoreBank.SharedKernel.Api.Handlers;
using LoreBank.SharedKernel.Api.Validation;
using LoreBank.SharedKernel.Application.Behaviors;
using LoreBank.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container => {
        container.RegisterModule(new SharedKernelInfrastructureModule());
        container.RegisterModule(new BankInfrastructureModule());
    }
);

builder.Services
    .AddControllers(options => options.Filters.Add<DomainExceptionFilter>())
    .AddApplicationPart(typeof(BankAccountsController).Assembly);

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
        configuration.RegisterServicesFromAssembly(typeof(OpenBankAccountCommand).Assembly);
        configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
    }
);

builder.Services.AddDbContext<BankDbContext>(
    options => options.UseNpgsql(builder.Configuration.GetConnectionString("BankDb"))
);

var app = builder.Build();

// Volontairement inconditionnel : pas de page d'exception de développement. Le
// contrat HTTP est le même en dev et en prod, et la stack trace part dans les
// logs plutôt que dans la réponse.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment()) {
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<BankDbContext>().Database.Migrate();
}

app.MapControllers();

app.Run();
// Rend la classe Program générée par les top-level statements visible de WebApplicationFactory.
public partial class Program;
