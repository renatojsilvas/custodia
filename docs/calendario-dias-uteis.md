# Calendário de dias úteis

## O que é

Tabela de configuração `calendario_dias_uteis` (coluna `data date PRIMARY KEY`), populada por
migration (`20260912100000_CriaCalendarioDiasUteis`). Contém **exatamente os dias úteis** —
segunda a sexta que não são feriado bancário nacional — dentro do horizonte semeado. Não é
projeção do livro (`movimentos`); não se reconstrói; diverge do critério de "dia com preço"
usado em `historico_precos`/`preco_atual` (§7.1 da arquitetura) de propósito.

"Próximo dia útil" é uma **leitura** (`MIN(data) WHERE data > @data`), nunca um cálculo
(`+1 dia corrido`, "pula fim de semana"). Feriado e fim de semana funcionam por construção —
a linha simplesmente não existe na tabela.

## Horizonte semeado

- **Início:** 2024-01-01 (o primeiro dia útil real dentro do horizonte é 2024-01-02, porque
  2024-01-01 é feriado — Confraternização Universal, numa segunda-feira).
- **Fim:** 2030-12-31 (dia útil, presente na tabela).
- **Linhas seguintes ao seed, conferidas rodando a migration contra Postgres 16 real:** 1754.

Quando o horizonte estiver acabando (guarda do item 6, `CalendarioDiasUteisHorizonteGuard`,
métrica `custodia_calendario_dias_uteis_horizonte_dias_restantes`, alerta configurável em
`CalendarioDiasUteis:AlertaHorizonteDiasMinimos`, default 90 dias), estenda por **nova
migration**, nunca editando a existente. A nova migration repete o mesmo padrão: `INSERT`
via `generate_series` filtrando fim de semana (`EXTRACT(ISODOW FROM dia) < 6`) e uma lista
literal de feriados dos anos novos — não calcule Páscoa em código (ver seção seguinte).

## Fonte dos feriados

Feriados nacionais bancários, no padrão do calendário ANBIMA
(`https://www.anbima.com.br/feriados/`), que coincide com o calendário de feriados nacionais
para os fixos e trata Carnaval (segunda e terça) como dias sem pregão.

**Fixos, todo ano:**

| Data | Feriado |
|---|---|
| 01/01 | Confraternização Universal |
| 21/04 | Tiradentes |
| 01/05 | Dia do Trabalho |
| 07/09 | Independência do Brasil |
| 12/10 | Nossa Senhora Aparecida |
| 02/11 | Finados |
| 15/11 | Proclamação da República |
| 20/11 | Dia Nacional de Zumbi e da Consciência Negra (feriado nacional a partir de 2024, Lei 14.759/2023) |
| 25/12 | Natal |

**Móveis (dependem da Páscoa), enumerados ano a ano — nunca calculados em código:**

| Ano | Carnaval (seg/ter) | Sexta-feira Santa | Corpus Christi |
|---|---|---|---|
| 2024 | 12/02, 13/02 | 29/03 | 30/05 |
| 2025 | 03/03, 04/03 | 18/04 | 19/06 |
| 2026 | 16/02, 17/02 | 03/04 | 04/06 |
| 2027 | 08/02, 09/02 | 26/03 | 27/05 |
| 2028 | 28/02, 29/02 | 14/04 | 15/06 |
| 2029 | 12/02, 13/02 | 30/03 | 31/05 |
| 2030 | 04/03, 05/03 | 19/04 | 20/06 |

Essas datas foram derivadas offline (algoritmo gregoriano padrão de cálculo da Páscoa,
aplicado por quem escreveu a migration, não pelo software em produção) e conferidas contra
os anos já públicos em calendários ANBIMA/bancários. **A migration em si só enumera datas
literais** — a §10 do `PADROES.md` proíbe calcular Páscoa dentro do código do repositório
justamente para manter essa lista auditável por inspeção, sem precisar confiar (ou reexecutar)
um algoritmo.

Ao estender o horizonte para 2031 em diante, calcule a Páscoa daquele ano por fonte externa
confiável (ANBIMA, ou o algoritmo gregoriano padrão executado fora do repo) e enumere as datas
resultantes na nova migration, do mesmo jeito.

## Por que não é ingestão

A Custódia não pode consultar a TD API nem o Hub para saber se um dia é útil — faria uma fonte
externa entrar no processamento de um evento já aceito, e a ADR-12 proíbe ler banco de outro
serviço. Por isso o calendário é dado de configuração deste banco, versionado em migration,
igual a qualquer outra constante de schema — não um job de ingestão com adapter de fonte
externa (que é o padrão usado em `hub-precos` para preços, e não se aplica aqui).

## Fuso de "hoje" na guarda de horizonte

A guarda do horizonte (`CalendarioDiasUteisHorizonteGuard`) calcula `diasRestantes = MAX(data)
- hoje`, e "hoje" é obtido pela **mesma consulta SQL** que a trigger
`movimentos_bloqueia_data_futura` usa — `(now() AT TIME ZONE 'America/Sao_Paulo')::date` — no
mesmo round-trip que busca `MAX(data)` (`ICalendarioDiasUteisReadRepository.ObterHorizonteAsync`).
Isso evita o defeito da §10.34 do `PADROES.md`: nenhum relógio do lado do .NET
(`DateTime.Today`, `DateTimeOffset.UtcNow.Date`) entra nessa conta.
