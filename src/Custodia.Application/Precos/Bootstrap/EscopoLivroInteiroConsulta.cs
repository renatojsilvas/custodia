namespace Custodia.Application.Precos.Bootstrap;

public sealed record EscopoLivroInteiroConsulta(IReadOnlyList<string> InstrumentosId, DateOnly? MenorDataEvento);
