using System.Reflection;

namespace Custodia.Architecture.Tests;

public sealed class DependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Custodia.Domain.Common.DomainErrors).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Custodia.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Custodia.Infrastructure.Persistence.AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNotReference_Application()
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != ApplicationAssembly.GetName().Name),
            "Domain must not depend on Application");
    }

    [Fact]
    public void Domain_ShouldNotReference_Infrastructure()
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != InfrastructureAssembly.GetName().Name),
            "Domain must not depend on Infrastructure");
    }

    [Fact]
    public void Domain_ShouldNotReference_Api()
    {
        var referencedAssemblies = DomainAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != ApiAssembly.GetName().Name),
            "Domain must not depend on API");
    }

    [Fact]
    public void Application_ShouldNotReference_Infrastructure()
    {
        var referencedAssemblies = ApplicationAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != InfrastructureAssembly.GetName().Name),
            "Application must not depend on Infrastructure");
    }

    [Fact]
    public void Application_ShouldNotReference_Api()
    {
        var referencedAssemblies = ApplicationAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != ApiAssembly.GetName().Name),
            "Application must not depend on API");
    }

    [Fact]
    public void Infrastructure_ShouldNotReference_Api()
    {
        var referencedAssemblies = InfrastructureAssembly.GetReferencedAssemblies();

        Assert.True(
            referencedAssemblies.All(a => a.Name != ApiAssembly.GetName().Name),
            "Infrastructure must not depend on API");
    }

    [Fact]
    public void GetReferencedAssemblies_DeveEnxergarReferenciasReais()
    {
        var applicationRefs = ApplicationAssembly.GetReferencedAssemblies();

        Assert.True(
            applicationRefs.Any(a => a.Name == DomainAssembly.GetName().Name),
            "Application referencia Domain de verdade (LoggingBehavior usa Custodia.Domain.Common.IResult). " +
            "Se esta asserção falhar, o mecanismo GetReferencedAssemblies() parou de enxergar referências " +
            "e TODOS os testes negativos deste arquivo viraram vacuidade. Nesta fase, Infrastructure não " +
            "implementa porta nenhuma do Application (não há repositório, não há IUnitOfWork — §F1), então " +
            "o ProjectReference Infrastructure→Application não existe no .csproj e não serve de controle " +
            "positivo aqui: um ProjectReference sem uso de tipo nenhum não sobrevive à emissão de metadados " +
            "do Roslyn, então usar essa referência como calibração testaria uma referência que sequer existe.");
    }
}
