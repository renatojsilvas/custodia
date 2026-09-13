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
        var lote180 = new Lote(1m, 0m, Dia(0));
        var resultado180 = MotorTributosResgate.Calcular([lote180], 100m, Dia(180));

        var lote181 = new Lote(1m, 0m, Dia(0));
        var resultado181 = MotorTributosResgate.Calcular([lote181], 100m, Dia(181));

        Assert.Equal(0m, resultado180.Iof);
        Assert.Equal(22.5m, resultado180.Ir);

        Assert.Equal(0m, resultado181.Iof);
        Assert.Equal(20.0m, resultado181.Ir);
    }

    [Fact]
    public void Calcular_NoDia360ContraDia361_MudaDeFaixaDeVinteParaDezesseteEMeio()
    {
        var lote360 = new Lote(1m, 0m, Dia(0));
        var resultado360 = MotorTributosResgate.Calcular([lote360], 100m, Dia(360));

        var lote361 = new Lote(1m, 0m, Dia(0));
        var resultado361 = MotorTributosResgate.Calcular([lote361], 100m, Dia(361));

        Assert.Equal(20.0m, resultado360.Ir);
        Assert.Equal(17.5m, resultado361.Ir);
    }

    [Fact]
    public void Calcular_NoDia720ContraDia721_MudaDeFaixaDeDezesseteEMeioParaQuinze()
    {
        var lote720 = new Lote(1m, 0m, Dia(0));
        var resultado720 = MotorTributosResgate.Calcular([lote720], 100m, Dia(720));

        var lote721 = new Lote(1m, 0m, Dia(0));
        var resultado721 = MotorTributosResgate.Calcular([lote721], 100m, Dia(721));

        Assert.Equal(17.5m, resultado720.Ir);
        Assert.Equal(15.0m, resultado721.Ir);
    }

    [Fact]
    public void Calcular_NoDia29ContraDia30_IofDeixaDeExistirEmVezDeMudarDeAliquota()
    {
        var lote29 = new Lote(1m, 0m, Dia(0));
        var resultado29 = MotorTributosResgate.Calcular([lote29], 1000m, Dia(29));

        var lote30 = new Lote(1m, 0m, Dia(0));
        var resultado30 = MotorTributosResgate.Calcular([lote30], 1000m, Dia(30));

        Assert.Equal(30.00m, resultado29.Iof);
        Assert.Equal(218.25m, resultado29.Ir);

        Assert.Equal(0m, resultado30.Iof);
        Assert.Equal(225.00m, resultado30.Ir);
    }

    [Fact]
    public void Calcular_NoDiaZero_TambemNaoTemCelulaDeIof()
    {
        var loteMesmoDia = new Lote(1m, 0m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([loteMesmoDia], 1000m, Dia(0));

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ComMenosDeTrintaDias_BaseDoIrEhOGanhoMenosOIofENaoOGanhoBruto()
    {
        var lote = new Lote(1m, 0m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([lote], 1000m, Dia(10));

        Assert.Equal(660.00m, resultado.Iof);
        Assert.Equal(76.50m, resultado.Ir);
        Assert.NotEqual(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_VendaComUmLoteLucrativoEOutroComPrejuizo_TributaApenasOLoteLucrativoSemNetarComOPrejuizo()
    {
        var loteComLucro = new Lote(5m, 100m, Dia(0));
        var loteComPrejuizo = new Lote(5m, 300m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([loteComLucro, loteComPrejuizo], 2000m, Dia(50));

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(112.50m, resultado.Ir);
    }

    [Fact]
    public void Calcular_VendaAtravessandoDoisLotesEmFaixasDiferentes_SomaAsDuasAliquotasEmVezDeAplicarUmaSoAVendaInteira()
    {
        var loteAntigo = new Lote(5m, 100m, Dia(0));
        var loteRecente = new Lote(5m, 100m, Dia(900));

        var resultado = MotorTributosResgate.Calcular([loteAntigo, loteRecente], 2000m, Dia(1000));

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(187.50m, resultado.Ir);
        Assert.NotEqual(150.00m, resultado.Ir);
        Assert.NotEqual(225.00m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ComTodosOsLotesEmPrejuizo_IrEIofSaoZero_PoisASomaDasBasesPositivasEhZero()
    {
        var loteA = new Lote(5m, 200m, Dia(0));
        var loteB = new Lote(5m, 180m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([loteA, loteB], 1000m, Dia(10));

        Assert.Equal(0m, resultado.Iof);
        Assert.Equal(0m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ArredondaTributosParaDuasCasasSemArredondarQuantidade()
    {
        var lote = new Lote(1m, 0m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([lote], 12.34m, Dia(1));

        Assert.Equal(11.85m, resultado.Iof);
        Assert.Equal(0.11m, resultado.Ir);
    }

    [Fact]
    public void Ratear_ComTresLotesDeQuantidadeQueNaoDivideRedondo_SomaDosRateiosEhExatamenteOValorDaVenda()
    {
        var lotes = new List<Lote> { new(1m, 0m, Dia(0)), new(1m, 0m, Dia(0)), new(1m, 0m, Dia(0)) };
        var metodoRatear = typeof(MotorTributosResgate).GetMethod(
            "Ratear", BindingFlags.NonPublic | BindingFlags.Static)!;

        var rateios = (IReadOnlyList<decimal>)metodoRatear.Invoke(null, [100.00m, lotes, 3m])!;

        Assert.Equal(100.00m, rateios.Sum());

        var resultado = MotorTributosResgate.Calcular(lotes, 100.00m, Dia(50));
        Assert.Equal(22.50m, resultado.Ir);
    }

    [Fact]
    public void Calcular_ConferidoContraOSimuladorDoTesouroDireto_ComMenosDeTrintaDiasEGanhoDiferenteDoCusto()
    {
        var lote = new Lote(10m, 100m, Dia(0));

        var resultado = MotorTributosResgate.Calcular([lote], 1200m, Dia(15));

        Assert.Equal(100.00m, resultado.Iof);
        Assert.Equal(22.50m, resultado.Ir);
    }

    [Fact]
    public void AliquotaIr_ComDiasCorridosNegativo_LancaExcecaoPorNaoCasarNenhumaFaixa()
    {
        Assert.Throws<InvalidOperationException>(() => TabelaTributosResgate.AliquotaIr(-1));
    }

    [Fact]
    public void AliquotaIr_ComDiasCorridosAcimaDoTetoDaUltimaFaixa_LancaExcecaoPorNaoCasarNenhumaFaixa()
    {
        Assert.Throws<InvalidOperationException>(() => TabelaTributosResgate.AliquotaIr(1_000_000));
    }

    [Fact]
    public void Calcular_ComLoteDeAquisicaoPosteriorADataDoResgate_LancaExcecaoAoInvesDeDevolverZero()
    {
        var loteDeDataFutura = new Lote(1m, 0m, Dia(100));

        Assert.Throws<InvalidOperationException>(() => MotorTributosResgate.Calcular([loteDeDataFutura], 100m, Dia(0)));
    }
}
