using Custodia.API.Extensions;

namespace Custodia.API.Tests.Extensions;

public sealed class RabbitMqConfigGuardTests
{
    [Fact]
    public void Validate_Testing_NuncaLanca_MesmoComTudoAusente()
    {
        var exception = Record.Exception(() => RabbitMqConfigGuard.Validate("Testing", null, null, null));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Validate_ForaDeTesting_ComHostAusente_Lanca(string environmentName)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMqConfigGuard.Validate(environmentName, null, "user", "senha"));

        Assert.Contains("RabbitMq:Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForaDeTesting_ComHostEmBranco_Lanca()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMqConfigGuard.Validate("Production", "   ", "user", "senha"));

        Assert.Contains("RabbitMq:Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForaDeTesting_ComUserAusente_Lanca()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMqConfigGuard.Validate("Production", "host", null, "senha"));

        Assert.Contains("RabbitMq:User", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForaDeTesting_ComPasswordAusente_Lanca()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => RabbitMqConfigGuard.Validate("Production", "host", "user", null));

        Assert.Contains("RabbitMq:Password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ForaDeTesting_ComTudoPreenchido_NaoLanca()
    {
        var exception = Record.Exception(() => RabbitMqConfigGuard.Validate("Production", "host", "user", "senha"));

        Assert.Null(exception);
    }
}
