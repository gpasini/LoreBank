namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Reproduit le contrat que respecte un vrai conteneur : IEnumerable<T> se résout
// toujours, éventuellement vide, jamais null.
public sealed class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    public void Register<TService>(params TService[] instances) where TService : notnull
    {
        _services[typeof(IEnumerable<TService>)] = instances.Cast<object>().ToList();
    }

    // Une résolution par type concret (celle de GetRequiredService(Type)) : la
    // clé est le type exact de l'instance.
    public void RegisterInstance<TService>(TService instance) where TService : notnull
    {
        _services[typeof(TService)] = instance;
    }

    public object? GetService(Type serviceType) => _services.TryGetValue(
        key: serviceType,
        value: out var service
    )
        ? service
        : serviceType.IsGenericType
            ? Activator.CreateInstance(typeof(List<>).MakeGenericType(serviceType.GetGenericArguments()))
            : null;
}
