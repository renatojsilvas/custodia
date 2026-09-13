using System.Reflection;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tributos;

namespace Custodia.Domain.Tests.Tributos;

public sealed class MotorTributosResgateTests
{
    private static DateOnly Dia(int offset) => new DateOnly(2026, 1, 1).AddDays(offset);

    [Fact]
    public void Calcular_NoDia180ContraDia181_MudaDeFaixaDe225ParaVinteAntesDeQualquerIof()
    {
        var lote180 = new LoteConsumido(1m, 0m, Dia(0), 180);
        var resultado180 = MotorTributosResgate.Calcular([lote180], 100m);

        var lote181 = new LoteConsumido(1m, 0m, Dia(0), 181);
        var resultado181 = MotorTributosResgate.Calcular([lote181], 100m);

        Assert.Equal(0m, resultado180.Iof);
        Assert.Equal(22.5m, resultado180.Ir);

        Assert.Equal(0m, resultado181.Iof);
        Assert.Equal(20.0m, resultado181.Ir);
    }

    [Fact]
    public void Calcular_NoDia360ContraDia361_MudaDeFaixaDeVinteParaDezesseteEMeio()
    {
        var lote360 = new LoteConsumido(1m, 0m, Dia(0), 360);
        var resultado360 = MotorTributosResgate.Calcular([lote360], 100m);

        var lote361 = new LoteConsumido(1m, 0m, Dia(0), 361);
        var resultado361 = MotorTributosResgate.Calcular([lote361], 100m);

        Assert.Equal(20.0m, resultado360.Ir);
        Assert.Equal(17.5m, resultado361.Ir);
    }

    [Fact]
    public void Calcular_NoDia720ContraDia721_MudaDeFaixaDeDezesseteEMeioParaQuinze()
    {
        var lote720 = new LoteConsumido(1m, 0m, Dia(0), 720);
        var resultado720 = MotorTributosResgate.Calcular([lote720], 100m);

        var lote721 = new LoteConsumido(1m, 0m, Dia(0), 721);
        var resultado721 = MotorTributosResgate.Calcular([lote721], 100m);

        Assert.Equal(17.5m, resultado720.Ir);
        Assert.Equal(15.0m, resultado721.Ir);
    }

    [Fact]
    public void Calcular_NoDia29ContraDia30_IofDeixaDeExistirEmVezDeMudarDeAliquota()
    {
        var lote29 = new LoteConsumido(1m, 0m, Dia(0), 29);
        var resultado29 = MotorTributosResgate.Calcular([lote29], 1000m);

        var lote30 = new LoteConsumido(1m, 0m, Dia(0), 30);
        var resultado30 = MotorTributosResgate.Calcular([lote30], 1000m);

        Assert.Equal(30.00m, resultado29.Iof);
        Assert.Equal(218.25m, resultado29.Ir);

        Assert.Equal(0m, resultado30.Iof);
        Assert.Equal(225.00m, resultado30.Ir);
    }

