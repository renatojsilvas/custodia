using System.Globalization;
using System.Text;

namespace Custodia.Infrastructure.Messaging;

public static class RabbitMqCabecalhos
{
    public const string Motivo = "x-custodia-motivo";
    public const string Voltas = "x-custodia-voltas";
    public const string PassagemId = "x-custodia-passagem-id";
    public const string XAcquiredCount = "x-acquired-count";

    public static Dictionary<string, object?> Copiar(IDictionary<string, object?>? origem)
    {
        var copia = new Dictionary<string, object?>();

        if (origem is null)
        {
            return copia;
        }

        foreach (var par in origem)
        {
            if (par.Value is not null)
            {
                copia[par.Key] = par.Value;
            }
        }

        return copia;
    }

    public static void DefinirMotivo(IDictionary<string, object?> cabecalhos, string motivo) =>
        cabecalhos[Motivo] = motivo;

    public static void DefinirPassagemId(IDictionary<string, object?> cabecalhos, string passagemId) =>
        cabecalhos[PassagemId] = passagemId;

    public static string? LerMotivo(IDictionary<string, object?>? cabecalhos) => LerTexto(cabecalhos, Motivo);

    public static string? LerPassagemId(IDictionary<string, object?>? cabecalhos) => LerTexto(cabecalhos, PassagemId);

    public static string? LerTexto(IDictionary<string, object?>? cabecalhos, string chave)
    {
        if (cabecalhos is null || !cabecalhos.TryGetValue(chave, out var bruto) || bruto is null)
        {
            return null;
        }

        return bruto switch
        {
            string texto => texto,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => bruto.ToString(),
        };
    }

    public static long LerVoltas(IDictionary<string, object?>? cabecalhos)
    {
        if (cabecalhos is null || !cabecalhos.TryGetValue(Voltas, out var bruto))
        {
            return 0;
        }

        return ParaLong(bruto);
    }

    public static long IncrementarVoltas(IDictionary<string, object?> cabecalhos)
    {
        var proximaVolta = LerVoltas(cabecalhos) + 1;
        cabecalhos[Voltas] = proximaVolta;
        return proximaVolta;
    }

    public static long ContarTentativasDeEntrega(IDictionary<string, object?>? cabecalhos)
    {
        if (cabecalhos is null || !cabecalhos.TryGetValue(XAcquiredCount, out var bruto))
        {
            return 0;
        }

        return ParaLong(bruto);
    }

    private static long ParaLong(object? valor) => valor switch
    {
        null => 0,
        long l => l,
        int i => i,
        short s => s,
        byte b => b,
        sbyte sb => sb,
        uint ui => ui,
        ulong ul => (long)ul,
        _ => Convert.ToInt64(valor, CultureInfo.InvariantCulture),
    };
}
