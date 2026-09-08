using Custodia.API.Extensions;

namespace Custodia.API.Tests.Extensions;

public sealed class ConnectionStringGuardTests
{
    [Fact]
    public void Validate_Testing_WithNullConnectionString_ShouldNotThrow()
    {
        ConnectionStringGuard.Validate("Testing", null);
    }

    [Fact]
    public void Validate_Testing_WithEmptyConnectionString_ShouldNotThrow()
    {
        ConnectionStringGuard.Validate("Testing", "");
    }

    [Fact]
    public void Validate_Production_WithNullConnectionString_ShouldThrowWithConnectionStringNameInMessage()
    {
        var act = () => ConnectionStringGuard.Validate("Production", null);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_WithEmptyConnectionString_ShouldThrow()
    {
        var act = () => ConnectionStringGuard.Validate("Production", "");

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_WithoutUsername_ShouldThrowWithConnectionStringNameInMessage()
    {
        var act = () => ConnectionStringGuard.Validate("Production", "Host=localhost;Database=custodia;Password=senha-forte");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_WithoutPassword_ShouldThrowWithConnectionStringNameInMessage()
    {
        var act = () => ConnectionStringGuard.Validate("Production", "Host=localhost;Database=custodia;Username=custodia");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_WithoutUsernameAndPassword_ShouldThrow()
    {
        var act = () => ConnectionStringGuard.Validate("Production", "Host=localhost;Database=custodia");

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_WithMalformedConnectionString_ShouldThrowWithConnectionStringNameInMessage()
    {
        var act = () => ConnectionStringGuard.Validate("Production", "Host=localhost;Port=nao-e-um-numero");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_WithValidCredentialedConnectionString_ShouldNotThrow()
    {
        ConnectionStringGuard.Validate(
            "Production",
            "Host=localhost;Database=custodia;Username=custodia;Password=senha-forte");
    }
}
