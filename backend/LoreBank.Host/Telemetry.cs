using System.Diagnostics;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LoreBank.Host;

// La Télémétrie (ADR 0025) : ce que l'hôte laisse sortir vers un collecteur.
// Le socle et les modules posent Activity et Meter en BCL, sans fournisseur ;
// c'est ici, et ici seulement, qu'OpenTelemetry est référencé — un cloneur
// qui n'en veut pas retire ce fichier, son appel et trois pins. Le SDK est
// toujours monté (sources et meters déclarés, le harnais y branche son
// exporteur mémoire) ; l'exporteur OTLP seulement si l'endpoint standard est
// configuré — sans lui rien ne sort, rien n'est tenté, rien n'est loggé. Les
// clés sont les OTEL_* de la spécification, que le SDK lit lui-même dans la
// configuration de l'hôte (variable d'environnement, ou racine d'appsettings).
// Le verbe migrate construit le même hôte : avec un endpoint, ses requêtes
// SQL partent aussi en trace — court, et il sort tout de suite après.
public static class Telemetry
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public const string ServiceNameKey = "OTEL_SERVICE_NAME";

    public const string DefaultServiceName = "LoreBank";

    public static bool ExportsTo(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[EndpointKey]);

    public static void AddTelemetry(this WebApplicationBuilder builder)
    {
        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceNameFrom(builder.Configuration)))
            .WithTracing(tracing => tracing
                // Gardée bien que le span serveur soit natif : en .NET 10 il
                // n'a ni statut ni route (natifs en .NET 11 seulement).
                .AddAspNetCoreInstrumentation()
                // Npgsql et HttpClient exposent leur source nativement.
                .AddSource("Npgsql")
                .AddSource("System.Net.Http")
                .AddSource(OutboxTracing.SourceName)
                .AddProcessor<RootSqlSpanFilter>()
            )
            .WithMetrics(metrics => metrics
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddMeter("System.Net.Http")
                .AddMeter("Npgsql")
                .AddMeter(OutboxMetrics.MeterName)
            );

        if (ExportsTo(builder.Configuration)) {
            telemetry.UseOtlpExporter();
        }
    }

    // Le tick n'est pas un fait d'intérêt (ADR 0025) : chaque passe de
    // l'outbox réserve et mesure en SQL, hors de toute requête et de tout
    // handler. Sans ce filtre, chaque seconde déposerait ses traces racines
    // « postgresql » par module dans le collecteur. Un span SQL n'est exporté
    // que sous un parent — requête HTTP, livraison, ou toute activité qu'un
    // cloneur ouvrira lui-même.
    private sealed class RootSqlSpanFilter : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.ParentId is null && data.Source.Name == "Npgsql") {
                data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }
    }

    // Un défaut lisible, la surcharge standard : OTEL_SERVICE_NAME prime,
    // relu ici parce qu'AddService écraserait sinon ce que le détecteur du
    // SDK a lu.
    private static string ServiceNameFrom(IConfiguration configuration) =>
        configuration[ServiceNameKey] is { Length: > 0 } configured ? configured : DefaultServiceName;
}
