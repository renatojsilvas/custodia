namespace Custodia.Infrastructure.Messaging;

public interface IIdentificadorDePassagem
{
    string GerarNovoId();
}

public sealed class IdentificadorDePassagemAleatorio : IIdentificadorDePassagem
{
    public string GerarNovoId() => Guid.NewGuid().ToString("N");
}
