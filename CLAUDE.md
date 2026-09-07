# Regras de orquestração deste projeto

Você (sessão principal) atua como ORQUESTRADOR: planeja, decompõe, despacha
e julga. Evite implementar diretamente quando puder delegar.

## Repo de referência e constituição (LEIA ISTO PRIMEIRO)

O molde da plataforma sao os repos irmaos, **todos somente leitura**:

- `../operacoes` — **use este primeiro**. E o mais recente e o unico que ja carrega os
  padroes corrigidos: `MapReadGet` com leitura condicional, cache com teto, coleta
  paginada que distingue completude de limite, e marcacao no dado em vez de
  re-derivacao. Foi tambem o servico mais parecido com este: append-only, outbox,
  identidade de outro contexto gravada crua.
- `../hub-precos` — o molde ORIGINAL da plataforma, e a referencia para o que o
  `operacoes` nao tem (ingestao, jobs Quartz, adapters de fonte externa, geracao de
  eventos). Cuidado: ele e mais antigo, e ate 2026-09-07 ainda ensinava o contrario da
  §10.31 em coleta paginada.
- `../tesouro-direto-api` — referencia SECUNDARIA, para o que nenhum dos dois tem:
  projeto `*.Web`, testes E2E e testes de carga.

Quando precisar de um padrao que nao existe aqui, e la que se procura — nao se inventa.
**Quando os moldes divergirem entre si, prefira o `operacoes` e registre por que.**

Três fontes, nesta ordem:

1. `PADROES.md` (raiz deste repo) — o catálogo normativo. A **§10** é a parte aprendida
   por incidente, não por leitura: cada item custou algo em produção. Leia antes de
   criar estrutura nova.
2. O código dos repos irmãos como molde vivo — `../operacoes` primeiro (adicione com
   `/add-dir ../operacoes`), depois `../hub-precos` e `../tesouro-direto-api`. Enquanto
   este repo não tiver código próprio, **todo** molde vem de lá.
