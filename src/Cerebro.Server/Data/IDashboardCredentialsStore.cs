namespace Cerebro.Server.Data;

public interface IDashboardCredentialsStore
{
    Task SetCredentialsAsync(string username, string password, CancellationToken cancellationToken);

    Task<bool> ValidateAsync(string username, string password, CancellationToken cancellationToken);

    Task<bool> HasCredentialsAsync(CancellationToken cancellationToken);
}
