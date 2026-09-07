namespace LoreBank.SharedKernel.Application;

// Marque la propriété d'une commande que la route écrase : sur une route mixte
// (`POST {id}/deposits`), le controller réécrit `command with { AccountId =
// id }` et un champ posté dans le body est ignoré. Sans le marqueur, la
// Description OpenAPI déclarerait ce champ requis dans le body — le Client
// l'enverrait en double, l'une des deux valeurs gagnant en silence. Vit dans
// l'Application et non dans l'Api parce que la commande *est* le body (ADR
// 0012) : c'est elle qui sait lequel de ses champs vient de la route. Le
// marqueur ne change rien au binding — seule la Description le lit.
[AttributeUsage(AttributeTargets.Property)]
public sealed class RouteBoundAttribute : Attribute;