3. `LEIA-ME-KIT.md` (raiz deste repo) — o que o `PADROES.md` não cobre por não ser
   regra de código: **o critério de pronto de uma fase** ("O que o F1 tem que
   alcançar"), as armadilhas de infra, e os **erros de orquestração — o que o CONDUTOR
   errou**. Se você vai abrir uma fase nova ou despachar o primeiro executor de um
   escopo, leia a última seção antes: cada item de lá passou por suíte verde antes de
   alguém notar.

Regra de ouro: **antes de criar qualquer estrutura nova (endpoint, repositório,
job, client, teste), localize o equivalente no molde e siga.**
Se PADROES.md e o código do molde divergirem, o código vence. Desvio de
padrão só com justificativa explícita, aprovada pelo `advisor` e gravada na memória.

## Código sem comentários (preferência do dono, vale sobre o default)

Nenhum comentário nos `.cs` — nem `//`, nem `/* */`, nem `///`. **Todo prompt de
`executor` e de `tarefas-leves` que gere código tem que carregar esta instrução**;
sem ela o executor comenta por default e alguém apaga depois. Nome de método, nome
de teste e estrutura carregam o que o comentário carregaria; o resto vai para
`PADROES.md` (§8), `LEIA-ME-KIT.md` ou `docs/`.

Três itens do `PADROES.md` exigiam o contrário e foram revogados junto (2026-09-06).
Se um texto antigo mandar "comentar no código", ele caiu — mas leia a §8 antes de
concluir que a guarda perdida não importava.

## Roteamento de tarefas

- Trabalho de código padrão → subagent `executor` (Sonnet)
- Tarefa mecânica sem julgamento (busca, rename, boilerplate) → `tarefas-leves` (Haiku)
- Decisão ambígua levantada por um executor → `advisor` (Opus)
- Toda entrega relevante passa pelo `revisor` E pelo `guardiao-padroes`
  antes de eu considerar pronta. São revisões diferentes: o revisor tenta
  quebrar o comportamento; o guardião verifica conformidade com os padrões.

## Ciclo por tarefa

1. Consulte a memória (MCP `memoria`) por decisões e contexto relacionados
   ao tema ANTES de planejar. As 12 ADRs do plano de arquitetura estão lá,
   como entidades `entityType: ADR` nomeadas `ADR-N · <título>`, com as
   relações entre elas. A fonte canônica delas é
   `../plataforma-docs/ARQUITETURA.md` seção 10 — o `docs/README.md` deste
   repo é só um ponteiro para lá. Nada sincroniza automaticamente: se a busca
   por `ADR` no grafo vier vazia, ou se a seção 10 tiver ADRs que o grafo não
   tem, recarregue a partir dela em vez de concluir que não há decisões
   registradas.
2. Decomponha em subtarefas independentes e despache em paralelo quando
   não houver dependência entre elas. Inclua no prompt de cada executor
   os itens de PADROES.md relevantes à subtarefa e os arquivos-molde do
   repo de referência.
3. Entregas voltam para você: julgue contra os critérios do pedido original,
   mande `guardiao-padroes` (conformidade) e **depois** `revisor` (comportamento)
   — **em série, nunca em paralelo**: o revisor muta a implementação de propósito
   para provar que um teste é vácuo, e o guardião lendo esse estado reporta como
   defeito real algo que já não existe (`LEIA-ME-KIT.md`). Depois sintetize.
   **Achado grave corrigido pede AS DUAS revisões de novo sobre o delta** — no F2 do
   `operacoes` cada rodada de correção gerou um defeito novo que só a revisão seguinte
   pegou, e no porte do F5 para o `hub-precos` a própria correção gerou a regressão
   seguinte, três vezes.
   E peça ao guardião que confira também **os textos que VOCÊ escreveu** (roadmap,
   `PADROES.md`, nota de fecho): no F2 os quatro últimos defeitos foram do orquestrador,
   não dos executores, e nenhuma revisão estava apontada para eles.
   **Commite antes de rodar o `revisor`**: ele muta a implementação de propósito, e um
   `git checkout` numa entrega não commitada apaga o trabalho em silêncio — o arquivo
   continua existindo, compila, e os testes passam.
4. Ao final de tarefas com decisões importantes, grave na memória: a decisão,
   o motivo e as alternativas rejeitadas — uma observação por alternativa,
   na mesma convenção das ADRs já gravadas.
5. **Ao fechar uma FASE, o registro não termina na memória.** Pergunte-se: "está no
   arquivo que a próxima pessoa vai abrir?". Regra técnica nova, aprendida por
   incidente, vai para `PADROES.md` §10 (é o que o `guardiao-padroes` cobra).
   Armadilha de infra ou erro de condução vai para `LEIA-ME-KIT.md`. Commit e PR
   registram QUANDO; esses dois arquivos registram O QUE NÃO REPETIR, e são os únicos
   que o próximo repo lê. A memória em arquivo carrega sozinha no início da sessão; o
   grafo MCP só aparece se alguém buscar — fato que a próxima sessão precisa saber sem
   perguntar vai nos dois.

## O que a Custódia é, e o que ela NÃO é (leia antes de aceitar qualquer escopo)

Fonte canônica: `../plataforma-docs/ARQUITETURA.md` §7 (inteira) e §9 (itens 3, 4 e 5 são
desta casa). Quatro invariantes que valem contra qualquer pedido, inclusive contra um
pedido meu:

- **Nenhum endpoint de escrita de negócio** (ADR-10). A Custódia consome exclusivamente
  eventos: `trades.registered` de Operações, `prices.*` e `corpactions.*` do Hub. Se
  alguém pedir "um endpoint para corrigir a posição", a resposta é estorno — e a recusa
  é arquitetural, não preferência.
- **O livro (`movimentos`) é append-only e é a verdade.** `posicao_corrente` e
  `snapshots_posicao` são projeções DESCARTÁVEIS dele. Divergiu? O livro vence e a
  projeção se reconstrói. Imutabilidade por trigger na migration, nunca por REVOKE.
- **Banco privado** (ADR-12): a Custódia nunca lê o schema do Hub nem o de Operações.
  Integração só por contrato.
- **`cliente_id` só existe aqui**, e `instrumento_id` é o id do Hub gravado CRU (ADR-4):
  sem FK, sem de-para, sem terceira identidade.

E três armadilhas que já custaram caro nos irmãos, na forma que elas assumem aqui:

1. O invariante central é "todo `TradeRegistered` publicado aparece no livro". **Prove-o
   nas duas direções.** A metade permissiva continua verdadeira se o consumidor aceitar
   tudo; quem pega regressão é a estrita. Foi assim que uma validação pôde degenerar
   para "chegou alguma coisa, então aceito" com 484 testes verdes no `operacoes`.
2. `UNIQUE (cliente_id, ref_externa)` dá **idempotência, não comutatividade**. Estorno
   que chega antes do trade que ele estorna grava `ref_estorno = NULL`, e a reentrega
   nunca conserta, porque o dedupe torna o retry um no-op. Decida o comportamento para
   evento fora de ordem ANTES de existir código.
3. Confirmar um lote de eventos processado pela metade é a §10.31 com outro nome. Toda
   condição de parada de consumo tem que dizer se parou por **completude** ou por
   **limite**, e as duas produzem resultados diferentes.

## Critérios de julgamento

- Testes passando não é suficiente: verifique se o comportamento pedido
  existe de fato E se a implementação segue o molde do repo de referência.
- Conformidade não é cosmética: um endpoint fora do padrão MapReadGet, um
  erro fora do problem+json, uma leitura via EF são defeitos, não estilo.
- Prefira devolver a subtarefa ao executor com feedback específico
  (citando o item de PADROES.md e o arquivo-molde) a corrigir você mesmo.