    [Fact]
    public void Calcular_NoDiaZero_TambemNaoTemCelulaDeIof()
    {
        var loteMesmoDia = new LoteConsumido(1m, 0m, Dia(0), 0);

        var resultado = MotorTributosResgate.Calcular([loteMesmoDia], 1000m);

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ComMenosDeTrintaDias_BaseDoIrEhOGanhoMenosOIofENaoOGanhoBruto()
    {
        var lote = new LoteConsumido(1m, 0m, Dia(0), 10);

        var resultado = MotorTributosResgate.Calcular([lote], 1000m);

        Assert.Equal(660.00m, resultado.Iof);
        Assert.Equal(76.50m, resultado.Ir);
        Assert.NotEqual(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_VendaComUmLoteLucrativoEOutroComPrejuizo_TributaApenasOLoteLucrativoSemNetarComOPrejuizo()
    {
        var loteComLucro = new LoteConsumido(5m, 100m, Dia(0), 50);
        var loteComPrejuizo = new LoteConsumido(5m, 300m, Dia(0), 50);

        var resultado = MotorTributosResgate.Calcular([loteComLucro, loteComPrejuizo], 2000m);

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(112.50m, resultado.Ir);
    }

    [Fact]
    public void Calcular_VendaAtravessandoDoisLotesEmFaixasDiferentes_SomaAsDuasAliquotasEmVezDeAplicarUmaSoAVendaInteira()
    {
        var loteAntigo = new LoteConsumido(5m, 100m, Dia(0), 1000);
        var loteRecente = new LoteConsumido(5m, 100m, Dia(900), 100);

        var resultado = MotorTributosResgate.Calcular([loteAntigo, loteRecente], 2000m);

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(187.50m, resultado.Ir);
        Assert.NotEqual(150.00m, resultado.Ir);
        Assert.NotEqual(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ComTodosOsLotesEmPrejuizo_IrEIofSaoZero_PoisASomaDasBasesPositivasEhZero()
    {
        var loteA = new LoteConsumido(5m, 200m, Dia(0), 10);
        var loteB = new LoteConsumido(5m, 180m, Dia(0), 10);

        var resultado = MotorTributosResgate.Calcular([loteA, loteB], 1000m);

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(0m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ArredondaTributosParaDuasCasasSemArredondarQuantidade()
    {
        var lote = new LoteConsumido(1m, 0m, Dia(0), 1);

        var resultado = MotorTributosResgate.Calcular([lote], 12.34m);

        Assert.Equal(11.85m, resultado.Iof);
        Assert.Equal(0.11m, resultado.Ir);
    }

    [Fact]
    public void Ratear_ComDoisLotesDeQuantidadeFracionaria_RateiaPelaQuantidadeExataSemArredondarAQuantidade()
    {
        var loteMenor = new LoteConsumido(0.37m, 0m, Dia(0), 50);
        var loteMaior = new LoteConsumido(1.63m, 0m, Dia(0), 50);
        var lotes = new List<LoteConsumido> { loteMenor, loteMaior };
        var metodoRatear = typeof(MotorTributosResgate).GetMethod(
            "Ratear", BindingFlags.NonPublic | BindingFlags.Static)!;

        var rateios = (IReadOnlyList<decimal>)metodoRatear.Invoke(null, [250.00m, lotes, 2.00m])!;

        Assert.Equal(46.25m, rateios[0]);
        Assert.Equal(203.75m, rateios[1]);
        Assert.Equal(250.00m, rateios.Sum());
        Assert.Equal(0.37m, loteMenor.Quantidade);
        Assert.Equal(1.63m, loteMaior.Quantidade);
    }

    [Fact]
    public void Ratear_ComTresLotesDeQuantidadeQueNaoDivideRedondo_SomaDosRateiosEhExatamenteOValorDaVenda()
    {
        var lotes = new List<LoteConsumido>
        {
            new(1m, 0m, Dia(0), 50), new(1m, 0m, Dia(0), 50), new(1m, 0m, Dia(0), 50),
        };
        var metodoRatear = typeof(MotorTributosResgate).GetMethod(
            "Ratear", BindingFlags.NonPublic | BindingFlags.Static)!;

        var rateios = (IReadOnlyList<decimal>)metodoRatear.Invoke(null, [100.00m, lotes, 3m])!;

        Assert.Equal(100.00m, rateios.Sum());

        var resultado = MotorTributosResgate.Calcular(lotes, 100.00m);
        Assert.Equal(22.50m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ConferidoContraOSimuladorDoTesouroDireto_ComMenosDeTrintaDiasEGanhoDiferenteDoCusto()
    {
        var lote = new LoteConsumido(10m, 100m, Dia(0), 15);

        var resultado = MotorTributosResgate.Calcular([lote], 1200m);

        Assert.Equal(100.00m, resultado.Iof);
        Assert.Equal(22.50m, resultado.Ir);
    }

    [Fact]
    public void AliquotaIr_ComDiasCorridosAcimaDaAntigaFaixaSentinela_PermaneceEmQuinzePorCentoPoisAUltimaFaixaEhAbertaParaCima()
    {
        Assert.Equal(15m, TabelaTributosResgate.AliquotaIr(1_000_000));
    }

    [Fact]
    public void LoteConsumido_ComPrazoNegativo_NaoPodeSerConstruido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoteConsumido(1m, 0m, Dia(100), -1));
    }
}
