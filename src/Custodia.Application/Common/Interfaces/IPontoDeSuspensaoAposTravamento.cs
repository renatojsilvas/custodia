namespace Custodia.Application.Common.Interfaces;

public interface IPontoDeSuspensaoAposTravamento
{
    Task AposTravarAsync(string clienteId, string refExterna, CancellationToken ct);
}
