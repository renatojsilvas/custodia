# ROADMAP do custodia

Fila de tarefas, uma por vez.

**Como usar:** abra este arquivo, copie o bloco **Prompt** do próximo `F` não marcado
e cole na sessão. O texto acima do prompt é briefing para você, não para colar — o
prompt já carrega o que o orquestrador precisa. Ao aceitar a entrega, rode o critério
de **Pronto**, marque o checkbox e commite. O arquivo é a fonte, não o que estiver no
contexto de alguma sessão.

Arquitetura: `../plataforma-docs/ARQUITETURA.md` (§7 inteira é desta casa).
**Molde primário: `../operacoes`** — é o mais recente e o único que já carrega os
padrões corrigidos, e foi o serviço mais parecido com este (append-only, trigger de
imutabilidade, identidade de outro contexto gravada crua). Para o que o `operacoes` não
tem — jobs, `IHostedService`, adapters de fonte externa, GET condicional, coleta em laço
contra serviço externo — o molde é `../hub-precos`. (Coleta **em laço**, não "paginada":
o `GET /prices/asof` do Hub **não é paginado**, e o F6 registra quais são os laços reais.)
Para projeto `*.Web`, E2E e teste de carga,
`../tesouro-direto-api`. Quando os moldes divergirem, prefere-se o `operacoes` e
registra-se por quê. Ver `PADROES.md`, `LEIA-ME-KIT.md` e `CLAUDE.md`.

**Aviso que vale para todas as fases:** não existe consumidor de RabbitMQ em nenhum dos
três moldes — o Hub e o Operações **publicam**. O molde vale para conexão, configuração,
serialização e conversão de exceção em `Result`; o laço de consumo, o ack manual e a
política de mensagem-veneno são código novo, sem molde vivo. Onde a §5 e o `PADROES` §5
não decidirem, a decisão é desta casa e vai escrita aqui.

---

## O que este roadmap entrega — os itens 3, 4 e 5 da §9

| Item da §9 | Fases aqui | Critério de pronto do item |
|---|---|---|
| **3.** Operações + Custódia: livro e projeções | **F2** (topologia), **F3** (schema), **F4** (`TradeRegistered` → livro e posição), **F5** (consequências contábeis do resgate), **F6** (`PriceObserved` + bootstrap REST) | "aplicação registrada via Operações aparecendo no livro por evento; preço do dia chegando por push" — fecha no **F6** |
| **4.** Custódia: snapshots e recálculo | **F7** (`eod.ready`, snapshots, worker §7.4, reconciliação), **F8** (extratos §7.5) | "fluxo 2 retroativo e fluxo 5 funcionando de ponta a ponta" — o motor fecha no **F7**, o item inteiro no **F8** |
| **5.** Corpactions | **F9** | um cupom virando N movimentos, um conjunto por cliente posicionado na data |
| — | **F1** | não pertence a item nenhum da §9: é a esteira de entrega, que a §9 pressupõe existir (`LEIA-ME-KIT`) |
| **1.** Hub mínimo (schema §4.1, TD Adapter, `GET /prices/asof`) | **nenhuma — é do `../hub-precos`, e já está feito** | o `asof` que o F6 consome já responde; nada aqui o entrega |
| **2.** RabbitMQ + relay | **nenhuma — é do `../hub-precos`, e já está feito** | o exchange `prices` e o relay já existem; o F2 declara a **fila do consumidor**, que é outra coisa |

**Os itens 1 e 2 da §9 não têm fase aqui porque já estão prontos no repo do Hub**, e o
**Fluxo 0 da §8** (discovery + backfill, "Rabbit permanece mudo") é dele pelo mesmo motivo.
Estão nomeados acima para a cobertura §8/§9 fechar item a item — ausência sem explicação é
lacuna, ausência explicada é escopo.

**A metade "Operações" do item 3 já está fechada**, no repo irmão: o `F5` de lá fechou em
2026-09-07, e o `POST /v1/operacoes` publica `trades.registered` em **produção** desde
2026-09-06. Consequência prática, e ela decide a ordem das fases: o critério de pronto do
item 3 só é verificável **com os dois serviços no ar**, e a janela de perda já está aberta.

O item 6 da §9 (fase 2: Yahoo/MB, `POST /instruments`, bindings novos) está fora deste
roadmap por decisão da própria §9.

**O F5 não é nomeado pela §9.** Ele vem da §7.3 (ramo `se resgate`) e do Fluxo 3 (§8.4),
que a §9 dobra dentro do item 3 sem citar. Fase derivada de outra seção, registrada como
tal — mesmo caso do F2 do `operacoes`, cujas tabelas o `ARQUITETURA` não especificava.

---

## Cobertura item a item da §7, da §8 e da §9 — o índice deste arquivo

A tabela acima mapeia os **itens da §9**. Esta mapeia o **texto da §7 e os fluxos da §8**,
peça por peça, e existe para uma pergunta só: *tem alguma coisa escrita na arquitetura que
nenhuma fase entrega?* Linha sem fase é lacuna; linha com fase é promessa conferível.

| Peça da `ARQUITETURA` | Fase que entrega | Onde ela é provada |
|---|---|---|
| §7.1 tabela `movimentos` (livro append-only + trigger) | F3 | Pronto (a) do F3 |
| §7.1 tabela `posicao_corrente` | F3 (DDL) · F4 (as três colunas) | I4, Pronto do F4 |
| §7.1 tabela `preco_atual` | F3 (DDL) · F6 (escritor) | Pronto (a) e (c) do F6 |
| §7.1 tabela `historico_precos` ("histórico local recomendado") | F3 (DDL) · F6 (escritor) | Pronto (a) do F6 |
| §7.1 tabela `snapshots_posicao` (versionada) | F3 (DDL) · F7 (escritor) | Pronto (d) e (e) do F7 |
| §7.1 convenção 1 — **caixa é instrumento** (`caixa:BRL`, `caixa:a_liquidar`) | F3 (V4) · F5 (escritura o limbo) · F7 (põe no snapshot) · F8 (mostra) | Pronto (n) do F3, (f) do F5, (j) do F7, (g) do F8 |
| §7.1 convenção 2 — **só há snapshot em dia com posição ≠ 0**, sem misturar com "repete o preço" | F7 | Pronto (b) e (k) do F7 |
| §7.1 convenção 3 — **tributos são movimentos próprios**, nunca desconto embutido | F3 (V1/V2) · F5 (grava) | Pronto (a) do F5 |
| §7.2 identidade e onboarding (resolução nome→id) | **nenhuma, e é decisão: acontece na borda, em Operações (ADR-10)** | F1 apaga a promessa herdada; F6 registra a negação |
| §7.3 ramo `TradeRegistered` (dedupe, livro, posição) | F4 | invariante I3, nas duas direções |
| §7.3 sub-ramo `se resgate` (IR/IOF, `a_liquidar` → `liquidacao`) | F5 | Pronto (a)–(h) do F5 |
| §7.3 ramo `PriceObserved` (upsert + `revisao > 0`) | F6 (upsert, marcação) · F7 (o `recalcular` que a marcação dispara) | Pronto (c) do F6; Fluxo 5 sintético no F7 |
| §7.3 ramo `CorporateActionObserved` | F9 | Pronto do F9 |
| §7.3 ramo `EodPricesReady(D)` (um batch, todos os clientes) — **entregue com desvio rotulado**: o handler materializa o intervalo `[U_anterior + 1, D]`, e não só o `D` do singular da §7.3 | F7 | Pronto (permissiva) e (m) do F7 |
| §7.4 gatilho 1 — movimento com `data_evento` no passado | F7 (o F4 só registra e mede) | Fluxo 2 retroativo, Pronto do F7 |
| §7.4 gatilho 2 — `PriceObserved` com `revisao > 0` | F7 (marcação vem do F6) | Fluxo 5 **por injeção sintética** (§13 item 5) |
| §7.4 gatilho 3 — corpaction atrasada | F9, **sem código novo**: cai no gatilho 1 | Pronto (e) do F9 |
| §7.5 extrato de **movimentação** (`SELECT` do livro) | F8 | Pronto (permissiva) do F8 |
| §7.5 extrato de **posição** (`snapshots_posicao WHERE vigente`) | F8 (dado gerado no F7) | Pronto (g) do F8 |
| §8.1 Fluxo 0 — discovery + backfill | **do `../hub-precos`, já feito** | fora deste roadmap |
| §8.2 Fluxo 1 — ciclo diário | F6 (preço por push) · F7 (o batch de snapshots) | Pronto do F6 e do F7 |
| §8.3 Fluxo 2 — aplicação normal ou retroativa | F4 (normal) · F7 (retroativa) | critério do item 3 e do item 4 da §9 |
| §8.4 Fluxo 3 — resgate + liquidação D+1 | F5 | Pronto (c) e (f) do F5 |
| §8.5 Fluxo 4 — corpactions TD | F9 | Pronto do F9 |
| §8.6 Fluxo 5 — correção retroativa de preço | F7 | injeção sintética, por §13 item 5 |

---

## Por que esta ordem: reversibilidade, não dependência

As fases estão em ordem **decrescente do custo de errar**, medido por uma pergunta só:
*o que for gravado (ou perdido) errado até a próxima fase tem conserto?*

1. **Perda irrecuperável FORA do nosso banco** → **F2**. Evento publicado em exchange
   topic sem binding casando é descartado **em silêncio**, com o produtor marcando
   sucesso e a outbox gravando `publicado_em` (`LEIA-ME-KIT`, "Perder o volume do
   broker"; o container do broker foi recriado em 2026-09-05 10:21 e levou a topologia
   durável junto com o volume). A assimetria que decide tudo: **preço** perdido volta
   pelo `GET {hub}/prices/asof` — a ADR-1 diz explicitamente que replay não é requisito
   do broker porque a canônica é o arquivo; **`trades.registered` perdido NÃO tem
   caminho de recuperação por contrato**, porque Operações não expõe leitura de
   operações (só o autocomplete de instrumentos, §6). **Precisão que a versão anterior
   deste parágrafo não tinha:** existe *um* caminho, e ele não é ilegítimo — um comando
   de republicação da outbox **no repo de origem**, executado pelo dono do dado, é tão
   legítimo quanto a dependência que o F9 declara do `../hub-precos`; o que a ADR-12
   proíbe é *nós* mexermos no banco deles. O que não existe é o caminho **por contrato**,
   e o comando de republicação **não existe hoje** no `../operacoes`: enquanto ninguém o
   escrever, a perda é definitiva na prática. Isso não enfraquece a prioridade do F2 —
   reforça-a, porque a alternativa é abrir uma fase em outro repo.
2. **Dado irreparável DENTRO do nosso banco** → **F3** e **F4**. Linha errada em tabela
   append-only com trigger de imutabilidade. `PADROES` §10.21.
3. **Projeção descartável** → do **F5** em diante. Errar custa um reprocessamento; a
   partir daí a ordem passa a ser a dependência natural da §9.

O **F1** vem antes de tudo por uma razão diferente e não negociável: sem a esteira, cada
problema de infraestrutura chega misturado com problema de código, e ninguém sabe qual
está olhando (`LEIA-ME-KIT`, "Deixar o deploy para depois de existir código").

---

## Dependência externa por fase

Cada fase declara, no cabeçalho, a dependência **nova** que ela passa a exigir do mundo
fora deste repo. Serve para saber, antes de abrir o prompt, se a fase pode travar por
terceiro.

| Fase | Dependência externa nova |
|---|---|
| F1 | nenhuma (VPS, CI, Grafana Cloud — já existem) |
| F2 | broker `plataforma-rabbitmq` alcançável (serviço do `hub-precos`) |
| F3 | Postgres com schema (a instância já existe) — **e a decisão da PENDÊNCIA BLOQUEANTE do `caixa:BRL`, que é um campo opcional novo na §5.1 do `../plataforma-docs`, OUTRO repo** (ver a pendência dentro da V2) |
| F4 | o `operacoes` publicando `trades.registered` — **já publica** |
| F5 | nenhuma |
| F6 | o Hub publicando `prices.*` e respondendo `GET /prices/asof` |
| F7 | o Hub publicando `eod.ready` |
| F8 | nenhuma |
| F9 | o Hub publicando `corpactions.*` — **exige código no `../hub-precos`, outro repo** |

---

## Inventário de invariantes — onde cada um nasce e onde é provado

Todo bloco **Pronto** deste arquivo enuncia a metade **ESTRITA** ("o que NÃO pode
acontecer") além da permissiva. Não é zelo: a metade permissiva de um invariante de
equivalência continua verdadeira se o consumidor aceitar tudo, e foi assim que uma
validação degenerou para "chegou alguma coisa, então aceito" com **484 testes verdes**
no `operacoes` (`LEIA-ME-KIT`, "Especificar só a metade permissiva").

| # | Invariante | Nasce em |
|---|---|---|
| I1 | `movimentos` é append-only (UPDATE e DELETE bloqueados por trigger) | F3 |
| I2 | dedupe por `UNIQUE (cliente_id, ref_externa)`, com `ref_externa` **NOT NULL** e derivada por MOVIMENTO | F3 / F4 |
| I3 | `TradeRegistered` ↔ movimento, nas duas direções | F4 |
| I4 | `posicao_corrente` é dobra pura do livro nas **três** colunas: Σ `qtd_delta` = `quantidade`, e `preco_medio`/`custo_total` reproduzidos pela mesma dobra, **percorrida em `data_evento`, desempate `registrado_em`, desempate final `id`** (a média ponderada é sensível à ordem, então a ordem é parte da definição, e é por isso que o F4 só aplica delta quando `data_evento ≥ MAX(data_evento)` da chave) — **sem filtro por relógio**, que é o caso **`D = ∞`** do **parâmetro de corte** da dobra (o snapshot do F7 é a MESMA dobra com `D = o dia`, e a posição na data do F9 também). A dobra é a da **V1 do F3**, escrita **por tipo, para os dez**, com a **regra de fronteira** (`preco_medio` só definido para `quantidade > 0`): regra que cobre dois tipos deixa oito colunas sem definição com a suíte verde, e regra que cobre dez tipos e nenhum valor de fronteira divide por zero | F3 (a regra) · F4 (a aplicação) |
| I5 | `eod.ready` é o **único** gatilho da valoração **DIÁRIA** (ADR-9, §7.3) — o qualificador é a metade do invariante: a **exceção nomeada** é o gatilho 2 da §7.4 (`PriceObserved` com `revisao > 0`). O mecanismo que faz as duas coisas conviverem é **recorte de intervalo**: o worker percorre `[desde, U]` — com **`U` = o último dia JÁ MATERIALIZADO** (`MAX(data_ref)` de `eod_processado`), **nunca uma data de relógio**, porque o `D` do `eod.ready(D)` é derivado do dado e não de `hoje` — e **insere quando difere do vigente OU quando não existe linha**. **`U` nulo tem DUAS regras, uma por consumidor da tabela, e trocá-las é o defeito:** para o **worker** `U` é limite SUPERIOR, então `U` nulo ⇒ intervalo **vazio** e o worker não faz nada (quem repara é o `eod.ready` seguinte); para o **handler** de `eod.ready` o `U_anterior` nulo é limite INFERIOR, e o intervalo começa em `MIN(data_evento)` do livro, **com teto declarado**. Dia `> U` fica fora do intervalo e é materializado pelo `eod.ready` que vier a fechá-lo, **já com o movimento no livro**: a dicotomia `≤ U` / `> U` é exaustiva e **nenhum dia ANUNCIADO fica sem dono** — o dia que o Hub nunca anunciar não é coberto por ela, e quem o detecta é o alerta das 12:00 sobre `eod_processado.processado_em`. **E a entrega dos gatilhos 1 e 2 é DURÁVEL POR VARREDURA, não por fila em memória:** a *varredura de defasagem de snapshot* do F7 refaz o que um crash entre o ack do evento e o recálculo teria perdido para sempre. Ela **não é um quarto gatilho** e não fura o "único" acima, porque chama o **mesmo** `recalcular` com o mesmo recorte `[desde, U]` — é o meio pelo qual os gatilhos 1 e 2 chegam ao destino, não um gatilho novo. É **desvio parcial da §7.4**, que escreve `[desde, hoje]`, e está rotulado no F7 com os dois fatos medidos; a própria tabela `eod_processado` é outro desvio rotulado (ADR-5 e §9 do `PADROES`) | F7 |
| I6 | só existe linha de snapshot em dia útil com posição ≠ 0 | F7 |
| I7 | versionar preserva a versão anterior legível (`calculado_em`) | F7 |
| I8 | `recalcular` é determinístico e idempotente (rodar duas vezes não cria terceira versão) | F7 |
| I9 | tributo é movimento próprio; o valor do fato é **bruto**, nunca líquido embutido | F5 |
| I10 | caixa é instrumento; o limbo D→D+1 aparece honestamente em dois dias | F5 |
| I11 | `instrumento_id` é o id do Hub gravado cru; `cliente_id` só existe aqui; sem FK e sem de-para (ADR-4) | F3 |
| I12 | banco privado: exatamente **uma** connection string, para o próprio banco (ADR-12) | F1 / F3 |
| I13 | **a chave de dedupe só se consome quando o movimento gravado está CORRETO E COMPLETO** | F3 (regra) · F4, F5, F6, F9 (aplicação) |
| I14 | fila e bindings existem **ANTES** da primeira publicação | F2 |
| I15 | toda condição de parada de laço declara **completude** ou **limite**, e as duas produzem resultados diferentes (§10.31); onde os dois rótulos não bastarem, o rótulo que falta se nomeia em vez de virar sucesso por omissão — no drenador do F4 são **seis**, e os três que faltavam (`VAZIO_DO_MOTIVO`, `PARCIAL` e `INTERROMPIDA`) apareceram por escrever o **procedimento de decisão** pergunta a pergunta — inclusive a pergunta *"por que a passagem não examinou as `N`?"*, que é a que faltava por último —, não por declarar a lista exaustiva | F2 (verificação do deploy), F4 (consumo e drenador), F5 (job de liquidação), F6 (os dois laços de coleta), F7 (batch do `eod.ready`, worker, **comando `materializar --desde --ate`** e **varredura de defasagem de snapshot**), F8 (cursor com clamp), F9 (fan-out por cliente) — **toda fase com laço, e a lista é a prova disso** |
| I16 | não existe verbo de escrita sob `/v1` (ADR-10), provado por teste e não por prosa | F8 |
| I17 | `caixa:BRL` e `caixa:a_liquidar` são ids **locais** desta casa, com preço **1,000000 por definição** — nunca recebem `PriceObserved`, nunca são pedidos ao Hub, nunca entram no alerta de preço ausente | F3 (regra) · F6, F7 (aplicação) |
| I18 | a Custódia não publica **nenhum evento de contrato da §5.1** e não tem outbox nem relay; o único tráfego AMQP que **a aplicação** emite é infraestrutura interna dela (`custodia.retry.in`, `custodia.parking`, e o que dead-letra para a `custodia.prices.dlq`), que não é evento de domínio. **Ressalva nomeada, para o invariante não ser lido como falso:** o *passo de deploy* do F2 publica `prices.smoke` no exchange `prices`, que é do Hub — é prova de fumaça de topologia, executada por script, e não a aplicação | F1 (texto) · F2 (topologia) · F4 (uso) |
| I19 | uma linha do livro é revertida **no máximo uma vez** (`UNIQUE (ref_estorno) WHERE ref_estorno IS NOT NULL`), e estorno **de** estorno continua possível | F3 |

**I13 é a regra de governo deste roadmap**, e ela decide sozinha os casos que este
arquivo não previu. Onde uma fase abre exceção — o F4 grava o fato do resgate antes de o
F5 saber escriturar as consequências — a exceção está **justificada dentro da fase**, não
tolerada em silêncio.

---

## As quatro decisões, e a fase que torna cada uma real

**A · Evento fora de ordem.** Parte-se em duas, e a divisão é o achado: a metade
**irreversível** vai para o **F3** (schema), porque é lá que ela deixa de ser reversível;
a metade **comportamental** vai para o **F4** (consumidor).

- F3: CHECK tornando `ref_estorno` obrigatório quando `tipo = 'ajuste'` e NULL nos demais
  tipos, mais FK composta com `cliente_id` e CHECK de não-auto-referência. Um estorno
  órfão fica **impossível de nascer**, pelo banco e não pela disciplina do handler.
  *Rejeitado:* aceitar `ref_estorno = NULL` e preencher depois — o UPDATE que isso exige
  é barrado pela trigger, e o `UNIQUE (cliente_id, ref_externa)` torna a reentrega um
  no-op, então a linha apontaria para o nada **para sempre**.
- F4: estorno órfão **não grava nada e não consome a chave de dedupe** (I13) → é
  **republicado pelo exchange `custodia.retry.in`, que entrega na `custodia.retry`** (fila
  com `x-message-ttl` e DLX de volta para a
  `custodia.prices`, declarada no F2) e **confirmado na fila principal**. *Publica-se
  sempre em **exchange**, nunca direto numa fila: a default exchange com routing key =
  nome da fila foi **rejeitada nominalmente** no F2, e escrever "republicado na
  `custodia.retry`" sem dizer por onde é o que convida a usá-la.* O atraso é
  real, a cabeça da fila é liberada, e a mensagem volta sozinha depois do TTL.
  *O motivo tem nome, e não é "backoff": é **head-of-line blocking**.* Com **uma** fila,
  **um** consumidor, `prefetchCount: 1` e consumo serial, `nack(requeue: true)` devolve a
  mensagem para a frente da fila e a reentrega imediatamente — e **o trade original que
  curaria a condição está ATRÁS dela, na MESMA fila, e nunca é entregue**. A justificativa
  "a condição se cura em segundos" seria falsa por construção nesse desenho: sem consumir
  o que vem depois, nada cura. Pior, `basic.nack` não tem atraso, então "com backoff" só
  seria implementável com `sleep` antes do nack — que trava **todo** o consumo, e é o laço
  quente com prefetch 1 num host de um núcleo que a Decisão C proíbe (§10.13); e sem
  atraso o `x-delivery-limit` queima em milissegundos e a mensagem cai na DLQ, que é o
  "DLQ na primeira tentativa" **rejeitado** logo abaixo. Requeue puro não é uma opção
  neste desenho, é o desfecho rejeitado com outro nome.
  *O que sustenta RETRY e não DLQ direto:* fora de ordem **aqui** é artefato de
  concorrência/redelivery, não de causalidade — em Operações a FK composta
  `(estorna_operacao_id, cliente_id, instrumento_id)` **impede** registrar um estorno
  antes do original, e o relay de lá publica em ordem de `id` aguardando cada confirm um
  a um (§10.26); com a cabeça da fila liberada, o original é processado no próprio ciclo
  e a volta do órfão encontra o livro já curado.
  *Teto, e ele é o modo de falha certo:* a contagem de voltas sai do header `x-death` que
  o **próprio broker** escreve ao dead-letrar (§10.32 — a distinção vem marcada, não
  re-derivada); estourado o teto, a mensagem vai para a `custodia.parked` com o motivo
  `estorno_orfao_expirado` e **alerta**, nunca para a DLQ. Estacionar visivelmente é o
  que a §10.26 chama de modo de falha certo em at-least-once; a DLQ deste roadmap não tem
  história de dreno e viraria buraco.
  *Duas condições sem as quais nada disso funciona, e as duas são de uma linha de código:*
  (i) **o republish na `custodia.retry.in` copia INTEGRALMENTE os headers da mensagem
  recebida** — o `x-death` é escrito pelo broker ao dead-letrar, mas o que o consumidor faz
  é um **publish novo**, e publish novo só carrega o que o publicador setar; sem a cópia o
  contador zera a cada volta, o teto de 10 nunca fecha, a mensagem circula a cada 30 s para
  sempre e o `estorno_orfao_expirado` **nunca é emitido** — laço infinito com todos os
  testes verdes; (ii) **a ordem é publica → espera o publisher confirm → só então ack**.
  `trades.registered` não tem caminho de recuperação por contrato: ack antes do confirm é
  **perda definitiva e silenciosa**, o modo de falha exato que o F2 existe para impedir.
  Confirm negado ou timeout ⇒ `nack(requeue: true)` da original — aqui o requeue é o certo,
  porque a alternativa é perder. **E o confirm negado PERSISTENTE tem desfecho próprio, sem
  o qual esta regra reproduz os dois desfechos que este arquivo rejeita.** Com uma fila, um
  consumidor, prefetch 1 e consumo serial, o `nack(requeue: true)` reentrega **na hora**;
  se a causa do confirm negado não for transitória — o caso previsto é a `custodia.retry`
  ter nascido com `x-overflow: reject-publish` (saída (i) do F2) e estar cheia —, o
  resultado seria `nack → reentrega → publish → reject → nack` sem atraso nenhum, que é o
  **laço quente** da §10.13, e o `x-delivery-limit` queimaria em milissegundos levando a
  mensagem para a `custodia.prices.dlq`, que é o "DLQ na primeira tentativa" **rejeitado
  abaixo**. Regra: **teto de tentativas de republish por mensagem** (mesmo teto de voltas,
  contado em memória dentro da entrega corrente); estourado, a mensagem vai para a
  `custodia.parked` pelo `custodia.parking`, com o motivo **`retry_indisponivel`** e
  alerta — preserva a mensagem, sem laço quente e sem DLQ. O valor entra na **lista fechada
  e sem default** de `x-custodia-motivo` do F4, junto dos outros: "com motivo nomeado" sem
  dizer o nome é o default voltando pela porta dos fundos.
  *E a frequência decide o que NÃO se corta:* com a FK composta em Operações e o relay
  publicando em ordem de `id`, um confirm por vez, o órfão **por corrida** é praticamente
  inexistente. Os casos que de fato vão acontecer são os **incuráveis** — original perdido
  na janela sem binding (o incidente do broker recriado) ou morto na DLQ —, e para esses o
  retry só adia: quem entrega valor é o **parking com motivo nomeado e alerta**. Logo, se um
  dia for preciso cortar escopo aqui, o candidato é **reduzir as 10 voltas** (2 ou 3 cobrem
  a corrida real), **nunca** remover o parking.
  *Rejeitados:* `nack(requeue: true)` com ou sem backoff (o head-of-line acima); gravar
  pendente e reconciliar — cria uma **segunda fonte de verdade mutável ao lado de um livro
  append-only**, com política de expiração, dreno e reconciliação próprios, e o estorno
  pendente ficaria **invisível** para o extrato de movimentação do F8, que é `SELECT` do
  livro (§7.5). *(A §9 dos padrões **não** proíbe isso nominalmente — a versão anterior
  deste arquivo lhe atribuía uma regra que ela não tem. O que ela nomeia é "estado de
  controle duplicando dados (flags de bootstrap)", que é caso vizinho e não este;
  alternativa rejeitada por norma inexistente é rejeição sem fundamento, e o motivo real
  está escrito acima.)*; DLQ na primeira tentativa (transforma atraso de segundos em intervenção
  manual); segunda fila só para `trades.registered` (mudaria a topologia da §5, que dá
  **uma** fila ao consumidor, e não resolveria o órfão que chega depois do seu próprio
  original).
- Reforço no F4: `prefetchCount = 1` e consumo serial **removem** a principal fonte de
  fora de ordem em vez de só absorvê-la, e fazem "confirmar lote processado pela metade"
  deixar de existir por construção. **Eles não absorvem o órfão** — é justamente por
  serem serial e prefetch 1 que o requeue não funciona e o retry por TTL é obrigatório.

**B · Reconstrução de projeção.** Também em duas metades — e detectar é automático,
corrigir é manual.

- F4: `posicao_corrente` se reconstrói por **comando administrativo executado dentro do
  container** (`dotnet Custodia.API.dll --reconstruir-posicoes [cliente] [instrumento]`),
  que é a **dobra da V1 do F3** (por tipo, para os dez, ignorando os pares revertidos, com
  corte `D = ∞`) aplicada ao livro inteiro daquela chave, e refaz **as três** colunas
  (`quantidade`, `preco_medio`, `custo_total`), nunca só a quantidade — reconstruir uma das três e
  deixar as outras duas como estavam é divergência plantada pela própria ferramenta de
  conserto. *Rejeitados:* endpoint admin protegido por API key
  — a fronteira da ADR-10 é **arquitetural, não de autenticação**, e um endpoint de
  escrita administrativo é a costura por onde o endpoint de escrita de negócio entra seis
  meses depois; e auto-cura ao detectar divergência — a projeção voltaria a concordar com
  o livro e o bug do handler ficaria invisível.
- F7: **job de reconciliação que compara a dobra da V1 (com corte `D = ∞`) com
  `posicao_corrente` nas três colunas e ALERTA, nunca corrige.** Reconstruir sozinho "só as
  chaves divergentes" é auto-cura com outro nome, e está rejeitado pelo mesmo motivo. A comparação é
  determinística **porque I4 não tem filtro por relógio** — é isso que a decisão da
  liquidação no F5 preserva.

**C · Topologia do broker.** O **F2** inteiro. Declaração idempotente pela management API
como **passo do deploy**, com verificação bloqueante, controle negativo e desfecho
assimétrico; redeclaração no boot do consumidor no F4 como **segunda** linha de defesa,
com os mesmos argumentos (divergência devolve 406 `PRECONDITION_FAILED`, que é falha alta
e desejável). *Rejeitados:* declarar só no boot do consumidor (não existe consumidor até
o F4 e a janela é agora; e o declare no boot só acontece quando a aplicação conecta — o
broker pode ser recriado com a custódia parada, que foi exatamente o que aconteceu);
provisionar por `definitions.json` no repo do `hub-precos` (põe a fila deste consumidor
sob o deploy de outro serviço, e a §5 diz que a fila é do consumidor); confiar na outbox
do produtor (ela garante que o evento **sai**, nunca que alguém o **recebe**).

**D · Onde para o F1.** **Sem tabela.** A `InitialCreate` sai vazia — ela existe só para
provar que o caminho de migration no boot funciona sob a role `custodia`. Ficam
explicitamente de fora: toda DDL de negócio; fila, exchange e binding (F2); `RabbitMq__*`
(F4) e `Hub__*` (F6); qualquer rota `/v1` real, inclusive de leitura; worker, job,
extrato e consumidor. E `/health/ready` fica em `CanConnectAsync` puro — **de
propriedade, não por esquecimento**: não há schema para sondar (§10.18 registra
exatamente isso sobre o F1 do `operacoes`), e o endurecimento da §10.22 é do F3, a fase
que cria o objeto cuja ausência silenciosa permite corrupção irreversível.

---

## As CINCO listas de configuração — quais fases mexem nelas

**A doutrina mora no `PADROES.md` §10.33** ("Credencial obrigatória entra em cinco listas;
a do `env:` do step é a que se esquece, e ela falha COM o secret cadastrado"): as cinco
listas, por que a (3) é a cara, e o critério de quem entra em cinco e quem entra em duas.
Leia lá antes de mexer em credencial; aqui fica só o **escopo por fase**.

Duas consequências dela que são escopo deste roadmap, e não doutrina:

- **O inventário incompleto está escrito em dois arquivos deste repo, e o F1 os corrige.**
  O `.env.example` (linhas 35-37) manda espelhar "**as quatro listas**" e omite o `env:` do
  step; o comentário do `ci.yml` ("SECRETS QUE AINDA NAO SAO VALIDADOS AQUI") enumera
  **duas** e omite o mesmo `env:`. É a §10.20 literal — o porte herdou um inventário que já
  estava incompleto no molde. Se o F1 não os corrigir, toda fase seguinte herda a lacuna.
- **A sexta superfície, que o CI não lê**, é o `.env` da máquina de quem desenvolve
  (`LEIA-ME-KIT`, "Gate de compose no CI passa com dummy"): por isso, nas fases F4 e F6,
  avisar no PR que é **mudança quebrante**, citando as linhas novas, e rodar
  `docker compose config -q` contra o `.env` **real**, não contra o sintético do gate.

| Fase | Listas que mudam |
|---|---|
| F1 | **nenhuma em valor** — muda o *texto* do `.env.example`, o *comentário* dos dois composes e do `ci.yml`, e os arquivos de observabilidade |
| F2 | **duas**: `envs:` do ssh-action **e** `env:` do step (+ secrets no GitHub) |
| F3 | nenhuma |
| F4 | **as cinco** + `.env.example` (`RabbitMq__{Host,User,Password}`) |
| F5 | nenhuma |
| F6 | **as cinco** + `.env.example` (`Hub__{BaseUrl,ApiKey}`) |
| F7 | nenhuma — mas **muda o perfil de recurso**: medir de novo |
| F8 | nenhuma |
| F9 | nenhuma |

---

## Fila

- [x] **F1** — esqueleto deployado e observável, e a limpeza do que o porte herdou.
  **Dependência externa nova: nenhuma.**

  Solução `Custodia.API` / `Custodia.Application` / `Custodia.Domain` /
  `Custodia.Infrastructure` seguindo o molde `../operacoes/src/`, mais Serilog com
  CorrelationId, `/health`, `/health/ready`, `/metrics`, `Loki__Uri=http://alloy:3100`,
  `Properties/launchSettings.json` (sem ele `dotnet run` sobe em `Production` e o boot
  falha — armadilha 3 do kit), e migrations no boot conectando como role `custodia`, com
  a `InitialCreate` **vazia**. **Sem endpoint de negócio.**

  **`Directory.Build.props` e `Dockerfile` JÁ EXISTEM na raiz — confira, não recrie.** Os
  dois vieram no scaffolding do commit `71382b2`, e o `Dockerfile` (31 linhas) carrega em
  comentário a lição do `PublishReadyToRun` com RID explícito, que custou um deploy
  quebrado em Apple Silicon. Sobrescrevê-los por fidelidade ao molde apagaria isso em
  silêncio, que é o modo de falha da §10.20 na direção contrária. O que a fase faz com eles
  é **conferir contra o molde e ajustar o que faltar** — a §10.10 (comparar por ausência)
  vale, a recriação não.

  **`README.md` na raiz entra nesta fase, e não é documentação por gentileza.** Este repo
  não tem README (conferido: a raiz tem `CLAUDE.md`, `PADROES.md`, `LEIA-ME-KIT.md`,
  composes, `Dockerfile`, `.env.example`, `docs/`, `infra/`, `scripts/` — e nenhum
  `README.md`), e **o scaffolding já aponta para um**: o `.env.example` diz "vem de
  `dotnet user-secrets` (dev, ver README.md)" e o `docker-compose.yml` (linha 28) manda
  "troque aqui, no bloco `db` abaixo e no README". Sem ele a âncora §10.9 que esta fase
  invoca é **inexecutável** — não há comando literal a rodar, e foi exatamente por rodar
  um equivalente em vez do comando do README que a perda do `launchSettings.json` passou
  por duas revisões adversariais. O molde `../operacoes` tem `README.md` **e**
  `COMECE-AQUI.md`; portam-se os dois, pela §10.10 (compare por ausência).

  **O F1 não termina quando compila.** Critério em cinco provas — `LEIA-ME-KIT.md`,
  "O que o F1 tem que alcançar":

  1. um merge na `main` deploya sozinho (deploy na unha por SSH **não conta**);
  2. `curl` no `/health/ready` **pela VPS**;
  3. a série `up{job="custodia"}` visível no Grafana Cloud;
  4. o dashboard `custodia` aparecendo lá, **com dados**;
  5. um alerta seu disparando de propósito e chegando no Telegram.

  O `./scripts/verificar-f1.sh` **já existe neste repo, já portado e já com a correção da
  prova 4** (busca de dashboard por `uid`, nunca por `/api/search?query=`, que casa só por
  título — e os títulos têm acento). Não o reescreva: rode-o com `VPS`, `GC_GRAFANA_URL`
  e `GC_GRAFANA_TOKEN` exportados. O SKIP da prova 5 é permanente por construção — é a mão.

  **A mensagem de falha da linha 142 JÁ ESTÁ CORRIGIDA no arquivo — confira e não a mexa.**
  Ela dizia *"'custodia' NAO esta no apply-cloud.sh — copiar o JSON nao basta, o nome entra
  na lista"*, e essa "lista" é exatamente a que os cinco pontos do `apply-cloud.sh`
  (detalhados abaixo, neste mesmo F1) desmentem: serviço vizinho entra por **blocos
  `if -f` próprios**, e a única lista fixa (`for d in tesouro-direto host`, linha 281)
  publica na pasta do TD. Deixá-la como estava faria quem rodasse o script ler a instrução
  errada **com o aval desta fase**, e a instrução errada leva ao desfecho (a) do parágrafo
  abaixo, que é silencioso. **Hoje a linha 142 diz** — conferido no arquivo, e é o texto
  que fecha a contradição:

  ```
      || vermelho "'$NOME' NAO aparece no apply-cloud.sh — servico vizinho entra por BLOCOS 'if -f' proprios (cinco pontos), nao por uma lista fixa"
  ```

  As aspas **simples** em `'if -f'` são deliberadas: dentro de string com aspas duplas em
  bash, crase é substituição de comando, e o shell tentaria executar `if -f` em runtime.
  A proibição de reescrever vale para **todo** o arquivo, esta linha inclusive — a lógica,
  os códigos de saída, a busca por `uid` e o `grep -oE "rules-[a-z0-9-]+\.yaml"`. O que
  continua valendo desta correção é a **explicação** dos cinco pontos, que é o que o
  executor precisa saber para fiar o serviço na nuvem.

  **Entra também, e não é cosmético: as afirmações que o porte herdou do molde e que a
  ADR-10 desmente** (§10.20 — "porte herda também as afirmações que só valiam para o
  molde"). São **seis** ocorrências, em **seis** arquivos — `.env.example` (itens 1, 2 e
  4), `docker-compose.yml` **e** `docker-compose.prod.yml` (item 3, que conta dois),
  `infra/grafana/cloud/rules-custodia.yaml` **e** `infra/grafana/dashboards/custodia.json`
  (item 5, que também conta dois) e `infra/grafana/README.md` (item 6) —, e a maioria
  ninguém tinha notado:

  1. `.env.example`, bloco `CUSTODIA_HUB_*`: promete que "`HubCatalogoClient` valida
     `instrumento_id`" e que "o **único endpoint de escrita** responderia 503" — texto de
     Operações. Afirma também que `HubConfigGuard` derruba o boot e cita
     `src/Custodia.API/Extensions/HubConfigGuard.cs`: as duas são falsas hoje (`Hub__*` não
     está em compose nenhum; não há `src/`). §10.22 — afirmação de cobertura inexistente é
     pior que a lacuna. **Cuidado:** o mesmo arquivo cita, nas linhas 21-24, o
     `ApiKeyGuard.cs`, que **esta fase cria** — a citação certa não morre com a errada.
  2. `.env.example`, **outro bloco, e é por isso que ele escapa de quem corrige só o
     primeiro**: a oferta de "resolução nome→id do onboarding (§7.2)", que a ADR-10
     revogou, está nas **linhas 39-40**, dentro do bloco `--- AINDA NAO USADAS NESTA FASE
     ---` (linhas 30-42) — **não** no bloco `CUSTODIA_HUB_*`, que só começa na linha 44.
     Corrigir um bloco e deixar o outro mantém metade do arquivo autorizando um
     `HubCatalogoClient` de validação que esta casa não tem. *(A versão anterior deste
     inventário dizia "mesmo bloco" e mandava o executor para o lugar errado; o Pronto (f)
     pega assim mesmo porque é `grep`, mas inventário que nomeia o lugar errado é
     inventário que envelhece.)*
  3. **A mesma frase do onboarding está nos DOIS composes** (`docker-compose.yml` 13-17 e
     `docker-compose.prod.yml` 24-26). É comentário, não valor — mas é comentário que
     autoriza a fase errada a acrescentar a variável pelo motivo errado.
  4. `.env.example`, bloco `RABBITMQ_*`: promete relay/outbox, que não vai existir (I18).
     **Cuidado com a formulação:** "a Custódia não publica", ponto, é semente do defeito
     seguinte — alguém no F4 lê isso e escolhe ack-e-descarta. Vale a formulação do I18.
  5. `rules-custodia.yaml` tem **duas regras** e `dashboards/custodia.json` **quatro
     painéis** sobre séries `custodia_outbox_*` e `custodia_relay_*`, que um serviço que só
     consome nunca vai emitir. Sem remover, a prova 4 fecha com painel morto e alerta que
     não pode disparar.
  6. `infra/grafana/README.md` chama este serviço de **"Operações"**. *(Enunciado sem
     número de propósito: contar ocorrências num inventário feito para ser conferido
     envelhece na primeira edição — a versão anterior dizia "nove" e `grep -c` devolve 10.)*

  A remoção segue a guarda da §10.20 — "ao remover, substitua por uma asserção equivalente
  sobre o que existe". O equivalente do **consumidor** (profundidade da `custodia.prices`,
  idade da mensagem mais antiga, DLQ e parking não vazios, mensagens processadas por
  desfecho — §12) **ainda não existe**: a substituição fica **agendada neste roadmap**
  (F2 e F4), e o F1 entrega o dashboard só com o que ele de fato tem (HTTP, health, GC,
  pool, uptime). Agendar é a substituição honesta; deixar o painel vazio ensina o leitor a
  ignorar painel vazio, que é o canal por onde a §10.11 passou despercebida.

  **As edições no `../tesouro-direto-api`** (métrica é *pull*, mora lá). Duas são de
  arquivo: (1) alvo de scrape em `infra/alloy/config.alloy` com `job="custodia"`;
  (2) `infra/grafana/dashboards/custodia.json` e `infra/grafana/cloud/rules-custodia.yaml`
  — **nunca** `rules.yaml` (esse nome já é das 21 regras do TD, e o PUT do publicador as
  sobrescreveria). E a terceira é a que todo mundo erra: **o `apply-cloud.sh` NÃO tem uma
  "lista fixa" onde se acrescenta o nome do serviço vizinho.** A lista fixa que existe
  (`for d in tesouro-direto host`, linha 281) é só dos dashboards **do TD**, publicados no
  `FOLDER_UID` da pasta *TesouroDireto*; Hub e Operações entram por **blocos `if -f`
  próprios**. Conferido no arquivo, são **cinco pontos por serviço**:

  1. `FOLDER_UID_CUSTODIA=$(gc_folder_uid Custodia)` no topo (como `FOLDER_UID_HUB` na
     linha 37 e `FOLDER_UID_OPERACOES` na 42) — pasta própria, não a do TD;
  2. bloco `if [ -f infra/grafana/cloud/rules-custodia.yaml ]` que publica as regras nessa
     pasta (como os das linhas 200 e 237);
  3. bloco `if [ -f infra/grafana/dashboards/custodia.json ]`, com a flag
     `CUSTODIA_DASHBOARD_PUBLICADO` (como os das linhas 313 e 345);
  4. bloco de **conferência da contagem** de regras publicadas na pasta, lida do próprio
     YAML (como os das linhas 428 e 443);
  5. `uids_dashboards_verificar+=(custodia)`, condicionado à flag (como nas linhas 523
     e 529).

  **Duas consequências concretas de errar isso, e nenhuma das duas é barulhenta:**
  (a) quem acrescentar `custodia` ao `for d in tesouro-direto host` publica o dashboard na
  pasta **TesouroDireto**, com HTTP 200 e sem reclamação; (b) sem o bloco `if -f
  rules-custodia.yaml`, as regras **nunca chegam à nuvem** — e o `./scripts/verificar-f1.sh`
  **não pega**: a linha 146 faz `grep -oE "rules-[a-z0-9-]+\.yaml"` no publicador, acha
  `rules-hub.yaml` e `rules-operacoes.yaml`, confirma que esses dois existem e fica verde.
  É o incidente "alerta ficou semanas sem existir na nuvem" reproduzido, com o verificador
  desta fase dando o aval.

  **Como publicar e como provar**: procedimento permanente no `LEIA-ME-KIT.md`, seção "No
  repo do `tesouro-direto` (métrica é *pull*, mora lá)" — as duas cópias do
  `rules-custodia.yaml`, o valor do token conferido, e a prova pela **API do Grafana
  Cloud** e não pelo YAML editado. Vale para toda fase deste roadmap que cria alerta.

  **NÃO ENTRA:** nenhuma tabela e nenhuma DDL; nenhuma fila, exchange ou binding; nenhum
  `RabbitMq__*`; nenhum `Hub__*`; nenhuma rota `/v1` real; nenhum worker, job, extrato ou
  consumidor. O smoke test do deploy (`401` sem chave e `404` com chave contra
  `/v1/smoke-inexistente`) é a prova certa **justamente por não depender de rota nenhuma**
  — não o troque por um smoke em endpoint de negócio, e reconfira quando o F8 criar rotas.

  **Configuração:** **nenhuma das cinco listas muda em VALOR** — `CUSTODIA_APP_PASSWORD` e
  `CUSTODIA_API_KEY` já estão nas cinco no commit `71382b2`. Muda **texto e comentário**,
  em três lugares, e é isso que impede as fases seguintes de herdarem o inventário errado:

  - `.env.example`: o bloco que manda espelhar "**as quatro listas**" passa a enumerar as
    **cinco**, com o `env:` do step nomeado e com o motivo (a variável só no `envs:` chega
    vazia e a guarda `-z` acusa "secret vazio" com o secret cadastrado);
  - `.github/workflows/ci.yml`, comentário "SECRETS QUE AINDA NAO SAO VALIDADOS AQUI":
    hoje manda incluir só no `printf` e no `envs:` — passa a enumerar as cinco;
  - os **dois composes**, no comentário que oferece o onboarding da §7.2 (item 3 acima).

  É justamente por o gate do CI construir o próprio insumo com dummies que esses textos
  são a única coisa que protege quem desenvolve.

  **Mais duas edições pequenas, na mesma passada, e as duas são do `ci.yml`:**

  - o smoke test do deploy usa `curl -s` (linhas 245 e 256), não `-sS`. O par
    `-w '%{http_code}'` cobre o pior caso, mas o `LEIA-ME-KIT` ("`curl -s` transforma falha
    de conexão em saída vazia") pede os dois juntos, e esta fase já edita o arquivo;
  - **ADR-12 provada e não afirmada** (I12): uma varredura de `ConnectionStrings*` em
    `src/`, `appsettings*`, nos dois composes e no `printf` do deploy tem que devolver
    **exatamente uma** connection string, apontando para o database `custodia`. A ADR-12
    define esse teste de conformidade com todas as letras e nenhuma fase o executava.

  **Âncoras:** `LEIA-ME-KIT` "O que o F1 tem que alcançar" e "Deixar o deploy para depois
  de existir código"; `PADROES` §10.1 (alias único **na rede**), §10.4 (normalizar segredo
  na origem), §10.9 (rodar o comando literal do README), §10.10 e §10.20 (comparar o molde
  por ausência **e por excesso**), §10.12–10.14 (limites de recurso), §10.17 (verde no CI
  não é deployado — o job `guarda-deploy` já está no `ci.yml`), §10.18, §10.19 (testar o
  header não testa o log), **§10.33** (as cinco listas de configuração — é dela que sai o
  texto que esta fase corrige); `LEIA-ME-KIT` "No repo do `tesouro-direto`" (os cinco pontos
  do `apply-cloud.sh` e a publicação de alerta); `ARQUITETURA` §12; ADR-10 e ADR-12.

  **Prompt:**
  ```
  ultracode Crie o esqueleto da solucao seguindo o molde em ../operacoes/src/:
  Custodia.API, Custodia.Application, Custodia.Domain, Custodia.Infrastructure, mais
  Serilog com CorrelationId, health/metrics, Properties/launchSettings.json e migrations
  no boot conectando como role `custodia`.
  Directory.Build.props e Dockerfile JA EXISTEM na raiz (scaffolding do commit 71382b2) —
  CONFIRA contra o molde e ajuste o que faltar, NAO OS RECRIE. O Dockerfile carrega em
  comentario a licao do PublishReadyToRun com RID explicito, que custou um deploy quebrado
  em Apple Silicon; sobrescreve-lo por fidelidade ao molde apaga isso em silencio.
  A migration InitialCreate sai VAZIA: nenhuma tabela nesta fase.
  SEM endpoint de negocio, nem de leitura.

  CRIE TAMBEM README.md e COMECE-AQUI.md na raiz, portados do ../operacoes (PADROES
  10.10, compare o molde por AUSENCIA). Este repo nao tem README hoje, e o scaffolding
  JA APONTA para um: o .env.example diz "vem de dotnet user-secrets (dev, ver
  README.md)" e o docker-compose.yml diz "troque aqui, no bloco `db` abaixo e no
  README". Sem README nao ha comando literal a rodar, e a PADROES 10.9 fica
  inexecutavel — foi rodando um equivalente em vez do comando do README que a perda do
  launchSettings.json passou por duas revisoes adversariais. O README tem que conter o
  comando literal de subida local, e voce vai roda-lo EXATAMENTE como escrito, sem
  variaveis de ambiente na frente.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///. Nome de metodo,
  nome de teste e estrutura carregam o que o comentario carregaria. Isto vale contra o
  default do seu treino; nao comente "so nesta parte dificil".

  Leia antes de despachar: PADROES.md secao 10 inteira (35 itens, cada um de um
  incidente real — CONFIRA a contagem no arquivo antes de citar, ela cresce a cada fase) e LEIA-ME-KIT.md, secoes "O que o F1 tem que alcancar", "Armadilhas
  que custaram tempo no hub" e "Erros de orquestracao".

  ENTRA TAMBEM, e nao e cosmetico — SEIS ocorrencias, em SEIS arquivos (.env.example nos
  itens 1/2/4; os DOIS composes no item 3; rules-custodia.yaml E custodia.json no item 5;
  infra/grafana/README.md no item 6), que o porte herdou do molde `operacoes` e que a
  ADR-10 desmente (PADROES 10.20 e 10.22):
  1. .env.example, bloco CUSTODIA_HUB_BASE_URL/CUSTODIA_HUB_API_KEY: diz que
     "HubCatalogoClient valida instrumento_id" e que "o unico endpoint de escrita
     responderia 503". A Custodia NAO tem endpoint de escrita de negocio. O MESMO bloco
     afirma que "o compose falha o boot: HubConfigGuard recusa subir" e cita
     src/Custodia.API/Extensions/HubConfigGuard.cs — as DUAS sao FALSAS hoje: Hub__*
     nao esta em compose nenhum e esse arquivo nao existe (nao ha src/ neste repo).
     Afirmacao de cobertura que nao existe e pior que a lacuna (PADROES 10.22).
     Reescreva com o papel real do Hub aqui: bootstrap REST da projecao de precos
     (ARQUITETURA 7.1) e o `asof` do worker (7.4) — sem prometer guarda que so nasce no
     F6. CUIDADO PARA NAO APAGAR A CITACAO CERTA JUNTO COM A ERRADA: o mesmo
     .env.example cita, nas linhas 21-24, src/Custodia.API/Extensions/ApiKeyGuard.cs,
     que TAMBEM nao existe hoje — mas esse arquivo e criado POR VOCE nesta fase, e a
     afirmacao passa a ser verdadeira no fim dela. A que morre e a do HubConfigGuard.
     Nao use "arquivo citado que nao existe" como criterio: ele leva as duas.
  2. .env.example: a oferta de "ou resolucao nome->id do onboarding (7.2)" NAO esta no
     bloco CUSTODIA_HUB_* — ela esta nas LINHAS 39-40, dentro do bloco
     "--- AINDA NAO USADAS NESTA FASE ---" (linhas 30-42), e o bloco CUSTODIA_HUB_* so
     comeca na linha 44. Corrija OS DOIS: quem resolve nome->id e Operacoes, na borda
     (ADR-10).
  3. A MESMA frase do onboarding esta nos DOIS composes: docker-compose.yml linhas
     13-17 e docker-compose.prod.yml linhas 24-26. E comentario, nao valor — mas e
     comentario que autoriza a fase errada a acrescentar a variavel pelo motivo errado.
     Corrija os dois.
  4. .env.example, bloco RABBITMQ_USER/RABBITMQ_PASSWORD: promete relay/outbox. Nao vai
     existir. MAS NAO ESCREVA "a Custodia nao publica", ponto: ela publica sim, na
     PROPRIA infraestrutura (custodia.retry, custodia.parked, custodia.prices.dlq, do
     F2/F4). A frase absoluta e semente do proximo defeito — alguem no F4 le "nao
     publica" e escolhe ack-e-descarta, que aquela fase proibe. A formulacao correta e:
     "a Custodia nao publica NENHUM evento de contrato da 5.1 e nao tem outbox nem
     relay; o unico trafego AMQP que ela emite e infraestrutura interna dela".
  5. infra/grafana/cloud/rules-custodia.yaml tem DUAS regras
     (custodia-outbox-backlog-velho, custodia-relay-falha-persistente) e
     infra/grafana/dashboards/custodia.json tem QUATRO paineis (Backlog da outbox,
     Idade do backlog mais antigo, Ciclos de relay por desfecho, Eventos publicados no
     broker) sobre series custodia_outbox_* e custodia_relay_*, que nunca vao existir.
     Remova. O substituto (metricas de CONSUMO) esta agendado no F2 e no F4 deste
     roadmap — nao invente metrica que ainda nao tem produtor.
  6. infra/grafana/README.md chama ESTE servico de "Operacoes". Nao confie em numero
     escrito aqui — rode `grep -n 'Operações' infra/grafana/README.md`. O criterio NAO e
     "zero ocorrencias": mencao que nomeia o servico IRMAO como TERCEIRO e legitima e pode
     ser a redacao certa ("diferente do Hub e do Operacoes, este container tem limite de
     memoria"). Mate toda ocorrencia que se refira a ESTE servico, e JUSTIFIQUE
     NOMINALMENTE, uma a uma, as que sobrarem.

  AS EDICOES NO ../tesouro-direto-api (metrica e pull, mora la):
  1. alvo de scrape em infra/alloy/config.alloy com job="custodia";
  2. dashboard em infra/grafana/dashboards/custodia.json e regras em
     infra/grafana/cloud/rules-custodia.yaml, NUNCA rules.yaml (esse nome ja e das 21
     regras do TD e o PUT do publicador as sobrescreveria);
  3. o apply-cloud.sh. NAO EXISTE "lista fixa" onde se acrescenta o nome do servico
     vizinho: a lista fixa que existe (`for d in tesouro-direto host`, linha 281) e so
     dos dashboards DO TD, publicados na pasta TesouroDireto. Hub e Operacoes entram por
     BLOCOS `if -f` proprios. SAO CINCO PONTOS POR SERVICO — leia o arquivo e copie o
     padrao do Operacoes:
       (a) FOLDER_UID_CUSTODIA=$(gc_folder_uid Custodia) no topo (cf. linhas 37 e 42);
       (b) bloco `if [ -f infra/grafana/cloud/rules-custodia.yaml ]` publicando as
           regras NESSA pasta (cf. linhas 200 e 237);
       (c) bloco `if [ -f infra/grafana/dashboards/custodia.json ]` com a flag
           CUSTODIA_DASHBOARD_PUBLICADO (cf. linhas 313 e 345);
       (d) bloco de CONFERENCIA DA CONTAGEM de regras publicadas na pasta, lida do
           proprio YAML (cf. linhas 428 e 443);
       (e) uids_dashboards_verificar+=(custodia), condicionado a flag (cf. 523 e 529).
     Se voce acrescentar "custodia" ao `for d in tesouro-direto host`, o dashboard vai
     para a pasta TesouroDireto com HTTP 200 e ninguem reclama. Se voce esquecer o bloco
     (b), as regras NUNCA sao publicadas — e o ./scripts/verificar-f1.sh NAO PEGA: a
     linha 146 dele faz grep -oE "rules-[a-z0-9-]+\.yaml" no publicador, acha
     rules-hub.yaml e rules-operacoes.yaml, confirma que existem, e fica verde.
  Ao rodar o apply-cloud.sh, exporte GC_GRAFANA_URL, GC_GRAFANA_TOKEN e
  TELEGRAM_BOT_TOKEN na invocacao E CONFIRA O VALOR do token: a guarda ${VAR:?} so
  testa vazio, e um placeholder passa por ela e deixa o Telegram mudo para TODOS os
  servicos publicados (hoje TD, Hub e Operacoes; com o seu, quatro) e o script reporta
  sucesso. E prove pela API do Grafana Cloud que a regra e o dashboard estao PUBLICADOS,
  na PASTA Custodia — nao que o arquivo foi copiado.

  CONFIGURACAO — nenhuma das CINCO listas muda em VALOR (CUSTODIA_APP_PASSWORD e
  CUSTODIA_API_KEY ja estao nas cinco). Muda TEXTO, e e o que impede as fases seguintes
  de herdarem o inventario errado. As cinco listas sao: (1) os dois composes, (2) o
  `envs:` do ssh-action, (3) o bloco `env:` DO MESMO STEP (mapeia ${{ secrets.* }}),
  (4) o printf do .env, (5) as dummies do config gate. A (3) e a que se esquece: a
  variavel so no `envs:` e encaminhada INEXISTENTE, chega como string vazia, e a guarda
  `-z` do deploy acusa "secret vazio" com o secret cadastrado e correto. Corrija os dois
  textos que hoje enumeram menos que cinco:
    - .env.example: "as quatro listas tem que espelhar uma a outra" -> as CINCO,
      nomeando o `env:` do step e dizendo por que ele importa;
    - .github/workflows/ci.yml, comentario "SECRETS QUE AINDA NAO SAO VALIDADOS AQUI":
      hoje manda incluir so no printf e no `envs:` -> as CINCO.
  MAIS DUAS EDICOES no mesmo ci.yml: o smoke test do deploy usa `curl -s` nas linhas 245
  e 256 -> troque por `curl -sS` (o -w '%{http_code}' cobre o pior caso, mas `-s` sozinho
  transforma falha de conexao em saida vazia — LEIA-ME-KIT). E rode a varredura da ADR-12
  (invariante I12): `ConnectionStrings*` em src/, appsettings*, nos dois composes e no
  printf do deploy tem que devolver EXATAMENTE UMA connection string, para o database
  `custodia`. A ADR-12 define esse teste de conformidade e nenhuma fase o executava.

  NAO ENTRA: tabela, DDL, fila, exchange, binding, RabbitMq__*, Hub__*, rota /v1 real,
  worker, job, extrato, consumidor. E /health/ready fica em CanConnectAsync puro — sem
  tabela nenhuma, sondar schema seria vacuo (PADROES 10.18); o endurecimento da 10.22 e
  do F3, nao seu.

  O F1 NAO termina quando compila. Termina com CI/CD funcionando, deploy na VPS e
  metrica no Grafana. Ao final rode ./scripts/verificar-f1.sh (ele JA EXISTE neste repo,
  ja com a correcao da prova 4 E ja com a mensagem da LINHA 142 corrigida — NAO O
  REESCREVA, E NAO PROCURE NADA PARA CONSERTAR NELE. A linha 142 dizia "o nome entra na
  lista" do apply-cloud.sh, que e falso, e ela FOI CORRIGIDA: hoje ela diz que servico
  vizinho entra por BLOCOS de 'if -f' proprios (cinco pontos) e nao por uma lista fixa —
  a unica lista fixa, o "for d in tesouro-direto host", publica na pasta do TD. NAO TOQUE
  EM NADA do arquivo: nem nessa mensagem, nem na logica, nem nos codigos de saida, nem na
  busca por uid, nem no grep -oE "rules-[a-z0-9-]+\.yaml") e me mostre a saida.
  O QUE VOCE PRECISA DAQUELA CORRECAO E A EXPLICACAO, nao a edicao: sao CINCO PONTOS por
  servico no apply-cloud.sh (FOLDER_UID proprio, bloco if -f das regras, bloco if -f do
  dashboard, conferencia de contagem, uids_dashboards_verificar), e eles estao listados
  na prosa desta fase. ATENCAO AO CODIGO
  DE SAIDA: o script termina com `[ "$pulado" -eq 0 ] || exit 2`, e a prova 5 (alerta no
  Telegram) e um SKIP PERMANENTE por construcao — e a mao. Entao `exit 2` com ZERO
  FALHAS e o desfecho ESPERADO desta fase; nao "conserte" o script para ele sair 0, e
  nao conclua que o F1 nao fechou por causa dele. Qualquer OUTRO skip, ou qualquer
  falha, reprova.

  Ao final, guardiao-padroes e DEPOIS revisor — em serie, nunca em paralelo: o revisor
  muta a implementacao de proposito para provar que um teste e vacuo, e o guardiao lendo
  esse estado reporta como defeito real algo que ja nao existe (LEIA-ME-KIT). Achado
  grave corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira
  tambem os textos que VOCE escreveu (README, .env.example, nota de fecho): no F2 do
  operacoes os quatro ultimos defeitos foram do orquestrador, nao dos executores.
  Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** as cinco provas do `LEIA-ME-KIT`, com a saída do
  `./scripts/verificar-f1.sh` anexada — **fechando com `exit 2` e ZERO falhas**, que é o
  desfecho esperado: o único SKIP aceitável é o da prova 5, permanente por construção
  porque é a mão (`[ "$pulado" -eq 0 ] || exit 2` nas últimas linhas do script). Qualquer
  outro SKIP e qualquer FALHA reprovam. Direção **estrita** da fase, e ela é o próprio
  smoke do deploy: `GET /v1/smoke-inexistente` **sem** chave = 401 **E com** a
  `CUSTODIA_API_KEY` = 404 — só o 401 passaria também com a chave errada. Mais:
  (a) depois do merge, o run de **push** com `deploy: success`, não o do PR (§10.17);
  (b) `grep -rn 'outbox\|relay' infra/grafana/cloud/*.yaml infra/grafana/dashboards/*.json`
  sem resultado, e `grep -n 'Operações' infra/grafana/README.md` **sem nenhuma linha que
  chame ESTE serviço de "Operações"** — as ocorrências de hoje (rode o grep; são 10 agora e
  o número envelhece) descrevem todas este container e todas morrem. **O critério não é
  "zero ocorrências", e isso é deliberado:** uma menção que nomeia o serviço **irmão como
  terceiro** é legítima e pode ser a redação certa (ex.: "diferente do Hub e do Operações,
  este container tem limite de memória"); um critério absoluto reprovaria a entrega
  **correta**. A entrega justifica **nominalmente** cada ocorrência sobrevivente, uma a
  uma. É o mesmo cuidado que o parágrafo seguinte aplica aos arquivos de dado, e a versão
  anterior deste Pronto não o aplicava aqui. **O grep de `outbox|relay`
  é sobre os arquivos de DADO de propósito:** a §10.20, que esta fase invoca, manda
  "substituir por uma asserção equivalente ao remover" — se o executor explicar no
  `infra/grafana/README.md` por que não há painel de outbox aqui, uma varredura do
  diretório inteiro reprovaria a entrega **correta**; (c) `docker inspect` dos aliases **efetivos**
  nas redes `plataforma` e `tesouro-net` mostrando `custodia`/`custodia-app` e **nenhum**
  `app` — `docker ps` não mostra alias, e o YAML não distingue "acrescenta" de
  "substitui" (§10.1);
  (d) `git -C ../tesouro-direto-api ls-files -- infra/grafana/dashboards/custodia.json
  infra/grafana/cloud/rules-custodia.yaml` devolvendo **as DUAS linhas**, mais
  `git -C ../tesouro-direto-api log -1 --` em cada um. **E a fiação no `apply-cloud.sh`
  conferida pelos cinco pontos, não por "o nome está na lista":** `grep -n custodia
  scripts/grafana-cloud/apply-cloud.sh` mostrando o `FOLDER_UID_CUSTODIA`, o bloco
  `if -f .../rules-custodia.yaml`, o bloco `if -f .../dashboards/custodia.json`, o bloco de
  conferência da contagem e o append em `uids_dashboards_verificar` — mais a conferência,
  **pela API do Grafana Cloud**, de que o dashboard caiu na pasta **Custodia** e não na
  *TesouroDireto*, e de que as regras existem lá. O `./scripts/verificar-f1.sh` **não
  substitui esta conferência** e é bom saber por quê: o `grep -oE "rules-[a-z0-9-]+\.yaml"`
  dele fica verde com `rules-hub.yaml` e `rules-operacoes.yaml`, mesmo que o bloco da
  custódia nunca tenha sido escrito. **Não use `git status` para isto:** ele mostra o que **não** está
  rastreado, então devolve a mesma coisa — nada — para "copiado e commitado" e para
  "nunca criado"; é Pronto que passa por vacuidade, e o fecho deste próprio arquivo
  registra a inversão ("`git ls-files` e `git diff` não mostram o que ainda não foi
  rastreado" vale para arquivo **novo**; aqui a pergunta é a oposta). `git status
  --short` fica só como checagem complementar de "não ficou nada por commitar";
  (e) `dotnet ef migrations list` mostrando só a `InitialCreate` vazia e `\dt` no
  database `custodia` devolvendo **zero** tabelas de negócio;
  (f) `grep -rn 'onboarding\|§7.2\|HubCatalogoClient\|HubConfigGuard' docker-compose.yml
  docker-compose.prod.yml .env.example` com **cada ocorrência sobrevivente justificada
  nominalmente**, uma a uma. **O critério NÃO é "sem resultado", e a diferença não é
  cosmética: "sem resultado" reprova a entrega CORRETA.** O prompt manda *reescrever* esses
  três textos com o papel real do Hub, e a redação certa é a **negação explícita** que o F6
  escreve com esse nome — "a resolução nome→id do **onboarding** (§7.2) acontece em
  Operações, na borda (ADR-10); não existe `HubCatalogoClient` nesta casa" —, e escrever a
  negação faz o grep casar. É exatamente a disciplina que este mesmo Pronto aplica duas
  alíneas acima a `Operações` e a `outbox|relay`, com a frase "um critério absoluto
  reprovaria a entrega **correta**"; a versão anterior desta alínea não a aplicava aqui.
  Critério: **não sobra nenhuma ocorrência que PROMETA** onboarding, `HubCatalogoClient` ou
  `HubConfigGuard` nesta casa — sobrevive só o que os nega ou os atribui nominalmente a
  outro serviço, e a entrega diz qual é qual. **E o escopo desta alínea são TRÊS arquivos, não
  seis:** ela cobre os itens 1, 2, 3 e 4 do inventário (`.env.example` e os dois composes);
  os itens 5 e 6 — `rules-custodia.yaml`, `dashboards/custodia.json` e
  `infra/grafana/README.md` — são cobertos pela alínea (b), e a versão anterior desta frase
  dizia "as seis ocorrências morreram nos cinco arquivos" contra um inventário que declara
  **seis arquivos** e um grep que lê **três**;
  (g) `grep -n 'quatro listas' .env.example .github/workflows/ci.yml` sem resultado, e as
  **cinco** enumeradas nos dois textos, com o `env:` do step nomeado;
  (h) o **comando literal de subida escrito no `README.md`** rodado exatamente como está,
  **sem variáveis de ambiente na frente** — é a forma executável da §10.9, e é ela que
  pega o `launchSettings.json` faltando;
  (i) **I12 / ADR-12, provada e não afirmada:** varredura de `ConnectionStrings*` em `src/`,
  `appsettings*`, nos dois composes e no `printf` do deploy devolvendo **exatamente uma**
  connection string, apontando para o database `custodia`. É o teste de conformidade que a
  própria ADR-12 enuncia; ele é reconferido no F6, quando `Hub__BaseUrl` entrar, para
  ninguém "resolver" o bootstrap com uma conexão ao banco do Hub;
  (j) `grep -n 'curl -s ' .github/workflows/ci.yml` sem resultado no bloco do smoke test —
  as duas chamadas passaram a `-sS`.

  <br>**FECHADO em 2026-09-08.** PR [#1](https://github.com/renatojsilvas/custodia/pull/1),
  commits `40a94c3` (esqueleto e limpeza), `f6cc66b`, `d1a48f0` e `762d39c` (as três rodadas
  de correção da revisão adversarial), `e4561b9` e `858b1db` (as lições). No repo vizinho:
  `renatojsilvas/tesouro-direto`, commit `14f0406`.

  **As cinco provas, com o que provou cada uma:**
  1. run de push `e6c54c1` com `test` → `deploy` → `guarda-deploy` todos verdes, sem passo
     à mão. O run anterior, do commit de scaffolding, ficou **vermelho de propósito** e é
     um bônus: o `guarda-deploy` fez o trabalho da §10.17 ao vivo, marcando "NÃO chegou a
     produção" em vez de um `skipped` cinza;
  2. `/health/ready` = 200 pela VPS;
  3. `up{job="custodia"} = 1`, instância `custodia-app:8080`;
  4. dashboard `custodia`, 10 painéis, **na pasta `Custodia`** (`dfxmfeaxnxh4wf`) e não na
     `TesouroDireto` — conferido pela API, que é o único jeito de pegar o modo de falha (a);
  5. as duas regras disparadas pela **condição real** (`docker stop custodia-app`, não
     limiar adulterado), roteadas para `telegram-custodia` e confirmadas no chat com o
     prefixo certo. Serviço restaurado e healthy em seguida.

  `./scripts/verificar-f1.sh`: **10 ok, 0 falhas, 1 pulado, exit 2** — o desfecho esperado.

  **Três coisas que esta fase descobriu e que mudam o que vem depois:**
  - **`PADROES.md` §10 foi de 35 para 38 itens**, mais um corolário na §10.8. O bloco de
    prompt acima diz "35 itens" porque era verdade quando foi despachado; ele carrega a
    própria guarda ("CONFIRA a contagem no arquivo"), e é ela que vale, não o número.
  - **Uma sétima ocorrência do inventário da §10.20, fora da lista desta fase:** o
    `infra/grafana/README.md` afirmava que o repo vizinho já tinha o contact point
    `telegram-custodia` e a rota `service = custodia`. Eram falsas. Foram **tornadas
    verdadeiras** lá em vez de apagadas aqui — o publicador confirmou criando o contact
    point ("criado", não "atualizado"). A regra que sai disso está no `LEIA-ME-KIT`:
    afirmação sobre outro repositório se confere **naquele** repositório.
  - **O número de memória do vizinho que o kit registrava está velho.** O
    `tesouro-direto-alloy` foi a 96,8% do teto ao ganhar o quarto alvo de scrape, e o
    cgroup mostrou que 190 dos 247 MB eram page cache, com `oom_kill 0` e PSI zerado. A
    lição da §10.14 continua inteira; o número, não (`PADROES.md` §10.38).

  **Pendência que o F2 herda, e ela é bloqueante para o schema:** o `AppDbContext` mantém
  `ApplyConfigurationsFromAssembly` de propósito (o `advisor` reprovou omiti-lo), mas
  **não há teste que prove que a fiação continua ligada** — hoje seria vácuo, com zero
  configurations. Ao criar a primeira `IEntityTypeConfiguration`, acrescente teste que
  assere `configurations.Count > 0` (guarda §10.8) **e** que toda entidade configurada
  aparece em `AppDbContext.Model.GetEntityTypes()`. Sem ele, a configuration não entra no
  modelo, o `migrations add` gera um `Up()` vazio — idêntico à `InitialCreate` legítima
  desta fase — e o `/health/ready` não pega (§10.18).
  **Segunda pendência, não bloqueante:** ao criar o primeiro repositório ou `IUnitOfWork`,
  reintroduzir o `ProjectReference` Infrastructure→Application e **migrar o controle
  positivo da §10.8** em `DependencyTests` de `Application→Domain` para
  `Infrastructure→Application`, apagando da mensagem do assert a frase que passa a ser
  falsa. Esta falha em compilação, então é auto-corretiva — a de cima não é.

- [ ] **F2** — topologia do broker **antes de qualquer consumidor**.
  **Dependência externa nova: broker `plataforma-rabbitmq` alcançável.**

  Um passo de deploy **idempotente** que declara, pela management API, o exchange `prices`
  (topic, durable), a fila `custodia.prices` (quorum, durable) e os **quatro** bindings da
  §5 (`prices.#`, `corpactions.#`, `eod.ready`, `trades.registered`); mais as **três**
  peças que as decisões deste roadmap exigem e que a `ARQUITETURA` não prevê (§10.26
  registra essa ausência): a DLQ `custodia.prices.dlq`, a fila de estacionamento
  `custodia.parked` e a fila de **retry com atraso** `custodia.retry`, que é o que torna
  a Decisão A executável. E a verificação bloqueante do resultado, com controle positivo
  **e** negativo (§10.8: a checagem tem que saber dizer "não").

  **Por que esta é a fase 2 e não a sexta.** Operações publica `trades.registered` em
  produção desde 2026-09-06. Cada dia entre o F1 e o F2 é um dia de trades potencialmente
  perdidos **para sempre** — e não há caminho de recuperação por contrato. Cada dia depois
  do F2 é um dia de mensagens acumuladas numa fila durável, esperando o consumidor do F4.
  A fila **acumula de propósito**: isso é a entrega, não efeito colateral.

  **Os argumentos da fila se decidem AQUI, e não no F4, porque são praticamente
  imutáveis** — mudar argumento de fila existente exige apagá-la e recriá-la, e apagá-la
  depois do F2 perde o backlog que ela existe para guardar. É a §10.21 aplicada fora do
  banco: na dúvida entre declarar agora e adiar, declara-se agora.

  - **Sem `x-max-length` e sem `x-message-ttl`.** *Rejeitado:* teto de fila com
    `x-overflow=reject-publish` — envenenaria o relay do `operacoes` e do `hub`, que são
    serviços de **terceiros**, reproduzindo do lado deles a amplificação da §10.26; e TTL
    descarta em silêncio, que é literalmente o defeito que esta fase existe para impedir.
    O contrapeso é alerta de profundidade + medição do broker, não um teto que empurra o
    problema para o vizinho.
  - **Com `x-delivery-limit` explícito e DLX apontando para `custodia.prices.dlq`.** Em
    quorum queue, limite estourado **sem** dead-letter descarta a mensagem. Verifique o
    default contra o `rabbitmq:4` real antes de escolher o número (§10.9: comando literal,
    não memória — o default mudou entre versões).
  - **Os QUATRO exchanges de infraestrutura são NOSSOS** — o DLX, o do parking, o de
    entrada do retry e o de retorno do retry (`custodia.dlx`, `custodia.parking`,
    `custodia.retry.in`, `custodia.retry.dlx`) —, nunca o `prices` compartilhado.
    Dead-letter de volta para um topic exchange compartilhado republicaria com a routing
    key original em infraestrutura de terceiros: o retry da Custódia passaria a ser visível
    para todo consumidor futuro que bindar (a §5 prevê "consumidor novo = fila nova com
    seus bindings"), duplicando entrega do lado deles. É reintroduzir por outra porta o
    acoplamento que a §5 evitou.
  - **Os quatro exchanges nossos são `fanout`, durable — e isso é a correção de um buraco,
    não um detalhe.** Mensagem dead-letrada **conserva a routing key original**
    (`trades.registered`, `corpactions.td`, ...). Um `custodia.dlx` declarado como `topic`
    com um binding qualquer que não case tudo — ou sem `x-dead-letter-routing-key` — faz a
    mensagem morta ser **descartada em silêncio dentro da nossa própria infraestrutura**:
    é o defeito que esta fase inteira existe para impedir, reproduzido do lado de cá.
    `fanout` ignora a routing key por construção e não tem como não casar. *Rejeitado:*
    `topic` com binding `#` — funciona, mas depende de um binding existir e estar certo,
    e o Pronto "o binding existe" fica verde com o binding errado; `fanout` remove a
    condição em vez de vigiá-la. *Rejeitado:* publicar na default exchange (`""`) com
    routing key = nome da fila — funciona e é menos topologia, mas torna invisível na
    management API quem publica para onde, e esta fase existe justamente para que a
    topologia seja **conferível**.
  - **A fila de retry com atraso, e é ela que sustenta a Decisão A.** `custodia.retry`
    (quorum, durable, **sem consumidor**) com `x-message-ttl` e
    `x-dead-letter-exchange = custodia.retry.dlx` (fanout) → de volta para
    `custodia.prices`. O consumidor republica o órfão **pelo exchange `custodia.retry.in`**
    (nunca direto na fila) e confirma a mensagem original,
    liberando a cabeça da fila; passado o TTL, o broker devolve a mensagem à fila
    principal sozinho. `x-message-ttl = 30000` (30 s) e teto de **10** voltas — cerca de
    5 minutos antes de estacionar visivelmente. **Confira o intervalo real do relay do
    `../operacoes` com o comando literal antes de fixar o número** (§10.9): o TTL só
    precisa ser maior que o intervalo entre a publicação do estorno e a do original, e
    esse número está lá, não na memória de ninguém.
    **O risco desta fila NÃO é o TTL — é a estratégia de dead-lettering, e confundir os
    dois faria a próxima pessoa concluir a coisa errada (§10.9).** `x-message-ttl` por fila
    e dead-letter por expiração **funcionam** em quorum queue; o que não é seguro é o
    **default**: a estratégia de dead-lettering de quorum queue é `at-most-once`, e ela
    pode **descartar** a mensagem durante o dead-letter — exatamente a perda silenciosa que
    esta fase existe para impedir, agora dentro da nossa própria infraestrutura. Duas
    saídas, e a fase escolhe uma **medindo contra o `rabbitmq:4` real**: (i) declarar
    `x-dead-letter-strategy: at-least-once` na `custodia.retry`, o que **exige**
    `x-overflow: reject-publish` nela — aceitável aqui porque o único publicador dessa fila
    somos nós, e o reject volta como **confirm negado**, que a Decisão A trata **pelo ramo
    persistente**: teto de tentativas de republish e, estourado, parking com
    `retry_indisponivel`. *Não é "a Decisão A já sabe tratar" e pronto: o tratamento
    ingênuo do confirm negado — `nack(requeue: true)` e repete — com a `custodia.retry`
    cheia é laço quente e DLQ, os dois desfechos que este arquivo rejeita. É por causa
    desta saída (i) que aquele ramo existe nomeado; escolher (i) sem ele é escolher o laço
    quente.* Ou
    (ii) a fila nascer `classic` **durable** — é fila de atraso, sem consumidor e sem dado
    próprio, o único lugar deste desenho onde `classic` é aceitável. **O fecho da fase
    registra qual foi E POR QUÊ**, com a razão escrita como "estratégia de DLX", nunca como
    "quorum não suporta TTL", que é falso.
    Ligações: `custodia.dlx` → `custodia.prices.dlq`; `custodia.parking` →
    `custodia.parked`; `custodia.retry.dlx` → `custodia.prices`; e o publish do
    consumidor entra por `custodia.parking` e por um `custodia.retry.in` (fanout) →
    `custodia.retry`.

  **A prova de fumaça, e por que ela mudou.** Publica-se `prices.smoke` (casa `prices.#`),
  **nunca** `trades.registered`: essa iria para o livro append-only no F4 e ficaria lá para
  sempre (`LEIA-ME-KIT`, "Teste manual em tabela append-only"). E a limpeza é **`basic.get`
  + ack da mensagem específica, jamais `purge`** — o purge é da fila inteira, e a fila
  passa a acumular `trades.registered` real desde o instante em que o binding existe:
  purgar apagaria exatamente o backlog que esta fase existe para preservar. *Os juízes
  divergiram aqui — um propôs provar o binding com um `POST /v1/operacoes` real e purge
  decidido antes; fica rejeitado porque purge com backlog real na fila é a mesma perda que
  a fase impede, e o binding de `trades.registered` já é provado por comparação de conjunto
  com controle negativo.* **E a prova é de DELTA, nunca de valor absoluto:** anota-se
  `messages_ready` antes, exige-se `depois == antes + 1`, retira-se a mensagem específica e
  exige-se `final == antes`. Exigir `messages_ready == 0` antes de publicar — como dizia o
  rascunho anterior — reprovaria o deploy assim que o primeiro `trades.registered` real
  chegasse, e esta fase diz com todas as letras que **a fila acumula de propósito**; o
  próprio Pronto manda rodar o deploy duas vezes seguidas, e na segunda já pode haver
  backlog. Critério que dá falso negativo justamente quando a fase deu certo é a mesma
  família de "reprovar o próprio deploy por causa de um serviço que não é seu".

  **Verificação no deploy — é ela que substitui o que o CI perdeu de propósito.** O
  comentário no `.github/workflows/ci.yml` (bloco do deploy) registra que a custódia não
  tem verificação de credencial do broker nem smoke de relay, porque o molde **publica** e
  a custódia **consome** — e que a prova equivalente do consumidor é outra: *o que importa
  é que a fila e o binding existam ANTES da primeira publicação.* Esta é a fase que
  preenche o buraco. A verificação sai de um container cliente **na rede `plataforma`**,
  nunca por loopback (§10.3), com `curl -sS -w '%{http_code}'` — `curl -s` transforma falha
  de conexão em saída vazia e apaga a diferença entre "não conectou" e "conectou e
  recusou". Desfecho **assimétrico**, que é a lição de "reprovar o próprio deploy por causa
  de um serviço que não é seu":

  | Situação | Desfecho | Por quê |
  |---|---|---|
  | 401 na management API | **reprova**, rápido, sem repetir | é o secret **deste** repo divergindo, e é acionável aqui |
  | broker inacessível **depois do laço de espera** | `::warning::` e segue — **e só enquanto o alerta de topologia existir** (ver abaixo) | o `plataforma-rabbitmq` é serviço do `hub-precos`; `exit 1` faria o `guarda-deploy` afirmar "este commit NÃO chegou a produção" com o container no ar e healthy |
  | autenticou, tentou declarar, e a fila/binding continua ausente | **reprova** | a declaração é nossa |
  | exchange `prices` **presente com propriedades divergentes** | **reprova e NÃO redeclara** | redeclarar com argumento diferente do que o produtor espera quebra o publish do Hub e do Operações no próximo boot deles (406) — reproduziria, do lado do **produtor**, a classe de dano que esta fase existe para impedir; a divergência é do dono do exchange e a correção é no repo dele |

  **O laço de espera antes de dizer "inacessível" não é zelo, é a §10.15.** "Inacessível"
  só pode ser declarado depois de um laço com timeout que **repete em connect-refused** e
  **falha rápido em 401** — o incidente da §10.15 é literalmente `curl: (7) failed to
  connect` seis segundos depois do "Started". Sem o laço, um blip de segundos durante o
  deploy deixa a topologia por declarar com tudo verde, que é a janela de perda que a fase
  existe para fechar.

  **Argumentos literais do `prices`, para a linha 4 da tabela ser conferível:** `type:
  topic`, `durable: true`, `auto_delete: false`, `internal: false`, **sem argumentos
  extras** — conferidos contra o que o `../hub-precos` declara hoje
  (`src/Hub.Infrastructure/Messaging/RabbitMqConnectionProvider.cs`:
  `ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false)`,
  sem `arguments`). Declarar o `prices` do nosso lado é defensável — quando o volume do
  broker se perde, o exchange volta pelo primeiro publicador, e nós não somos publicador —
  mas só com **estes** argumentos e com a regra "divergiu, não redeclara".

  **Alerta de topologia — INCONDICIONAL nesta fase, e é ele que autoriza o `::warning::`.**
  O `LEIA-ME-KIT` registra que "o alerta que falta na plataforma não é sobre a outbox — é
  sobre o exchange `prices` ter os bindings esperados, ou sobre `custodia.prices`
  existir". A versão anterior desta fase adiava a regra para o F4 "se o broker não for
  raspado", e isso abria exatamente o buraco que a fase fecha: entre o F2 e o F4, um blip
  transitório deixaria a fila sem existir, o deploy verde, e **nenhum sinal** — o
  `::warning::` de um run antigo não é sinal, e o próximo deploy pode ser semanas depois.
  A dependência se inverte: **o desfecho assimétrico ("segue apesar do broker fora") só é
  legítimo PORQUE existe um alerta cobrindo a lacuna.** Sem alerta, a linha 2 da tabela
  vira **reprova**. Três caminhos, nesta ordem, e a fase não fecha sem um deles:

  1. **Medir** se o `plataforma-rabbitmq` já é raspado pelo alloy (`LEIA-ME-KIT`,
     "Especular em vez de medir"). Se for, a regra `custodia-topologia-ausente` entra em
     `rules-custodia.yaml` **nesta fase**, ancorada em
     `absent(rabbitmq_queue_messages_ready{queue="custodia.prices"})`.
  2. Se **não** for raspado, o F2 **torna-o raspado**: acrescenta o alvo em
     `../tesouro-direto-api/infra/alloy/config.alloy`, do mesmo jeito que o F1 acrescentou
     o da custódia. **E a §10.9 se aplica à SÉRIE, não ao endpoint:** conferir com o
     comando literal que o endpoint de métricas responde prova que o plugin está
     habilitado, e **não** prova que a série que a regra vai usar existe. O comando literal
     a rodar é o que busca **`rabbitmq_queue_messages_ready{queue="custodia.prices"}` no
     corpo da resposta**, com a fila já declarada. Sem esse passo, a regra nasce sobre uma
     série que nunca existiu, e o Pronto por `noDataState` a aprova — ver a armadilha
     abaixo.
  3. Se o endpoint de métricas do broker não existir, ou existir **sem a série** acima, e
     não puder ser habilitado (é container de **outro** repo), então a regra não tem como
     existir, e a consequência é
     a inversão acima: o passo passa a **reprovar** também no caso "broker inacessível", e
     isso fica escrito no comentário do step com o motivo.

  **O sinal escolhido, e o que ele NÃO cobre — escrito porque a versão anterior desta fase
  escolheu um sinal que não existe.** A regra é `absent()` sobre
  `rabbitmq_queue_messages_ready{queue="custodia.prices"}`, isto é, sobre a **existência da
  fila**, que o `rabbitmq_prometheus` expõe de verdade, por fila. *Rejeitado:* **contagem
  de bindings do `prices`** — como dizia a versão anterior. Dois defeitos, e o primeiro é
  fatal: (i) o plugin expõe séries **por fila** e contagens globais, e **não** expõe
  contagem de bindings **por exchange**; a regra nasceria sobre uma série inexistente,
  disparando todo dia para sempre — o "alerta diário permanente para o qual ninguém olha
  mais" que o F7 usa como argumento contra pôr `caixa:*` no alerta de preço ausente;
  (ii) a contagem de bindings do `prices` **muda quando outro serviço binda uma fila nova**
  (a §5 prevê: "consumidor novo = fila nova com seus bindings") — seria alerta nosso
  disparando por mudança alheia, a mesma família de "estragar o alheio por causa de um
  serviço que não é seu" que esta fase invoca para justificar o `::warning::`.
  **A lacuna que sobra é declarada:** a fila existir não prova que os **quatro bindings**
  existem. O incidente real — volume do broker perdido — leva fila **e** bindings juntos, e
  a ausência da série cobre exatamente esse caso; o que fica descoberto é a remoção
  cirúrgica de um binding com a fila intacta, e para isso a cobertura é a **verificação
  bloqueante do deploy** (management API, onde binding é conferível), no próximo deploy.
  Lacuna escrita é lacuna; lacuna coberta por uma série que não existe é lacuna **com
  alerta verde**.

  **A regra de topologia só existe quando chega à nuvem** — as duas cópias do
  `rules-custodia.yaml`, o valor do token conferido e a prova pela API do Grafana Cloud
  estão no `LEIA-ME-KIT.md`, seção "No repo do `tesouro-direto`". É procedimento permanente,
  não escopo desta fase; segui-lo é obrigatório aqui.

  **NÃO ENTRA:** nenhum código .NET de consumo, nenhum `basic.consume`, nenhum
  `RabbitMq__*` no compose, nenhuma leitura de mensagem. Um consumidor "esqueleto" que
  desse ack e ignorasse descartaria trade **real** de produção que não tem de onde voltar.
  Também não entra alerta de **profundidade** da fila: aqui ela cresce por desenho, e o
  alerta entra no F4 com o consumidor, ou o Telegram vira ruído.

  **Configuração: DUAS listas**, e a assimetria é proposital. Entram os secrets do broker
  no GitHub, no `envs:` do ssh-action **e no bloco `env:` do mesmo step** — as duas
  superfícies andam juntas e nunca se separam: só no `envs:` a variável é encaminhada
  **inexistente**, chega vazia no script e a guarda `-z` acusa "secret vazio" com o secret
  cadastrado. Normalizados com `tr -d '\r\n'` **na origem** (§10.4). **Não** entram nos
  composes, **não** entram no `printf` do `.env`, **não** entram nas dummies do config
  gate — porque nada no compose as referencia, e variável com `:?` que ninguém lê derruba
  o `up` e o `config -q` sem motivo.

  **Âncoras:** `ARQUITETURA` §5 (topologia mínima, quorum queue, fila do consumidor), §12
  (profundidade da `custodia.prices` na observabilidade mínima), ADR-1, ADR-3;
  `LEIA-ME-KIT` "Perder o volume do broker apaga fila e binding, e a outbox não protege
  contra isso", "Reprovar o próprio deploy por causa de um serviço que não é seu",
  "`curl -s` transforma falha de conexão em saída vazia"; `PADROES` §10.8, §10.9, §10.15
  (o laço espera pela **própria verificação** que você vai fazer), §10.26, §10.4;
  `.github/workflows/ci.yml`, o comentário do bloco de deploy.

  **Prompt:**
  ```
  Acrescente ao job de deploy um passo IDEMPOTENTE que declara, pela management API do
  plataforma-rabbitmq, a topologia da secao 5 do ARQUITETURA pelo lado do consumidor:

    exchange `prices`            (topic, durable=true, auto_delete=false,
                                  internal=false, SEM argumentos extras)
    fila     `custodia.prices`   (quorum, durable)
    bindings prices.# · corpactions.# · eod.ready · trades.registered
    exchange `custodia.dlx`       (NOSSO, FANOUT durable) -> fila `custodia.prices.dlq`
    exchange `custodia.parking`   (NOSSO, FANOUT durable) -> fila `custodia.parked`
    exchange `custodia.retry.in`  (NOSSO, FANOUT durable) -> fila `custodia.retry`
    exchange `custodia.retry.dlx` (NOSSO, FANOUT durable) -> fila `custodia.prices`
    fila     `custodia.retry`    (quorum, durable, SEM CONSUMIDOR) com
                                  x-message-ttl = 30000 e
                                  x-dead-letter-exchange = custodia.retry.dlx

  Os argumentos literais do `prices` acima foram conferidos contra o que o hub-precos
  declara hoje (../hub-precos/src/Hub.Infrastructure/Messaging/
  RabbitMqConnectionProvider.cs: ExchangeDeclareAsync(Exchange, ExchangeType.Topic,
  durable: true, autoDelete: false), sem arguments). CONFIRA de novo antes de declarar.

  Argumentos da fila principal, decididos AGORA porque sao praticamente imutaveis
  (mudar exige apagar a fila, e apagar depois desta fase perde o backlog):
    - SEM x-max-length e SEM x-message-ttl. Teto com reject-publish envenenaria o relay
      do operacoes e do hub, que sao servicos de TERCEIROS (PADROES 10.26); TTL descarta
      em silencio, que e o defeito que esta fase existe para impedir.
    - COM x-delivery-limit explicito e x-dead-letter-exchange = custodia.dlx. Em quorum
      queue, limite estourado SEM dead-letter DESCARTA a mensagem. Verifique o valor
      default contra o rabbitmq:4 REAL antes de escolher o numero (PADROES 10.9:
      comando literal, nao memoria — o default mudou entre versoes).
    - Os QUATRO exchanges nossos sao NOSSOS, nunca o `prices` compartilhado:
      dead-letter de volta para um topic exchange de terceiros republicaria com a
      routing key original e vazaria o nosso retry para todo consumidor futuro.
    - E os quatro sao FANOUT, e isso e CORRECAO DE BURACO, nao detalhe: mensagem
      dead-letrada CONSERVA A ROUTING KEY ORIGINAL (trades.registered, corpactions.td,
      ...). Um custodia.dlx `topic` com binding que nao case tudo descarta a mensagem
      morta EM SILENCIO dentro da NOSSA infraestrutura — o defeito que esta fase existe
      para impedir, reproduzido do lado de ca. Fanout ignora a routing key por
      construcao. NAO use topic com binding `#`: funciona, mas depende de um binding
      estar certo, e o Pronto "o binding existe" fica verde com o binding errado.
    - A fila `custodia.retry` NAO TEM CONSUMIDOR de proposito: ela e um atraso. O
      consumidor do F4 publica o estorno orfao nela pelo custodia.retry.in e confirma a
      mensagem original (liberando a cabeca da fila); passado o TTL, o broker devolve a
      mensagem a custodia.prices sozinho, pelo custodia.retry.dlx. Isso e o que torna a
      Decisao A executavel — `nack(requeue: true)` com prefetch 1 e consumo serial e
      LIVELOCK DE CABECA DE FILA: o trade original que curaria a condicao esta ATRAS do
      orfao, na MESMA fila, e nunca e entregue. O TTL de 30 s e o teto de 10 voltas dao
      ~5 min antes de estacionar; CONFIRA o intervalo real do relay do ../operacoes com
      o comando literal (PADROES 10.9) antes de fixar o numero.
      O RISCO DESTA FILA NAO E O TTL — E A ESTRATEGIA DE DEAD-LETTERING, e trocar um pelo
      outro faz a proxima pessoa concluir a coisa errada (PADROES 10.9). x-message-ttl
      por fila e dead-letter por expiracao FUNCIONAM em quorum queue. O que nao e seguro
      e o DEFAULT: a estrategia de dead-lettering de quorum queue e `at-most-once`, e ela
      pode DESCARTAR a mensagem durante o dead-letter — a perda silenciosa que esta fase
      existe para impedir, agora dentro da NOSSA infraestrutura. Duas saidas, escolha
      MEDINDO contra o rabbitmq:4 real: (i) declarar x-dead-letter-strategy:
      at-least-once na custodia.retry, o que EXIGE x-overflow: reject-publish nela —
      aceitavel porque o unico publicador dessa fila somos nos e o reject volta como
      CONFIRM NEGADO — mas so pelo RAMO PERSISTENTE da Decisao A do F4 (teto de
      tentativas de republish e, estourado, parking com motivo `retry_indisponivel`).
      O tratamento ingenuo do confirm negado (nack e repete) com a custodia.retry CHEIA
      e laco quente + DLQ, os dois desfechos que este arquivo rejeita: se voce escolher
      (i), o ramo persistente NAO E OPCIONAL; ou (ii) custodia.retry nasce
      `classic` durable — fila de ATRASO, sem consumidor e sem dado proprio, o unico
      lugar deste desenho onde classic e aceitavel. ESCREVA NO FECHO QUAL FOI E POR QUE,
      com a razao dita como "estrategia de DLX", NUNCA como "quorum nao suporta TTL",
      que e falso.

  NAO IMPLEMENTE CONSUMIDOR. Nada de basic.consume, nada de codigo .NET de consumo,
  nada de RabbitMq__* no compose. A fila ACUMULA de proposito ate o F4 — o operacoes ja
  publica trades.registered em PRODUCAO desde 2026-09-06, e evento em exchange topic sem
  binding casando e descartado EM SILENCIO com o produtor marcando sucesso.

  A verificacao no deploy substitui o que o CI perdeu de proposito (leia o comentario no
  bloco de deploy do .github/workflows/ci.yml). Ela roda de um container cliente NA REDE
  `plataforma`, nunca por loopback (PADROES 10.3), com `curl -sS -w '%{http_code}'` —
  nunca `curl -s`, que transforma falha de conexao em saida vazia. ANTES de classificar
  o broker como inacessivel, LACO DE ESPERA com timeout (PADROES 10.15): REPETE em
  connect-refused, FALHA RAPIDO em 401 — o incidente da 10.15 e literalmente
  `curl: (7) failed to connect` 6 s depois do "Started", e sem o laco um blip de
  segundos deixa a topologia por declarar com o deploy verde. Desfecho ASSIMETRICO:
    401                                  -> REPROVA rapido (secret deste repo
                                            divergindo), sem repetir
    broker inacessivel APOS O LACO       -> ::warning:: e segue (servico do hub-precos;
                                            exit 1 faria o guarda-deploy afirmar "nao
                                            chegou a producao" com o container healthy)
                                            — E SO ENQUANTO O ALERTA DE TOPOLOGIA
                                            EXISTIR (veja abaixo). Sem alerta, REPROVA.
    autenticou mas fila/binding ausente  -> REPROVA (a declaracao e nossa)
    exchange `prices` presente com       -> REPROVA e NAO REDECLARA. Redeclarar com
    propriedades DIVERGENTES                argumento diferente quebra o publish do Hub
                                            e do Operacoes no proximo boot deles (406):
                                            seria reproduzir do lado do PRODUTOR o dano
                                            que esta fase impede. A divergencia e do
                                            dono do exchange; a correcao e no repo dele.

  Prova de fumaca: publique com routing key `prices.smoke` (casa prices.#) e confirme
  que messages_ready subiu; retire com basic.get + ack DA MENSAGEM ESPECIFICA. NUNCA
  publique com routing key `trades.registered` no exchange `prices` (iria para o livro
  append-only no F4) e NUNCA use purge (e da fila inteira, e apagaria o backlog real que
  esta fase existe para preservar). E NAO exija messages_ready == 0 antes de publicar:
  esta fase diz, com todas as letras, que a fila ACUMULA DE PROPOSITO trades.registered
  real desde o instante em que o binding existe — exigir zero reprovaria o deploy assim
  que o primeiro trade chegasse, e o proprio Pronto manda rodar o deploy duas vezes. A
  prova de uma fila que acumula e de DELTA: registre `antes`, publique, exija
  `depois == antes + 1`, retire a mensagem especifica, exija `final == antes`.

  ALERTA DE TOPOLOGIA — INCONDICIONAL NESTA FASE, e e ele que autoriza o ::warning::
  acima. MECA primeiro se o plataforma-rabbitmq ja e raspado pelo alloy; nao especule.
    (1) se for  -> a regra `custodia-topologia-ausente` entra AGORA em
                   rules-custodia.yaml, ancorada em
                   absent(rabbitmq_queue_messages_ready{queue="custodia.prices"});
    (2) se nao for -> o F2 TORNA-O RASPADO: acrescente o alvo em
                   ../tesouro-direto-api/infra/alloy/config.alloy, do mesmo jeito que o
                   F1 acrescentou o da custodia. E A 10.9 SE APLICA A SERIE, NAO AO
                   ENDPOINT: conferir que o endpoint de metricas responde prova que o
                   plugin esta habilitado e NAO prova que a serie da sua regra existe.
                   O comando literal a rodar e o que procura
                   `rabbitmq_queue_messages_ready{queue="custodia.prices"}` NO CORPO da
                   resposta, com a fila ja declarada;
    (3) se o endpoint nao existir, ou existir SEM ESSA SERIE, e nao puder ser habilitado
                   (container de OUTRO repo)
                -> nao ha regra possivel, e entao o passo passa a REPROVAR tambem no
                   caso "broker inacessivel", com o motivo escrito no comentario do step.
  NAO ANCORE A REGRA EM CONTAGEM DE BINDINGS DO `prices`, como dizia a versao anterior
  desta fase, e sao dois motivos: (i) o rabbitmq_prometheus expoe series POR FILA e
  contagens globais, e NAO expoe contagem de bindings POR EXCHANGE — a regra nasceria
  sobre uma serie que nunca existiu, disparando todo dia para sempre, que e o alerta
  permanente que ninguem olha; (ii) a contagem de bindings do `prices` MUDA QUANDO OUTRO
  SERVICO BINDA UMA FILA NOVA (a secao 5 preve isso), e seria alerta NOSSO disparando por
  mudanca ALHEIA. A LACUNA QUE SOBRA E DECLARADA: a fila existir nao prova que os quatro
  bindings existem. O incidente real (volume do broker perdido) leva fila E bindings
  juntos, entao a ausencia da serie cobre esse caso; a remocao cirurgica de um binding com
  a fila intacta fica coberta pela VERIFICACAO BLOQUEANTE DO DEPLOY, que le a management
  API, onde binding E conferivel. Escreva isso no comentario da regra.
  A regra NAO PODE ficar adiada para o F4: entre o F2 e o F4 a fila pode nao existir com
  tudo verde, que e exatamente a janela que esta fase existe para fechar.

  ONDE O ARQUIVO DE REGRA TEM QUE EXISTIR PARA CHEGAR A NUVEM: depois do F1 ha DUAS
  copias de rules-custodia.yaml — a deste repo e a de
  ../tesouro-direto-api/infra/grafana/cloud/ — e SO A SEGUNDA e lida pelo apply-cloud.sh.
  Edite as DUAS, rode o apply-cloud.sh com GC_GRAFANA_URL, GC_GRAFANA_TOKEN e
  TELEGRAM_BOT_TOKEN exportados E COM O VALOR DO TOKEN CONFERIDO (a guarda ${VAR:?} so
  testa vazio), e prove pela API do Grafana Cloud que a regra esta PUBLICADA, NA PASTA
  Custodia — nao que o YAML foi editado. No hub os alertas ficaram semanas sem existir na
  nuvem por isso, com o publicador pulando o bloco em silencio. Se a regra nova entrar num
  grupo novo, confira que o bloco de CONFERENCIA DA CONTAGEM do apply-cloud.sh (o que le
  o numero de regras do proprio YAML) continua batendo — ele aborta o script quando nao
  bate, e isso e desejavel.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem /// — se algum .cs
  nascer aqui. Nome de metodo, nome de teste e estrutura carregam o que o comentario
  carregaria; isto vale contra o default do seu treino. Shell e YAML podem ter
  comentario — e devem, no criterio de desfecho assimetrico acima.

  Configuracao: mudam DUAS listas — secrets do broker no GitHub, no `envs:` do
  ssh-action E NO BLOCO `env:` DO MESMO STEP (o que mapeia ${{ secrets.* }}). As duas
  superficies andam juntas: so no `envs:` a variavel e encaminhada INEXISTENTE, chega
  como string vazia no script e a guarda `-z` acusa "secret vazio" com o secret
  cadastrado e correto — e o operador vai procurar no unico lugar onde o defeito nao
  esta. Normalize com `tr -d '\r\n'` NA ORIGEM (PADROES 10.4). NAO entram nos composes,
  NAO entram no printf do .env, NAO entram nas dummies do config gate: nada no compose
  as referencia, e `:?` que ninguem le derruba o `up` e o `config -q` sem causa.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (permissiva) `GET /api/queues/%2F/custodia.prices` responde 200 com
  `type: quorum` e `durable: true`, os argumentos batendo com os decididos;
  `GET /api/exchanges/%2F/prices/bindings/source` lista os **quatro** routing keys; a
  `custodia.prices.dlq`, a `custodia.parked` e a `custodia.retry` existem, ligadas aos
  exchanges **nossos**, e a `custodia.retry` traz `x-message-ttl` e
  `x-dead-letter-exchange` batendo com os decididos.
  (**Estrita**) a MESMA checagem **reprova** quando alimentada com um binding inventado, e
  reprova quando um binding real é removido à mão — controle negativo **e** positivo, sem
  os quais a asserção passa também quando o mecanismo de detecção quebrou (§10.8). Mais:
  (a) **prova de que o fanout não engole nada** — publicar no `custodia.dlx` com uma
  routing key arbitrária que nenhum binding casaria (`chave.que.ninguem.binda`) e provar
  que a mensagem **aparece** na `custodia.prices.dlq`; mesmo controle para
  `custodia.parking` → `custodia.parked`. Sem isso o F4 dead-letra para um buraco, e o
  Pronto "existem, ligadas" fica verde com a mensagem sendo descartada em silêncio;
  (b) **o retry fecha o ciclo, não é buraco:** uma mensagem publicada no
  `custodia.retry.in` com routing key `prices.smoke` sai da `custodia.retry` **depois do
  TTL** e chega na `custodia.prices` — e é retirada de lá com `basic.get`+ack da mensagem
  específica ao fim da prova (`prices.smoke` de propósito: se a limpeza falhar, a
  mensagem estaciona no F4 em vez de virar linha de livro);
  (c) a prova de fumaça por **DELTA**, nunca por valor absoluto: `messages_ready` anotado
  **antes**, `depois == antes + 1` após publicar `prices.smoke`, `final == antes` após o
  `basic.get`+ack. Exigir `messages_ready == 0` seria reprovar o deploy assim que o
  primeiro `trades.registered` real chegasse — e esta fase existe para que ele chegue;
  (d) o deploy roda **duas vezes seguidas** com o mesmo resultado — idempotência provada,
  não suposta;
  (e) a regra de topologia **publicada na nuvem**, na pasta **Custodia**, conferida pela
  API do Grafana Cloud (e não pelo YAML editado), com o arquivo rastreado no
  `../tesouro-direto-api` e **lido pelo bloco `if -f` do `apply-cloud.sh`** — não "citado
  na lista", que não existe para serviço vizinho (ver os cinco pontos no F1);
  (f) **o caminho de `::warning::` exercitado sem derrubar serviço de ninguém.** O
  `plataforma-rabbitmq` é do `hub-precos` e serve o relay dele — derrubá-lo de propósito
  dispara `hub-relay-falha-persistente` no plantão de outro serviço, que é precisamente o
  "reprovar/estragar o alheio por causa de um serviço que não é seu" que esta fase invoca
  para justificar o `::warning::`. **Decidido: a prova é em duas partes, nenhuma
  destrutiva.** (i) O passo de deploy é rodado uma vez apontado para um **host
  inalcançável** (endereço de management API inexistente, por variável, numa execução
  manual): prova que o laço de espera roda, que o desfecho é `::warning::` e não `exit 1`,
  e que o deploy segue verde. (ii) O alerta de topologia é provado em **duas metades, e a
  primeira é CONTROLE POSITIVO, sem o qual a segunda passa por vacuidade**: primeiro, com a
  `custodia.prices` declarada e o broker no ar, a série
  `rabbitmq_queue_messages_ready{queue="custodia.prices"}` **está presente** — conferida na
  consulta do Grafana Cloud, não no arquivo de regra — e a regra avalia em **OK**; só então
  a segunda metade, `noDataState` configurado para disparar, é aceita como prova do estado
  "broker fora". *Sem o controle positivo, uma regra construída sobre uma série que **nunca
  existiu** satisfaz a prova perfeitamente — e depois dispara todo dia, para sempre. Era
  esse o buraco da versão anterior deste Pronto, e ele casava com a escolha de sinal
  (contagem de bindings) que a fase agora rejeita nominalmente.* *Rejeitado:*
  derrubar o broker de propósito durante a janela; o estrago é no plantão do vizinho e a
  §10.8 já é satisfeita pelo controle negativo acima. Se **nenhuma** das duas partes for
  executável, a linha 2 da tabela de desfecho **não está autorizada** e o passo passa a
  reprovar também no caso "broker inacessível".

- [ ] **F3** — o schema do livro: as constraints que tornam o dado irreparável impossível
  de gravar. **Dependência externa nova: Postgres com schema (a instância já existe).**
  **PENDÊNCIA BLOQUEANTE ABERTA: `caixa:BRL` nunca é debitado — leia a pendência dentro da V2
  ANTES de despachar; ela depende de um campo novo na §5.1 do `../plataforma-docs`.**

  As **cinco** tabelas da §7.1 — o livro e as quatro projeções, e o "cinco" é sobre a §7.1,
  não sobre o schema inteiro do serviço — como migrations EF, snake_case, índices nomeados, no molde
  de `../operacoes/src/Operacoes.Infrastructure/Persistence/`: `movimentos` (o livro),
  `posicao_corrente`, `preco_atual`, `historico_precos` e `snapshots_posicao`. Trigger de
  imutabilidade em `movimentos`. O conjunto **estrito** de CHECKs. **O vocabulário do
  livro fechado por inteiro** — os `tipo`, o mapeamento a partir da §5.1, a convenção
  completa de `ref_externa` e os ids de caixa. `/health/ready` endurecido. Constantes de
  precisão/escala com teste contra o `information_schema`.

  **São cinco e não quatro, e a quinta não é invenção:** a §7.1, logo depois da DDL de
  `preco_atual`, diz que "se a Custódia mantiver também histórico local de preços
  (**recomendado** para recálculo sem depender do Hub online), a tabela espelha a canônica
  com a mesma chave". A decisão "histórico local SIM" era do F6 no rascunho anterior, e a
  tabela simplesmente **aparecia** lá, com escritor já ligado — a condição exata que esta
  fase usa como argumento a favor de si mesma ("no F3 não existe escritor nenhum"). Ela
  desce para cá **com a chave inteira**, `(instrumento_id, data_ref, campo, fonte,
  revisao)`, que é a chave de dedupe do consumidor da §5.1: note que `preco_atual`, pela
  DDL da §7.1, **não tem coluna `fonte`** — sem a tabela de histórico definida, a chave da
  §5.1 não tem onde morar inteira, e o dedupe do F6 nasceria mutilado.

  **Por que aqui e não junto do consumidor:** em tabela append-only com trigger, a
  assimetria de custo entre restringir demais e restringir de menos **se inverte**
  (§10.21). Sair de constraint estrita demais é um `DROP CONSTRAINT` — migration de uma
  linha, sem rewrite, que nunca invalida linha existente. Sair de constraint que faltou
  exige `DISABLE TRIGGER` mais perícia manual, e o que já foi projetado rio abaixo não
  volta. E no F3 **não existe escritor nenhum**, então nada pode ser gravado errado
  enquanto ele não existir — o que torna esta a última fase barata para acertar **o schema do
  livro e das quatro projeções da §7.1**. **O escopo é esse, e é literal:** a assimetria da
  §10.21 vale onde o dado errado é irreparável — tabela append-only com trigger, ou projeção
  cuja chave de dedupe o consumidor herdaria mutilada. **Tabela que não está na §7.1, não é append-only e não tem trigger — seja de
  CONTROLE mutável, seja de CONFIGURAÇÃO semeada por migration — fica FORA deste argumento**
  e pode nascer na fase que a decide. **São DUAS neste roadmap, e nomeá-las é parte do
  escopo:** o `eod_processado` do **F7** (controle de consumo mutável, cuja forma depende de
  decisões daquela fase — a chave, as duas colunas e o significado de `U` nulo) e o
  **calendário de dias úteis** do **F5** (configuração, populada por migration/seed, que não
  se reconstrói do livro). As duas estão rotuladas nas fases delas. *"As tabelas são cinco" e
  "a última fase barata" leem-se com esse escopo — sem ele, as duas frases afirmam cobertura
  sobre o schema inteiro do serviço, que esta fase não tem como dar. E a enumeração das
  exceções é parte do escopo: uma versão anterior nomeava só o `eod_processado`, e o
  calendário do F5 — que nem sequer cabe na forma "tabela de controle mutável" — ficava de
  fora sem que nada apontasse a omissão.*

  ---

  **O VOCABULÁRIO DO LIVRO — fechado aqui, repetido literalmente no F4, F5, F7 e F9.**
  *Com uma exceção declarada, e ela é o motivo de a fase estar bloqueada: a **PENDÊNCIA
  BLOQUEANTE do `caixa:BRL`** (dentro da V2) é a única peça deste vocabulário que ainda não
  fechou, e "fechado aqui" só passa a ser verdade quando ela fechar. Não leia as cinco V como
  completas enquanto o cabeçalho da fase trouxer o aviso.*

  Cinco decisões que se sustentam umas às outras. Corrigir uma sem as outras **recria** as
  demais, e por isso elas estão num bloco só, com um nome só para cada linha. **Fase que
  usar nome diferente para a mesma linha é defeito**, e o `guardiao-padroes` reprova.

  **V1 · O enum de `tipo`, enumerado literalmente — e a dobra das três colunas de
  `posicao_corrente` para cada um dos dez (segunda metade, logo abaixo do enum).**

  ```
  compra | venda | aporte | cupom | resgate | ir_retido | iof | a_liquidar |
  liquidacao | ajuste
  ```

  A §7.1 escreve o enum **sem `aporte`**, e isso é um furo dela: a própria §7.3, três
  seções adiante, descreve o handler gravando "movimento da operação
  (**compra/venda/aporte**, qtd_delta, valor, data_evento)". **Desvio POR CORREÇÃO da
  §7.1, apoiado no texto da §7.3 — registre-o**, na mesma convenção do `ref_externa NOT
  NULL` abaixo. *Rejeitado:* mapear `aporte` para `compra` — economizaria um valor de CHECK
  e tornaria o aporte **indistinguível de uma aplicação** no extrato de movimentação, que é
  precisamente a superfície auditável desta casa ("`SELECT` do livro, não relatório
  calculado", §7.5). O que separa os dois é **só o rótulo** (V2), e o rótulo é o que o
  cliente declarou: apagá-lo é apagar informação para sempre, numa tabela sem UPDATE. *Rejeitado:* a taxonomia completa da B3 no dia 1 — cada valor a mais é um valor
  no CHECK de uma tabela append-only que ninguém exercita (§9 da `ARQUITETURA`, nota final;
  dividendo/desdobramento/grupamento/bonificação são fase 2).

  **A DOBRA DAS TRÊS COLUNAS DE `posicao_corrente`, POR TIPO, PARA OS DEZ — é a SEGUNDA
  METADE DA V1, e as fases a citam com esse nome ("a dobra da V1", ou "a dobra da V1 com
  corte em `data_evento ≤ D`" quando o corte importa), sem símbolo novo.**
  Está aqui, junto do enum, porque é aqui que o vocabulário é fechado — e a regra escrita
  por nome de tipo só é conferível se cobrir todos os nomes. A versão anterior deste arquivo
  escrevia a regra em duas frases ("na compra…, na venda…") e deixava **oito** tipos sem
  dobra definida, enquanto o I4 afirmava que as três colunas são "dobra pura do livro".
  Uma implementação `switch (tipo) { case compra: …; case venda: …; }` é a leitura literal
  daquele texto, e ela **infla o preço médio para sempre** no `aporte` e **corrompe** o
  preço médio no `ajuste` de uma venda. Os dois casos estão nos números abaixo.

  | `tipo` | `quantidade` | `custo_total` | `preco_medio` |
  |---|---|---|---|
  | `compra` | `+= qtd_delta` | `+= valor_financeiro` | `= custo_total / quantidade` |
  | `aporte` | `+= qtd_delta` | `+= valor_financeiro` | `= custo_total / quantidade` |
  | `venda` | `+= qtd_delta` (negativo) | `= preco_medio × quantidade` (baixa proporcional) | **inalterado** |
  | `resgate` (vencimento) | `+= qtd_delta` (negativo) | `= preco_medio × quantidade` | **inalterado** |
  | `cupom` | `+= 0` | **inalterado** | **inalterado** |
  | `ir_retido` · `iof` · `a_liquidar` · `liquidacao` | `+= qtd_delta` | `= quantidade` | `= 1,000000` |
  | `ajuste` | `+= qtd_delta` | **recalculado** (regra abaixo) | **recalculado** (regra abaixo) |

  **A dobra tem um PARÂMETRO DE CORTE, e ele faz parte do nome: "a dobra da V1 com corte em
  `data_evento ≤ D`".** Ela percorre **só** as linhas do livro com `data_evento ≤ D`.
  `posicao_corrente` é o caso **`D = ∞`** — é isso, e só isso, que o "sem filtro por
  relógio" do I4 quer dizer; o snapshot de um dia (F7) é o caso **`D = aquele dia`**, nas
  **três** colunas; e a posição na data de uma corpaction (F9) é o mesmo caso, precisando
  só da primeira. **É uma implementação com um parâmetro, nunca duas.** Sem o parâmetro,
  "copiar as três colunas de `posicao_corrente` para o snapshot de um dia passado" parece
  obediência à regra única e grava `quantidade` de `D` com `preco_medio` e `custo_total` de
  **hoje**: violaria `custo_total = preco_medio × quantidade` em **todo** snapshot
  histórico, e um movimento retroativo reescreveria o preço médio da série inteira — no
  documento que o cliente vê (P2).

  **A ORDEM DE PERCURSO é parte da definição da dobra, e ela é `data_evento` crescente,
  desempate `registrado_em`, desempate final `id`.** Não é detalhe de implementação: a média
  ponderada **é sensível à ordem**, e a mesma cesta percorrida em ordens diferentes dá preços
  médios diferentes — `compra 10 @ 100`, `venda 5`, `compra 10 @ 200` dá `pm 166,67` na ordem
  escrita e `pm 150` se a segunda compra for anterior às outras duas. Sem a ordem declarada,
  "a dobra do livro" não nomeia **uma** função: nomeia uma família de funções que discordam
  entre si, e o F4 (que aplica na ordem de chegada), o F7 (que precisa percorrer por
  `data_evento`) e a reconciliação (que compara os dois resultados) escolheriam cada um a
  sua. As duas primeiras chaves são as **mesmas** da §7.5, de propósito: o extrato e a dobra
  leem o mesmo livro na mesma ordem, e divergir aqui seria explicar ao cliente por que o
  preço médio não bate com a lista de movimentos que ele acabou de ler.
  **Por que existe a terceira chave:** `registrado_em` **não** desempata sozinho. Das seis
  linhas derivadas de um resgate (V2), **quatro** nascem na **mesma transação** — a `venda`,
  o `ir_retido`, o `iof` e o `a_liquidar`, as quatro com `data_evento = dataEvento` —, e
  `now()` no Postgres é hora de **transação**: as **três** dessas quatro que caem em
  `caixa:a_liquidar` compartilham `data_evento` e `registrado_em` byte a byte. *As duas
  pernas de `liquidacao` NÃO estão nesse grupo — elas são de **outra** transação, gravadas em
  **D+1 útil** por um job, que é o desvio (1) da lista canônica (F5). A premissa "as seis
  nascem na mesma transação" é falsa e contradizia aquele desvio; a conclusão sobrevive
  inteira pelas três linhas de `caixa:a_liquidar`, que bastam para o empate existir.* Hoje
  esse empate é inofensivo (em `caixa:*` a V4 fixa
  `preco_medio = 1,000000` e `custo_total = quantidade`, que não dependem de ordem), mas
  **a dobra é definida como função total, e não pela inofensividade do empate de hoje** — o
  `id` fecha a ordem.
  **E é a MESMA tripla que o F8 usa para ORDENAR e para PAGINAR o extrato de movimentação:**
  `(data_evento, registrado_em, id)`, nas duas coisas. *A versão anterior desta frase dizia
  "é o mesmo par `(data_evento, id)` que o cursor do F8 já usa", e ela era falsa duas vezes:
  o cursor não é a chave da dobra, e `(data_evento, id)` **remove** `registrado_em` da chave
  em vez de refiná-lo. `registrado_em` é `now()`, hora de **transação**; `id` é `bigserial`
  alocado na execução do INSERT — dois escritores concorrentes na mesma
  `(cliente, instrumento, data_evento)` ordenam-se ao contrário nos dois campos, e o segundo
  escritor existe **por desenho** a partir do F5 (o `IHostedService` de liquidação escreve
  enquanto o consumidor de eventos escreve). Ordenar por uma chave e paginar por outra pula
  ou duplica linha quando a fronteira de página cai dentro de um grupo empatado — e o grupo
  empatado não é hipotético: são as três linhas de `caixa:a_liquidar` de **todo** resgate.
  Decisão, e ela é do F8 também: uma chave só, a tripla.*

  **Consequência para o F4, e ela está escrita lá também:** a aplicação **incremental** em
  O(1) só é a mesma função quando o movimento que chega é o **último** da chave nessa ordem,
  isto é, `data_evento ≥ MAX(data_evento)` daquela `(cliente_id, instrumento_id)`. Chegando
  movimento com `data_evento` anterior, o handler **re-dobra a chave inteira**, exatamente
  como já faz para `ajuste`. **Na PRIMEIRA linha da chave o `MAX` é NULO, e a comparação tem
  de ser escrita para esse caso:** em SQL, `data_evento >= NULL` é `NULL` — que o `WHERE`
  trata como falso, mas que uma implementação em C# com `DateOnly?` resolve como
  `NullReferenceException` ou como `false`, e `false` manda a primeira linha de toda chave
  nova para a re-dobra (correto por acidente, e O(1) virando O(n) sem ninguém notar).
  **Regra: `MAX` nulo ⇒ aplica o delta incremental** — não há história anterior, então a
  linha que chega **é** a última da ordem por definição. Sem essa cláusula, `compra 10 @ 100` (D1), `venda 4` (D2) e
  depois a retroativa `compra 10 @ 200` (D0) deixam `posicao_corrente` com `custo 2600` /
  `pm 162,50` enquanto a dobra por data vale `custo 2400` / `pm 150`: **I4 fica falso** e a
  reconciliação do F7 passa a alertar para sempre sobre um movimento retroativo legítimo —
  que é o caso que o próprio F7 chama de típico.

  **`data_evento` do `ajuste`: é o da linha revertida, e a decisão é desta seção.** A V2 já
  fixa o `ajuste` como o **simétrico exato** da revertida nas duas colunas de valor; a data
  entra na mesma simetria, e pelo mesmo motivo — o estorno declara que aquele fato **não
  aconteceu**, e a data em que ele deixou de valer é a data em que ele teria acontecido, não
  a data em que alguém percebeu o erro. Quem registra **quando**
  a correção chegou é `registrado_em`, coluna própria e desempate da §7.5: a linha continua
  visível no extrato de movimentação, com a data do fato e a hora do registro.
  *Rejeitado:* carimbar o `ajuste` com a data de chegada do estorno — o snapshot de um dia
  passado continuaria exibindo um trade cancelado **para sempre**, porque o gatilho 1 do F7
  dispara `recalcular(desde = data_evento do movimento)` e, com `data_evento = hoje`, ele
  nunca volta ao dia errado. Com a data herdada o `ajuste` **é** um movimento retroativo, o
  gatilho 1 o alcança, e os snapshots do intervalo são reversionados.
  **Fronteira do corte no par revertido, e agora ela é consequência e não restrição:** o par
  (linha revertida, `ajuste` que a reverte) só sai da dobra quando **as duas** linhas têm
  `data_evento ≤ D` — e, com a data herdada, as duas caem **sempre** do mesmo lado de
  qualquer corte. A regra fica escrita assim mesmo porque é ela que reprova a implementação
  que carimba o `ajuste` com a data de chegada: essa implementação quebra as duas coisas de
  uma vez — parte o par no corte e apaga o gatilho 1. *Não procure teste em que só a metade
  revertida do par esteja dentro do corte: com esta decisão ele não existe, e é por isso que
  a fronteira é enunciada como consequência da simetria, não como caso a exercitar.*

  Com a ordem declarada e a cláusula do F4, a dobra é função **do livro e de `D`**, e a ordem
  de chegada deixa de ser parâmetro — que é o que o I4 afirma. *A frase é forte, então o
  contraexemplo tentado fica escrito: duas linhas da mesma chave com `data_evento` e
  `registrado_em` idênticos percorridas em ordens diferentes dariam resultados diferentes se
  a dobra fosse sensível à ordem nesse ponto; é o empate que o desempate por `id` fecha.*

  **A REGRA DE FRONTEIRA DE VALOR, e ela é obrigatória porque duas linhas da tabela acima
  escrevem uma divisão.** `preco_medio` só é **definido** para `quantidade > 0`, e a divisão
  `custo_total / quantidade` só acontece nesse ramo. **A regra parte pelo par (sinal da
  `quantidade` ANTERIOR, sinal da `quantidade` RESULTANTE), e são três casos de fronteira mais
  o regime normal** — resultante `= 0` (qualquer anterior); resultante `< 0`; resultante `> 0`
  vindo de anterior `≤ 0` (o **cruzamento**); e resultante `> 0` vindo de anterior `> 0`, que é
  a tabela por tipo acima. *A partição é sobre os dois sinais, e por isso não sobra combinação:
  o contraexemplo tentado foi um `cupom` (`qtd_delta = 0`) sobre posição negativa — resultante
  `< 0`, cai no segundo caso, e o `custo_total` fica inalterado pela regra do tipo.* Nenhum dos
  três é hipotético:

  - **`quantidade` resultante = 0, para QUALQUER tipo:** `preco_medio = 0` e
    `custo_total = 0`. É estado **canônico e independente do caminho** — o mesmo a que a
    baixa proporcional da venda chega (`custo = pm × 0`) — e é ele que impede
    `custo_total / 0` numa `compra`/`aporte` que traz a posição **de negativa para zero**.
    O F4 admite `quantidade < 0` no livro e prescreve como conserto **exatamente** a compra
    que a zera: o caso está no caminho normal, não na borda.
  - **`quantidade` resultante < 0:** `preco_medio` fica **INALTERADO** — nunca recalculado,
    portanto **nunca negativo** —, e `custo_total` acumula pela regra do tipo. Sem esta
    linha, `venda sem compra` de 10 seguida de `compra 4 @ 100` daria `custo_total = 400`,
    `quantidade = −6` e `preco_medio = −66,67`, e o F5 tributaria sobre preço médio negativo.
    *("Base = preço médio **do livro**", §7.3, é **elipse**: o preço médio é o **insumo** da
    base, e a base é o **ganho** (`valorFinanceiro − pm × quantidade`). A fórmula está fechada
    na decisão da base do F5 — não a deduza desta frase.)* Enquanto a quantidade for negativa as
    três colunas são **provisórias**: é o estado que a camada 3 do F4 já sinaliza (métrica +
    log + alerta) como "falta lançar a compra antiga", e lançá-la refaz as três pela mesma
    dobra. **"Provisórias" vale para `posicao_corrente` e NÃO vale para o livro** — a
    projeção se refaz por dobra, mas o `ir_retido`/`iof` que o F5 tiver gravado a partir de
    um `preco_medio` provisório está em tabela append-only e **só sai por estorno**. É por
    isso que o F5 tem decisão própria sobre tributar em cima de coluna provisória, e não
    herda esta frase como tranquilidade.
  - **`quantidade` anterior `≤ 0` e resultante `> 0` — o CRUZAMENTO, e ele é o conserto MAIS
    provável, não a borda mais rara:** `preco_medio` = o **preço unitário da própria linha
    que cruza** (`valor_financeiro / qtd_delta`), e `custo_total = preco_medio × quantidade`
    resultante. A posição **recomeça** ali: a parte da compra que cobre o negativo está
    pagando uma venda que nunca foi lançada, e não é custo de aquisição do que sobrou.
    Sem esta linha, "falta lançar a compra antiga" — que o F4 prescreve como conserto —
    costuma ser lançada **maior** que a venda, e a regra ingênua acumula: `venda sem compra`
    de 10 seguida de `compra 20 @ 100` daria `quantidade 10`, `custo_total 2000` e
    `preco_medio 200`, quando a dobra por data vale `pm 100` e `custo 1000`. **Esse erro
    passa nas duas guardas que já existem:** satisfaz `custo_total = preco_medio ×
    quantidade` (`200 × 10 = 2000`) e satisfaz `preco_medio ≥ 0`. E é o `preco_medio` que o
    F5 usa como base de IR. *A regra vale para `compra` e `aporte`, os únicos tipos que
    cruzam para positivo aplicando delta: `cupom` tem `qtd_delta = 0` e não cruza; as linhas
    de `caixa:*` são fixadas pela V4 e estão fora desta regra; e o `ajuste`, que também pode
    ter `qtd_delta > 0`, nunca aplica delta — ele re-dobra a chave inteira, e o cruzamento
    aparece lá dentro, linha a linha, por esta mesma regra.* **A fronteira `= 0` vem antes
    desta:** resultante exatamente 0 é `preco_medio = 0`, estado canônico, e não o preço
    unitário da linha.

  **O invariante conferível NÃO pega estas fronteiras — é por isso que elas são regra escrita
  e teste, e não corolário.** `custo_total = preco_medio × quantidade` é satisfeito
  **algebricamente** por `−66,67 × −6 = 400` e por `200 × 10 = 2000`: ele passa com o preço
  médio errado nos dois. Ele é
  afirmado para `quantidade > 0` (e vale trivialmente em 0); para `quantidade < 0` ele **não
  é afirmado** — e para o **cruzamento** ele é afirmado e **passa mesmo assim**, porque o par
  (`pm` errado, `custo` errado) é consistente entre si. Quem pega os valores errados é o **teste obrigatório dos TRÊS desfechos** da
  mesma `venda sem compra` de 10: → **compra que zera** (10 @ 100 → `quantidade 0`,
  `preco_medio 0`, `custo_total 0`, sem exceção de divisão); → **compra que não zera**
  (4 @ 100 → `quantidade −6`, `custo_total 400`, `preco_medio` inalterado); e → **compra que
  cruza** (20 @ 100 → `quantidade 10`, `preco_medio 100`, `custo_total 1000`, jamais
  `pm 200`). Mais a asserção `preco_medio ≥ 0` **sempre**. *O terceiro é o único que o
  invariante e a varredura de sinal **ambos** aprovam com o valor errado — é por isso que ele
  é teste nomeado e não corolário.*

  Quatro linhas dessa tabela precisam do porquê escrito, senão viram opinião:

  1. **`aporte` dobra EXATAMENTE como `compra`.** É o corolário direto da V2 ("a diferença
     é SÓ O RÓTULO"), e está escrito aqui porque a regra por nome de tipo não o deduz:
     `aporte` não é `compra`, e uma implementação que só conhece os dois nomes da frase
     antiga aumenta a `quantidade` (via Σ `qtd_delta`) e **não** mexe no `custo_total` —
     `preco_medio` inflado, para sempre, numa coluna que o F5 usa como base de IR e o F7
     grava em cada snapshot.
  2. **`cupom` não toca `custo_total` nem `preco_medio`.** Ele tem `qtd_delta = 0` e
     `valor_financeiro > 0` no instrumento do título: é **renda**, não custo de aquisição.
     Somar o `valor_financeiro` ao custo mudaria o preço médio **sem que a posição mudasse**
     — e, com a posição zerada no mesmo dia, seria **divisão por zero**.
  3. **`caixa:*` é definição, não caso especial.** Para as quatro linhas de caixa a V4
     fixa `preco_medio = 1,000000` e `custo_total = quantidade`; não há média ponderada a
     manter, e a regra vale igual para `ir_retido`, `iof`, `a_liquidar` e `liquidacao`
     porque **todas** escrevem em `caixa:*`.
  4. **`venda` e `resgate` mantêm o `preco_medio` e baixam o custo proporcionalmente** —
     que é o mesmo que dizer `custo_total = preco_medio × quantidade`, e é essa forma que
     torna o **invariante conferível**: depois de qualquer `compra`/`aporte`/`venda`/
     `resgate`, **com `quantidade > 0`**, `custo_total = preco_medio × quantidade` (a menos
     do arredondamento de 2 casas do financeiro). Um teste que afirma essa igualdade pega a
     implementação que baixa uma coluna e esquece a outra — e **não** pega preço médio
     negativo, que é por que a regra de fronteira acima existe em separado.

  **A dobra do `ajuste`, e ela é a decisão desta seção.** Um `ajuste` **não** é "uma compra
  quando `qtd_delta > 0`": ele **desfaz** a linha que `ref_estorno` aponta. A dobra é
  definida assim, e vale tanto para a reconstrução em lote quanto para o handler:

  > A dobra das três colunas percorre o livro **ignorando os pares (linha revertida,
  > `ajuste` que a reverte)**, com "revertida" resolvido recursivamente: uma linha é
  > **efetiva** quando não existe `ajuste` **efetivo** apontando para ela. Como o par soma
  > **zero nas duas colunas** (V2), `Σ qtd_delta = quantidade` continua valendo **literalmente**
  > — I4 não muda uma vírgula —, e o que muda é só o cálculo de `preco_medio`/`custo_total`,
  > que deixa de depender da ordem em que o estorno chegou.

  Consequência operacional para o F4: os **nove** outros tipos aplicam-se
  **incrementalmente** em O(1) na mesma transação; **ao gravar um `ajuste`, o handler
  recalcula as três colunas daquela chave `(cliente_id, instrumento_id)` dobrando o livro**
  — a mesma operação do comando de reconstrução, com escopo de uma chave. O custo é
  O(movimentos daquela chave), que a §11 descreve como minúsculo, e a alternativa custa
  correção de dado.

  Os números que **obrigam** essa forma, porque são o caso que a regra ingênua erra:

  ```
  compra 10 @ 100         -> quantidade 10, custo_total 1000, preco_medio 100
  venda   4 por Y = 600   -> quantidade  6, custo_total  600, preco_medio 100
     (o preço subiu para 150; a venda NÃO mexe no preço médio)
  estorno da venda: ajuste com qtd_delta +4 e valor_financeiro -600 (simétrico, V2)
     regra ingênua ("qtd_delta > 0, logo compra"):
        custo_total = 600 + 600 = 1200, preco_medio = 1200/10 = 120   <- ERRADO
     dobra desta seção (o par venda+ajuste sai da dobra):
        quantidade 10, custo_total 1000, preco_medio 100              <- correto
  ```

  A regra ingênua deixa o `preco_medio` **20% errado e permanente**: o F5 tributa com
  "base = preço médio **do livro**" (§7.3) e a próxima venda calcula IR sobre a base errada;
  o F7 grava esse `preco_medio` em **cada snapshot**, que é documento que o cliente vê (P2);
  e o `ajuste` — cuja razão de existir é desfazer — passa a não desfazer.

  *Rejeitado:* **regra inversa por tipo** (o `ajuste` aplica o inverso da dobra do tipo da
  linha revertida, em O(1), sem recálculo). Ela acerta o exemplo acima e **erra sob
  intercalação**, que é o caso comum quando o estorno chega dias depois: `compra 10 @ 100`,
  `venda 4`, `compra 10 @ 200`, `estorno da venda` devolve `custo_total = 3250`, quando o
  livro sem o par vale `3000`. E ela fica ambígua no estorno **de** estorno, que a V5
  permite de propósito — o inverso do inverso teria de ser reconstruído caso a caso. A
  dobra que ignora pares acerta os dois por construção, e é a mesma definição usada pelo
  comando de reconstrução do F4, então **não existem duas regras para divergirem**.
  *Rejeitado:* deixar `preco_medio`/`custo_total` fora da dobra e recomputá-los só sob
  demanda — I4 exige as três colunas na projeção, e o F5 lê a coluna dentro do cálculo de
  imposto.

  **V2 · O mapeamento `operacao` (§5.1) → `tipo` (§7.1). É a tradução entre dois
  vocabulários que NÃO coincidem, e sem ela cada fase inventa o seu.**

  **Convenção de sinal, uma vez para todas as linhas:** `qtd_delta` **carrega o sinal** (o
  que entra na posição é positivo, o que sai é negativo) e `valor_financeiro` é a
  **magnitude bruta** do que aquela linha movimenta, não negativa — é a convenção da §7.1
  (`valor_financeiro`: "bruto da operação") e da §7.3 (`vencimento → qtd_delta = -qtd,
  valor = qtd × PU final`: valor positivo com quantidade negativa). **A única exceção é o
  `ajuste`**, que carrega o **simétrico exato** da linha revertida **nas duas colunas**, de
  modo que o par some **zero em ambas** — é isso que faz "estorno desfaz" ser conferível
  por soma e não por inspeção.

  **As quatro `operacao` da §5.1 — o movimento principal do fato:**

  | `operacao` do `TradeRegistered` | `tipo` no livro | `instrumento_id` | `qtd_delta` | `valor_financeiro` |
  |---|---|---|---|---|
  | `aplicacao` | `compra` | o do evento | **+**`quantidade` | `valorFinanceiro` (bruto) |
  | `resgate` | `venda` | o do evento | **−**`quantidade` | `valorFinanceiro` (bruto) |
  | `aporte` | `aporte` | **o do evento** | **+**`quantidade` | `valorFinanceiro` (bruto) |
  | `estorno` | `ajuste` | o do movimento estornado (por lookup) | simétrico ao estornado | simétrico ao estornado |

`estorno` herda também a **`data_evento`** do movimento estornado — é a V1 ("`data_evento` do
`ajuste`"), repetida aqui porque é nesta tabela que o executor procura os campos da linha.

  **`aporte` é operação COM instrumento — aplicação adicional no título —, e a diferença
  contra `aplicacao` é SÓ O RÓTULO no extrato de movimentação (§7.5). Não procure diferença
  de comportamento: não existe.** Ele grava o mesmo instrumento, a mesma quantidade e o
  mesmo valor que uma `aplicacao`; o que ele preserva é a distinção que o cliente fez, numa
  superfície auditável. *Rejeitado, e estava escrito errado na versão anterior deste
  arquivo:* gravar `aporte` como `instrumento_id = caixa:BRL` com `qtd_delta =
  +valorFinanceiro` — isso **descartaria** o `instrumentoId` (que Operações validou contra o
  catálogo do Hub) e a `quantidade`, numa tabela sem UPDATE, para sempre. A evidência de que
  o campo existe e é obrigatório está no molde: `Operacao.Create`
  (`../operacoes/src/Operacoes.Domain/Operacoes/Operacao.cs`) exige `instrumentoId`
  não-vazio e `quantidade > 0` para **todo** tipo, `aporte` incluído, e os testes de lá
  gravam `('cliente-1','td:tesouro-selic-2029','aporte',10.5,1000.00)`. A §3 da
  `ARQUITETURA` agrupa "aplicações / resgates / aportes" como captura de negociação e a §7.3
  lista "compra/venda/aporte" lado a lado.

  *Nota de higiene, que NÃO bloqueia esta fase:* `movimentos.tipo` é DDL privada desta casa
  (ADR-12), então acrescentar `aporte` ao enum é **desvio por correção registrado aqui** e,
  em paralelo, correção do texto da §7.1 no `plataforma-docs` — ao contrário do
  `estornaTradeId`, que era a §5.1, contrato **entre** serviços, e por isso foi
  pré-requisito bloqueante no F3 do `operacoes`.

  **As linhas DERIVADAS — uma linha por movimento, com instrumento, sinal e valor
  fechados.** Um resgate produz **até seis** linhas, e sem esta tabela `ir_retido` e `iof` não
  têm instrumento nem sinal, e `a_liquidar` não tem valor definido. *"Até": são seis com IOF e
  base positiva; **cinco** sem IOF (prazo ≥ 30 dias) e **quatro** quando a base do IR é zero
  ou negativa — as duas condições são do F5, e quem contar linhas num teste declara qual dos
  três casos o fixture monta.* Para um resgate de `q`
  unidades do instrumento `X` por `Y` bruto, com IR `t` e IOF `f` (`f` pode não existir):

  | Linha | `ref_externa` (V3) | `tipo` | `instrumento_id` | `qtd_delta` | `valor_financeiro` | `data_evento` |
  |---|---|---|---|---|---|---|
  | venda | `<tradeId>` | `venda` | **X** | **−**`q` | `Y` | `dataEvento` |
  | IR retido | `ir:<tradeId>` | `ir_retido` | **`caixa:a_liquidar`** | **−**`t` | `t` | `dataEvento` |
  | IOF | `iof:<tradeId>` | `iof` | **`caixa:a_liquidar`** | **−**`f` | `f` | `dataEvento` |
  | direito a receber | `aliq:<tradeId>` | `a_liquidar` | **`caixa:a_liquidar`** | **+**`Y` | `Y` | `dataEvento` |
  | liquidação, perna 1 | `liq:<tradeId>:aliq` | `liquidacao` | **`caixa:a_liquidar`** | **−**(`Y−t−f`) | `Y−t−f` | **D+1 útil** |
  | liquidação, perna 2 | `liq:<tradeId>:brl` | `liquidacao` | **`caixa:BRL`** | **+**(`Y−t−f`) | `Y−t−f` | **D+1 útil** |

  As famílias de corpaction (F9) seguem a mesma forma, trocando só o principal: **cupom** →
  `(tipo=cupom, instrumento X, qtd_delta 0, valor = q × valorPorUnidade)`, e o dinheiro
  entra por `aliq:cupom:…` em `caixa:a_liquidar`; **vencimento** → `(tipo=resgate,
  instrumento X, qtd_delta = −q, valor = q × PU final)`. As linhas de reversão (`est:…`,
  F5) são `tipo = 'ajuste'` no **mesmo `instrumento_id`** da linha revertida, com as duas
  colunas simétricas, a **mesma `data_evento`** da revertida (V1) e `ref_estorno` apontando
para ela.

  **`a_liquidar` é BRUTO, e os tributos DEBITAM `caixa:a_liquidar`. Esta é a decisão, e ela
  tem consequência permanente.** O direito a receber nasce pelo valor cheio da operação e é
  **reduzido pelas próprias linhas de tributo**, porque IR e IOF são retidos **na fonte** —
  a corretora desconta do que vai pagar. O saldo a receber é, então, uma **soma de linhas do
  livro** (`+Y −t −f`), não um número calculado no momento da escrita: divergência entre "o
  tributo lançado" e "o valor a receber" deixa de ser representável. Consequência de
  brinde: se a linha `ir:` faltar (a janela F4→F5), o direito a receber fica **grande demais**
  e a guarda permanente do F5 (`resgate sem linha ir:<tradeId>`) aponta para o buraco.
  *Rejeitado:* `a_liquidar` **líquido** (`+(Y−t−f)`) com `ir_retido`/`iof` gravados no
  instrumento do título com `qtd_delta = 0` — as linhas de tributo não teriam efeito em
  projeção nenhuma (viram documentação pura), o valor a receber passaria a ser um número
  derivado no instante da escrita que **nada confere depois**, e o tributo voltaria a ser,
  de fato, desconto embutido — contra a convenção 3 da §7.1, que existe justamente para o
  extrato ser conferível contra o da corretora.

  **A aritmética do resgate, escrita aqui porque vários critérios de pronto pendem dela — e
  ela é INDEPENDENTE DE PREÇO de propósito.** Patrimônio do dia = Σ (`quantidade` ×
  `preço`) sobre todas as linhas do snapshot, com `caixa:*` a preço 1 por definição (V4).
  São **três invariantes**, e é sobre eles que os Prontos se apoiam:

  1. **O saldo do fato em `caixa:a_liquidar` é uma soma do livro:** `Σ qtd_delta` das linhas
     daquele fato em `caixa:a_liquidar`, depois de D, é `Y − t − f`; **depois da liquidação
     de D+1, é 0**. É o que prova que "tributo é movimento próprio" virou dado — com IR
     embutido no valor da venda, a soma bateria **sem** as linhas de tributo.
  2. **A liquidação é transferência pura:** `Σ qtd_delta(caixa:a_liquidar) +
     Σ qtd_delta(caixa:BRL)` é **inalterado** pelas duas pernas. Corolário que dispensa
     preço: as duas pernas são `caixa:*`, a preço 1 por definição, e somam zero — **a
     liquidação, sozinha, não muda o patrimônio**. Logo a variação de patrimônio entre D e
     D+1 é **exatamente** a variação de preço da posição residual do título, e nada mais.
  3. **A queda de patrimônio entre D-1 e D é `t + f` — SOB A HIPÓTESE, DO FIXTURE, DE PREÇO
     CONGELADO entre os dois dias.** É hipótese de fixture, **nunca** invariante do sistema.

  **Por que o item 3 é hipótese e não invariante, com a conta feita** — porque a versão
  anterior deste arquivo o escrevia como invariante e o punha em quatro critérios de pronto,
  onde uma implementação **correta** reprova. O snapshot de D-1 é, pela §7.1/§7.4,
  `quantidade × preco(último ≤ D-1)`, isto é `P₍D-1₎` — **não** o preço de D. Com posição
  total `Q`, caixa preexistente `C` e a venda de `q` por `Y`:

  ```
  patrimônio(D-1) = Q·P₍D-1₎ + C
  patrimônio(D)   = (Q−q)·P₍D₎ + C + (Y − t − f)
  queda           = Q·P₍D-1₎ − (Q−q)·P₍D₎ − Y + t + f
  ```

  Isso só vale `t + f` quando `P₍D-1₎ = P₍D₎` **e** `Y = q·P₍D₎`. A §8.4 fixa a segunda
  condição de propósito (`"livro: venda (qtd -X, valor = X x PU_venda(D))"`), o que deixa a
  **primeira** — preço parado de um dia para o outro — como única hipótese que faz a conta
  fechar. E preço parado é justamente o que **não** acontece: a variação diária de preço é o
  motivo de o sistema existir. O mesmo vale para "entre D e D+1 o patrimônio é constante":
  só é verdade porque o exemplo **zera** a posição; com posição residual `Q > q`,
  `patrimônio(D+1) − patrimônio(D) = (Q−q)·(P₍D+1₎ − P₍D₎)`, e o `eod.ready(D+1)` traz preço
  novo por construção. O invariante independente de preço é o **item 2** acima; a
  constância é consequência do fixture que zera a posição, e tem de estar escrita **no
  fixture**.

  ### PENDÊNCIA BLOQUEANTE DO F3 — `caixa:BRL` nunca é DEBITADO, e o vocabulário do livro não fecha sem decidir isto

  **Estado:** aberta. **Bloqueia:** o F3 (e, por dependência, tudo que lê caixa: F5, F7, F8).
  **Quem decide:** o dono, e a correção é na §5.1 do `../plataforma-docs` — **outro repo**.
  **O F3 NÃO fecha o vocabulário do livro sem isto.**

  **O fato, e ele é conferível por varredura deste arquivo:** nenhuma linha deste roadmap
  debita `caixa:BRL`. A única linha que o escreve é a `liquidação, perna 2`, sempre
  `+(Y−t−f)`. Não existe tipo de saque, não existe `a_pagar`, e o `aplicacao → compra` grava
  **uma** linha, no instrumento do título, sem contrapartida. Venda credita caixa para
  sempre; compra consome caixa que nunca sai.

  **A conta, com números, no fixture da §8.4 (`Y = q·P`):**

  ```
  D0  compra 10 @ 100          X: q 10, pm 100                 patrimônio = 10×100      = 1000
  D1  resgate 10, Y=1000, t=100, f=0
                               X: q 0;  caixa:a_liquidar 900    patrimônio = 900   (cai t ✓)
  D2  liquidação               a_liquidar 0; caixa:BRL 900      patrimônio = 900  (inalterado ✓)
  D3  reaplica 9 @ 100         X: q 9, pm 100; caixa:BRL 900
                               patrimônio = 9×100 + 900         = 1800
  ```

  O cliente tem **900**. O extrato de posição diz **1800** — 100% de erro, no dia seguinte à
  reaplicação, que é o ciclo de vida normal do produto, e infla de novo a cada
  resgate+reaplicação. **A própria álgebra escrita acima denuncia:**
  `patrimônio(D) = (Q−q)·P₍D₎ + C + (Y − t − f)` trata `C` (caixa preexistente) como parcela
  **aditiva**; aplicar `q'` unidades soma `q'·P` e não subtrai nada de `C`.

  **Por que a decisão NÃO é desta casa, e é isto que a torna pendência em vez de escolha.** O
  modelo atual é **coerente** se `aplicacao` significa *"dinheiro entrou de fora e virou
  título"*, e é **errado** se a aplicação consumiu o produto de uma liquidação anterior — aí
  o dinheiro já estava no livro e passa a ser contado duas vezes. **As duas leituras são
  legítimas**, e a Custódia **não tem como distinguir uma da outra**: o `TradeRegistered` da
  §5.1 não diz se a aplicação trouxe dinheiro novo ou reinvestiu saldo em custódia.
  **Operações sabe, e não publica.** Escolher entre as duas leituras aqui é **re-derivar rio
  abaixo o que o produtor já sabe**, que é a §10.32 literal — o mesmo erro que este arquivo
  recusa no `campoPosicao` (F6) e na distinção `sem_preco_ate_a_data` ×
  `instrumento_desconhecido`.

  **A correção, quando o dono decidir:** campo **OPCIONAL** no `TradeRegistered` da §5.1
  dizendo se a aplicação consumiu saldo em custódia. É a **mesma forma** do `estornaTradeId`,
  que o F3 do `../operacoes` acrescentou à §5.1 **antes** do código que monta o payload, e que
  o roadmap de lá registra como **"pré-requisito, não consequência"**. Campo novo opcional é
  o que a própria §5.1 autoriza sem incrementar `v`.

  **As duas saídas alternativas — se o dono decidir não mexer na §5.1 —, com o custo de cada
  uma escrito, porque nenhuma é de graça:**

  - **(a) perna simétrica:** `compra` e `aporte` debitam `caixa:BRL`. Fecha a conta da
    reaplicação e **mostra caixa NEGATIVO em toda aplicação com dinheiro novo** — porque não
    existe evento de depósito na §5.1, e o primeiro aporte de um cliente vira `caixa:BRL =
    −1000` no primeiro dia. Trocaria um número inflado por um número negativo, no mesmo
    documento que o cliente lê (P2).
  - **(b) livro-caixa PARCIAL declarado:** `caixa:BRL` é, por definição, **só o produto de
    liquidações ainda não reinvestido**, e nunca o saldo do cliente. Não exige nada de
    ninguém e **superestima o patrimônio em toda reaplicação**, exatamente pela conta acima —
    a diferença é que passa a ser **limite declarado** em vez de erro silencioso, e toda
    afirmação de "soma dos instrumentos + caixa = patrimônio" cai junto.

  **O que JÁ foi feito neste arquivo, e não espera a decisão:** o F8 Pronto (g) **deixou de
  afirmar** "soma dos instrumentos + caixa = patrimônio", porque essa asserção fecharia
  **verde com o número errado** — que é o pior dos três estados possíveis. O que ele afirma
  agora está escrito lá, com o motivo.

  **Por que isto não pode ser adiado para a fase que esbarrar:** `movimentos` é append-only e
  **não tem UPDATE**. A saída (a) muda o conjunto de linhas que **todo trade** grava; adotá-la
  depois de o F4 estar em produção deixa um período inteiro de livro sem a perna de caixa, e
  não há como apendá-la sem inventar `ref_externa` de uma linha que deveria ter nascido junto.

  Duas armadilhas nomeadas, porque as duas já apareceram em rascunho deste arquivo:

  - **`resgate` do evento NÃO é `resgate` do livro.** No enum da §7.1, `resgate` é
    **vencimento** — a §7.3 o usa exatamente assim ("vencimento → livro: `(tipo=resgate,
    qtd_delta = -qtd, valor = qtd × PU final)`"). O resgate **voluntário** que o cliente
    pede em Operações é uma **venda**. As duas palavras são a mesma em português e coisas
    diferentes no schema; quem escrever "resgate" sem dizer qual está criando o defeito.
  - **`aplicacao` não existe no enum**, e não deve passar a existir por tradução preguiçosa:
    `aplicacao` **é** `compra`. `aporte`, ao contrário, ganha valor próprio (V1) porque é o
    rótulo que o extrato preserva.

  **A DISPENSA DECLARADA DO ESTORNO — a única desta V2, e ela existe porque sem ela a
  guarda estrita abaixo fica ambígua exatamente no caso que mais importa.** O
  `TradeRegistered` de estorno carrega `instrumentoId`, `quantidade` e `valorFinanceiro`
  **próprios** — são campos **obrigatórios** do envelope da §5.1, não opcionais —, e o
  mapeamento acima manda usar os valores do **movimento estornado, por lookup**. Regra
  escrita, para não ficar por conta do executor: **esses três campos do evento de estorno
  são CONFERIDOS contra o movimento original e NÃO são usados para gravar.** A conferência
  é campo a campo: `instrumentoId` igual ao `instrumento_id` do movimento revertido,
  `quantidade` igual a `|qtd_delta|` dele, `valorFinanceiro` igual ao `valor_financeiro`
  dele. **Divergir em qualquer um deles não é caso feliz nem detalhe:** hoje a reversão
  **total** seria gravada em silêncio sobre um payload que pede outra coisa (estorno
  parcial, ou erro em Operações), numa tabela sem UPDATE. Desfecho: **nada é gravado, a
  chave de dedupe não é consumida (I13), a mensagem estaciona com o motivo
  `estorno_divergente` e alerta** — valor que entra na lista fechada e sem default de
  `x-custodia-motivo` do F4. *Rejeitado:* aplicar o payload em vez do original — o sinal do
  ajuste depende do tipo da operação estornada, que o payload não carrega (corolário da
  §10.32 no F4), e estorno parcial não está no contrato da §5.1. *Rejeitado:* ignorar os
  três campos sem conferir — é a dispensa silenciosa que a guarda abaixo existe para
  proibir, e nenhum teste a distinguiria de um mapeamento errado.

  **Guarda estrita do mapeamento, e ela é do invariante I3:** **nenhum campo do
  `TradeRegistered` pode ser descartado sem regra escrita nesta V2** — e "conferido contra o
  original e não usado", acima, **é** uma regra escrita, que o teste estrito afirma como
  tal (não como dispensa implícita). Foi exatamente um
  descarte silencioso (`instrumentoId` e `quantidade` do `aporte`) que passou pela versão
  anterior deste arquivo. O teste que prova I3 na direção estrita tem de afirmar isso — que
  cada campo do payload ou aparece numa coluna do movimento, ou tem aqui a regra escrita que
  o dispensa —, senão ele **codifica o próximo erro de mapeamento** em vez de pegá-lo.

  **V3 · A convenção de `ref_externa`, completa — inclusive as duas pernas da liquidação e
  os derivados que não têm `tradeId`.**

  `ref_externa` é **NOT NULL** (a §7.1 a escreve nullable — **desvio POR CORREÇÃO,
  registre-o**) e a chave é derivada **por MOVIMENTO, não por FATO**. A forma é sempre:

  ```
  ref_externa = <papel>:<fato>[:<perna>]        (o ":" só existe quando há o que separar:
                                                 com <papel> vazio a chave é o <fato> puro,
                                                 sem dois-pontos à esquerda)
  ```

  - **`<fato>`** é a chave do fato de origem, e há **duas** famílias:
    - de Operações: `<tradeId>` (ex.: `op-7f3a...`);
    - de corpaction, que **não tem `tradeId`**: `cupom:<instrumentoId>:<data>` e
      `venc:<instrumentoId>:<data>` — a forma que a própria §7.1 dá de exemplo
      (`"cupom:td:...:2026-11-15"`).
  - **`<papel>`** é **vazio** para o movimento principal do fato — inclusive para o
    principal do estorno, cuja chave é o `<tradeIdEstorno>` puro —, e um de
    `ir` · `iof` · `aliq` · `liq` · `est:ir` · `est:iof` · `est:aliq` · `est:liq`
    para os derivados. **Não existe papel `est` sozinho**: a versão anterior o listava e
    a enumeração completa nunca o usava, e gramática com símbolo morto convida a inventar.
  - **`<perna>`** só existe onde um movimento escreve **duas linhas**, e vale
    `aliq` ou `brl`.

  Enumeração completa, e é ela que o F4, o F5 e o F9 copiam sem reescrever:

  | Fato | Linhas e suas `ref_externa` |
  |---|---|
  | trade (aplicacao/resgate/aporte) | `<tradeId>` · `ir:<tradeId>` · `iof:<tradeId>` · `aliq:<tradeId>` · `liq:<tradeId>:aliq` · `liq:<tradeId>:brl` |
  | cupom | `cupom:<instrumentoId>:<data>` · `ir:cupom:<instrumentoId>:<data>` · `aliq:cupom:<instrumentoId>:<data>` · `liq:cupom:<instrumentoId>:<data>:aliq` · `liq:cupom:<instrumentoId>:<data>:brl` |
  | vencimento | `venc:<instrumentoId>:<data>` · `ir:venc:...` · `iof:venc:...` · `aliq:venc:...` · `liq:venc:...:aliq` · `liq:venc:...:brl` |
  | estorno (o `<fato>` é o `tradeId` DO ESTORNO) | `<tradeIdEstorno>` · `est:ir:<tradeIdEstorno>` · `est:iof:<tradeIdEstorno>` · `est:aliq:<tradeIdEstorno>` · `est:liq:<tradeIdEstorno>:aliq` · `est:liq:<tradeIdEstorno>:brl` |

  Por que a **perna** entra no sufixo: a liquidação é intrinsecamente de **duas pernas**
  (−Y em `caixa:a_liquidar`, +Y em `caixa:BRL`) e `movimentos` tem **um** `instrumento_id`
  por linha. Com uma chave só (`liq:<tradeId>`) as duas linhas colidem no
  `UNIQUE (cliente_id, ref_externa)` — que é literalmente o defeito que esta decisão diz
  estar prevenindo, reintroduzido dentro dela. Por que a família de corpaction existe:
  cupom e vencimento produzem, **por cliente**, o mesmo conjunto de 4–5 linhas que um
  resgate, e nenhuma delas tem `tradeId`; sem a convenção aqui, ou colidem, ou o executor
  do F9 inventa uma convenção nova dentro de uma tabela append-only depois de esta fase
  ter declarado a convenção fechada. Por que a chave do estorno usa o `tradeId` **do
  estorno** e não o do original: cada estorno é um fato, com uma chave de dedupe própria; a
  proteção contra reverter a mesma linha duas vezes não é o `UNIQUE (cliente_id,
  ref_externa)`, é o **V5** abaixo.

  A chave é montada por **concatenação em ordem fixa** e **nunca é lida de volta** — não se
  faz parsing dela. `<instrumentoId>` contém `:` (`td:tesouro-ipca-2035-05-15`), e isso é
  inofensivo justamente por não haver parsing; as duas famílias também não se cruzam,
  porque a de corpaction termina sempre em data ISO e a de trade nunca. *Rejeitado:*
  `ref_externa = tradeId` para todas as linhas do fato — colidem entre si no UNIQUE.
  *Rejeitado:* `ref_externa` NULL nos derivados — **o Postgres não constrange NULL**, e
  cada linha derivada poderia duplicar para sempre numa tabela onde não há DELETE, que é o
  defeito que mata a idempotência exatamente nas linhas que a reentrega mais gera.

  **V4 · Os ids de caixa: locais, com preço 1 por definição.**

  `caixa:BRL` (saldo) e `caixa:a_liquidar` (direitos a receber) são **ids locais da
  Custódia**, não ids do Hub. **A ADR-4 não os proíbe** — ela proíbe uma *terceira
  identidade* para instrumento **do Hub** (de-para, uuid interno); caixa não existe no Hub,
  nunca existiu, e não tem id lá para ser traduzido. Escrito aqui porque um executor lendo
  a ADR-4 junto do CHECK de `instrumento_id` pode restringi-lo de um jeito que os rejeite.

  E o que resolve o buraco que atravessava F5→F7→F8: **`caixa:*` tem `preco = 1,000000` POR
  DEFINIÇÃO.** Não é observação, não é forward-fill, não é "último ≤ D". Consequências, e
  as três precisam ser escritas juntas:

  1. `caixa:*` **nunca** recebe `PriceObserved` e **nunca** é pedido ao Hub no bootstrap do
     F6 (o `/prices/asof` devolveria `motivo: instrumento_desconhecido`, que é sinal de
     defeito e não deve ser fabricado por nós);
  2. `caixa:*` fica **fora** do alerta de "preço ausente em dia útil com posição" do F7 —
     senão a regra do F7 ("dia sem preço é sinal") produziria alerta diário permanente,
     todo dia, para sempre;
  3. `snapshots_posicao.preco` é **NOT NULL** (DDL da §7.1), então sem o preço por
     definição **não nasceria linha de snapshot de caixa nenhuma** — o F5 escrituraria o
     limbo D→D+1 no livro (I10) e o F7 o apagaria do documento, deixando o Pronto do F8
     ("extrato de posição batendo com a soma dos snapshots vigentes, **incluindo as linhas
     de caixa**") inalcançável, e o "soma dos instrumentos + caixa = patrimônio diário" da
     §7.5 sem parcela de caixa. *Esta consequência afirma só que **sem preço por definição
     não existe linha de caixa no snapshot**, e isso vale sob qualquer desfecho da
     **PENDÊNCIA BLOQUEANTE do `caixa:BRL`** (V2, acima). A **igualdade** da §7.5 citada aqui
     é justamente o que aquela pendência deixa em aberto — não a leia como asserção fechada,
     e é por isso que o F8 Pronto (g) não a afirma mais.*

  Coerente com isso: em `posicao_corrente`, uma linha de `caixa:*` com **`quantidade > 0`**
  tem `preco_medio = 1,000000` e `custo_total = quantidade`.

  **A REGRA DE FRONTEIRA DA V1 PRECEDE ESTA, INCLUSIVE PARA `caixa:*` — e a precedência
  está escrita porque sem ela as duas regras dão respostas diferentes para o estado MAIS
  COMUM da tabela.** `caixa:a_liquidar` volta a **zero depois de toda liquidação** (é o
  invariante (1) da aritmética do resgate, logo abaixo, e o Pronto (a) do F5): esse é o
  estado permanente da linha, não uma borda. Pela fronteira da V1, `quantidade` resultante
  `= 0` ⇒ `preco_medio = 0` e `custo_total = 0`, **para QUALQUER tipo**; pela frase acima,
  lida sem qualificador, seria `preco_medio = 1,000000`. **Decisão: a fronteira vence, e a
  V4 vale só para `quantidade > 0`.** Não é cosmético — **três consumidores têm de
  concordar**: o handler incremental do F4/F5 (O(1)), o comando de reconstrução e a
  **reconciliação do F7**, que é o único detector automático de divergência do sistema. Se
  um pegar a tabela por tipo e o outro a fronteira, a reconciliação alerta **em toda
  operação normal**, que é o falso positivo permanente que este arquivo condena ao rejeitar
  `data_ref` para o alerta das 12:00. *A assimetria que produzia a contradição fica
  registrada: a regra do **cruzamento** exclui `caixa:*` nominalmente, e as regras `= 0` e
  `< 0` não excluíam — excluir uma de três é o que fazia parecer que as outras duas não se
  aplicavam.* Consequência no F5: o Pronto (e) afirma `preco_medio = 0` para
  `caixa:a_liquidar` depois da liquidação, e é esse o valor, não `1,000000`.

  **CHECK enumerando os ids de caixa permitidos** (`caixa:BRL`, `caixa:a_liquidar`), pela
  §10.21: restringir é reversível (`DROP CONSTRAINT`, uma linha, sem rewrite), e não
  restringir é eterno. O motivo é a §10.24 combinada com a decisão de gravar identidade
  **crua**: sem `ToLowerInvariant`, um `caixa:brl` digitado uma vez vira um **segundo**
  instrumento de caixa, para sempre, e o patrimônio passa a somar duas linhas onde havia
  uma. Um `caixa:USD` futuro custa uma migration de uma linha.

  **V5 · Uma linha é revertida no máximo uma vez, e estorno DE estorno continua possível.**

  Índice `UNIQUE (ref_estorno) WHERE ref_estorno IS NOT NULL`, **nomeado** com
  `HasDatabaseName` (§10.23). Sem ele, dois `ajuste` podem apontar para o **mesmo**
  movimento: cada estorno chega com `tradeId` próprio, os dois passam pelo
  `UNIQUE (cliente_id, ref_externa)` sem conflito, e a posição é revertida **duas vezes**
  numa tabela onde não há UPDATE nem DELETE. É a constraint que a §10.21 nomeia junto com
  as outras, no incidente que esta fase inteira cita como fundamento ("índice
  `UNIQUE (estorna_operacao_id) WHERE ... IS NOT NULL` — uma operação não se estorna duas
  vezes"), e o molde `operacoes` já a tinha. **Ela não fecha a porta de correção:** estorno
  **de** estorno tem `ref_estorno` apontando para uma linha **diferente** (o ajuste
  anterior), então não colide — e continua permitido de propósito, que é a exceção nomeada
  da §10.21. O Pronto prova as duas coisas.

  ---

  **Decisões desta fase:**

  - **Decisão A, a metade irreversível:** `ref_estorno` NOT NULL quando `tipo = 'ajuste'`,
    e NULL obrigatório nos demais tipos, por CHECK; mais FK composta com `cliente_id` (o
    ajuste não cruza cliente) e CHECK de não-auto-referência. Consequência deliberada: um
    estorno órfão **não pode ser gravado**. O custo aceito é explícito e é o F4: o
    consumidor precisa de política para o evento fora de ordem.
  - **`ref_externa`: a convenção completa é a V3 acima**, e o que ela compra é isto — o
    adiamento das consequências contábeis para o F5 vira **recorte de escopo** em vez de
    dívida sem prazo, porque com a chave definida o backfill do F5 é idempotente por
    construção e não toca nenhuma linha existente. O sufixo faz parte do schema, não da
    imaginação do handler.
  - **`preco_atual` mantém a PK da §7.1 (`instrumento_id`) e guarda SÓ o `campoPosicao`.**
    A DDL da §7.1 dá **uma linha por instrumento**, com uma coluna `campo` singular; o
    `historico_precos` é que guarda **todos** os campos, com a chave inteira da §5.1
    (`instrumento_id, data_ref, campo, fonte, revisao`). Escrito aqui porque o F6 enuncia a
    monotonicidade da projeção e precisa saber sobre o quê: com esta decisão ela se enuncia
    **por instrumento**, não por `(instrumento, campo)` — não há dois campos disputando a
    linha. *Rejeitado:* PK `(instrumento_id, campo)` como desvio por correção — resolveria o
    mesmo problema, mas contra o texto explícito da §7.1 e sem necessidade, já que o
    histórico local (decidido nesta mesma fase) guarda os demais campos com a chave completa.
    Sem esta linha o executor do F6 teria de inventar, com o schema já fechado.
  - **`data_evento` no futuro é dado sem conserto, e a guarda é TRIGGER, não CHECK.** O F5
    prova o ponto ao rejeitar a liquidação antecipada ("põe no livro um fato que ainda não
    aconteceu"), e a §10.21 manda pôr no schema o que não tem conserto. **CHECK com data de
    hoje não é aceito pelo Postgres** — nem `current_date` nem
    `(now() AT TIME ZONE 'America/Sao_Paulo')::date` são `IMMUTABLE`, e o CHECK exige
    imutabilidade —, então a guarda vai numa trigger `BEFORE INSERT` ao lado da de
    imutabilidade, e **a expressão que ela compara é a do fuso**, decidida na alínea seguinte,
    nunca `current_date`. Ela é
    segunda linha de defesa: a §6.1 camada 2 já rejeita `data_evento > hoje` na borda, em
    Operações; a diferença é que aqui a violação seria **eterna**. Nenhum escritor deste
    roadmap grava data futura de propósito — o job do F5 só insere a liquidação **quando ela
    vence** —, então a guarda não bloqueia caminho nenhum previsto. *Rejeitado:* confiar só
    na borda; é a mesma classe de "afirmação de cobertura que não existe" da §10.22, e a
    borda é de **outro repo**.
  - **O FUSO HORÁRIO DE NEGÓCIO É `America/Sao_Paulo` (UTC−3), e a decisão é desta fase
    porque é aqui que a primeira `date` vira dado.** A `ARQUITETURA` §11 já manda ("relógio
    dos jobs em UTC−3; D+1 de liquidação conta DIAS ÚTEIS") e este roadmap não carregava a
    regra — de propósito não: por omissão. Sem ela, **três relógios diferentes decidem dado
    e nenhum concorda**: `current_date` no Postgres é o dia no fuso do servidor (**UTC** num
    container padrão), "hoje" no job é o `DateOnly` do processo .NET, e "12:00" do alerta é
    avaliado pelo Grafana Cloud. **Regra, e ela é herdada por F5 e F7 sem decisão nova lá:**
    toda coluna `date` deste schema é uma data no fuso `America/Sao_Paulo`; a trigger de data
    futura compara contra `(now() AT TIME ZONE 'America/Sao_Paulo')::date`, **nunca** contra
    `current_date`; "hoje", "dia útil" e "12:00" das fases seguintes são nesse fuso; e a
    regra do alerta das 12:00 do F7 **carrega o offset explicitamente**, porque o avaliador
    roda em UTC. *O caso concreto que a ausência produzia é diário, não teórico:* entre 00:00
    e 03:00 BRT o dia BRT já virou e o UTC não, então o job de **ciclo curto** do F5 — que
    roda 24 h por dia por decisão daquela fase — tentaria inserir `data_evento = hoje_BRT` e
    a trigger o **rejeitaria como data futura**, todo dia, por três horas, com um desfecho
    que não tem nome em lugar nenhum (o job só tem `COMPLETUDE` e `LIMITE`). *Rejeitado:*
    fixar `TZ=America/Sao_Paulo` no container e continuar usando `current_date` — resolve por
    configuração de ambiente o que é regra de **dado**, some no primeiro compose que esquecer
    a variável (e as cinco listas não a carregam), e não alcança o avaliador do Grafana, que
    é de outro repo. *Rejeitado:* trocar `date` por `timestamptz` — a §7.1 escreve `date` e a
    granularidade do negócio é o dia; o que faltava era **dizer o fuso**, não trocar o tipo.
  - **`preco_medio` e `custo_total` de `posicao_corrente` têm dono, e é o F4 — mas a REGRA
    de dobra é desta fase, e está na V1.** As três
    colunas nascem aqui e são preenchidas **na mesma transação** pelo handler do F4 (I4).
    Escrito nesta fase porque a coluna sem dono é o que produz o defeito: o F5 tributa com
    "base = **preço médio do livro**" (§7.3) e o F7 grava `preco_medio` e `custo` em cada
    snapshot (§7.4) — um executor do F5 encontrando a coluna vazia inventaria a regra de
    preço médio **dentro do cálculo de imposto**, que é o pior lugar possível para ela.
  - **A regra I13, escrita aqui como norma do repo:** *a chave de dedupe só se consome
    quando o movimento gravado está **correto e completo**.* Onde uma fase abrir exceção,
    ela justifica dentro da fase.
  - **Imutabilidade por TRIGGER na migration, nunca por REVOKE.** Motivo verificado no F2
    do `operacoes`: a role `custodia` é **dona do database** e roda as migrations no boot,
    então o revoke seria decoração e ainda quebraria migration de dados dentro do laço de
    deploy. Manter o `TRUNCATE` aberto de propósito (trigger de linha não dispara em
    TRUNCATE) — é o que torna fixture e limpeza de teste possíveis, e registrar isso, para
    ninguém "consertar" depois. **Estorno de estorno continua permitido de propósito**: é a
    única saída quando um estorno entra errado (§10.21, a exceção nomeada).
    **E há um segundo motivo, que só aparece no F5 e por isso fica registrado aqui, onde
    alguém iria "endurecer":** o ponto de serialização entre o job de liquidação e o handler
    de estorno é um `SELECT … FOR UPDATE` sobre a linha `aliq:<fato>` **de `movimentos`**, e
    o Postgres exige privilégio de escrita na tabela para travar a linha. Ele funciona
    **porque** a imutabilidade é trigger. Com `REVOKE UPDATE ON movimentos`, os dois
    caminhos param de serializar **em silêncio** — e o teste sequencial do F5 continua
    verde, porque ele não exercita a corrida.
  - **As QUATRO projeções** (`posicao_corrente`, `preco_atual`, `historico_precos`,
    `snapshots_posicao`) **nascem SEM FK para `movimentos` e SEM trigger de
    imutabilidade.** *Rejeitado:* FK para o livro — tornaria as projeções co-dependentes
    dele e atrapalharia exatamente a operação que as define como descartáveis (`TRUNCATE` +
    reconstrução).
  - **Identificadores gravados crus, com `Trim()` e nada mais** (§10.24, ADR-4).
    *Rejeitado:* `Trim().ToLowerInvariant()` como no molde `Hub.Domain.Instrumentos.InstrumentoId`
    — lá o slug é do próprio Hub; aqui `instrumento_id` é identidade de **outro contexto**,
    e canonizar identidade alheia é presumir regra que pode mudar sem aviso. Trim remove
    ruído de transporte; baixar caixa **transforma** o valor. Numa tabela append-only,
    `"cli-1"` e `"cli-1 "` seriam dois clientes distintos para sempre.

  **NÃO ENTRA:** consumidor, handler, tributo, snapshot calculado, extrato, worker. E não
  entra a validação de escala/magnitude no Domínio: ela nasce com a entidade no F4, e o
  adiamento é legítimo pela única razão que o legitima aqui — **não há escritor, então não
  há o que gravar errado**. As **constantes**, porém, nascem agora com o teste que as
  confronta com o banco, porque é a divergência silenciosa entre as duas cópias que a
  §10.25 cobra.

  **Configuração: nenhuma** das cinco listas muda. O `.env.example` também não — schema
  não é credencial.

  **Âncoras:** `ARQUITETURA` §7.1 (DDL e convenções: caixa é instrumento, só há snapshot em
  dia com posição ≠ 0, tributo é movimento próprio), §7.2, ADR-4, ADR-12; `PADROES` §3,
  §9, §10.21, §10.22, §10.23 (FK composta faz o EF gerar índice PascalCase que ninguém
  nomeou), §10.24, §10.25, §10.18, **§10.34** (coluna `date` sem fuso declarado — é dela que
  sai a decisão do fuso desta fase); `LEIA-ME-KIT` "Aceitar 'isso é da próxima fase' sem
  perguntar se dá para consertar depois"; e o **F2 do `../operacoes/docs/ROADMAP.md`
  inteiro**, que é o molde vivo desta fase.

  **Prompt:**
  ```
  Implemente o schema da Custodia como migrations EF, snake_case, indices nomeados,
  seguindo o molde ../operacoes/src/Operacoes.Infrastructure/Persistence/
  (Configurations/OperacaoConfiguration.cs e Migrations/20260905224603_CriaSchemaOperacoes.cs
  sao os arquivos a ler antes de escrever).

  As tabelas DA SECAO 7.1 sao CINCO: movimentos, posicao_corrente, preco_atual,
  historico_precos e snapshots_posicao. O "cinco" e sobre a 7.1 — o livro e as quatro
  projecoes —, NAO sobre o schema inteiro do servico. HA DUAS TABELAS FORA DA 7.1 NESTE
  ROADMAP, as duas rotuladas nas fases delas, e NENHUMA das duas contradiz esta fase: o
  eod_processado do F7 (controle de consumo, MUTAVEL, sem trigger, com forma decidida la) e
  o CALENDARIO DE DIAS UTEIS do F5 (CONFIGURACAO, semeada por migration, que nao se
  reconstroi do livro). NAO ESCREVA "a unica tabela fora da 7.1": sao duas. As quatro primeiras e a ultima estao na secao 7.1 do ARQUITETURA;
  historico_precos e a que a 7.1 chama de "historico local de precos (recomendado)",
  logo depois da DDL de preco_atual, e ela espelha a canonica com a chave INTEIRA da 5.1
  — (instrumento_id, data_ref, campo, fonte, revisao). Ela entra AQUI e nao no F6 pelo
  argumento desta propria fase: aqui nao existe escritor, entao nada pode ser gravado
  errado; no F6 ela nasceria com escritor ja ligado. Note que preco_atual NAO tem coluna
  `fonte` na 7.1 — sem historico_precos a chave de dedupe da 5.1 nao tem onde morar
  inteira. Confira coluna a coluna contra a 7.1 antes de implementar; se discordar de
  alguma, levante ANTES de escrever.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  ============================================================================
  O VOCABULARIO DO LIVRO — VOCE FECHA ISTO AQUI, E AS FASES F4/F5/F7/F9 COPIAM
  ============================================================================

  V1. O CHECK de `tipo` e ESTA lista, literal:
      compra | venda | aporte | cupom | resgate | ir_retido | iof | a_liquidar |
      liquidacao | ajuste
      A 7.1 escreve o enum SEM `aporte`, e isso e um furo dela: a 7.3 descreve o handler
      gravando "movimento da operacao (compra/venda/APORTE, qtd_delta, valor,
      data_evento)". DESVIO POR CORRECAO da 7.1, apoiado no texto da 7.3 — registre-o.
      NAO mapeie aporte para `compra`: tornaria o aporte indistinguivel de uma aplicacao
      no extrato de movimentacao, que e a superficie auditavel desta casa. O que separa
      os dois e SO O ROTULO (V2), e o rotulo e o que o cliente declarou.
      NAO acrescente a taxonomia da B3 (dividendo, desdobramento,
      grupamento, bonificacao sao fase 2).

      SEGUNDA METADE DA V1 — A DOBRA DAS TRES COLUNAS DE posicao_corrente, POR TIPO,
      PARA OS DEZ. Quem PREENCHE e o F4 (item 5b abaixo); quem FECHA A REGRA e voce,
      aqui, junto do enum — regra escrita por NOME DE TIPO so e conferivel se cobrir
      todos os nomes. A versao anterior deste arquivo escrevia so "na compra..., na
      venda...", e deixava OITO tipos sem dobra, com o I4 afirmando que as tres colunas
      sao dobra pura do livro.
        compra   -> quantidade += qtd_delta; custo_total += valor; pm = custo/quantidade
        aporte   -> IDENTICO a compra (corolario da V2: a diferenca e so o rotulo)
        venda    -> quantidade += qtd_delta (negativo); pm INALTERADO;
                    custo_total = pm x quantidade  (baixa proporcional)
        resgate  -> IDENTICO a venda (vencimento e venda compulsoria)
        cupom    -> quantidade += 0; custo_total INALTERADO; pm INALTERADO.
                    E RENDA, NAO CUSTO DE AQUISICAO: somar o valor ao custo mudaria o
                    preco medio SEM a posicao mudar, e com a posicao zerada no mesmo dia
                    seria DIVISAO POR ZERO.
        ir_retido | iof | a_liquidar | liquidacao -> todas escrevem em caixa:*, entao
                    vale a V4: quantidade += qtd_delta; custo_total = quantidade;
                    pm = 1,000000. Nao ha media ponderada a manter.
        ajuste   -> ver a regra abaixo. NAO E "compra quando qtd_delta > 0".
      A DOBRA TEM UM PARAMETRO DE CORTE, E ELE FAZ PARTE DO NOME: "a dobra da V1 com corte
      em data_evento <= D" percorre SO as linhas com data_evento <= D. posicao_corrente e o
      caso D = INFINITO (e so isso que o "sem filtro por relogio" do I4 quer dizer); o
      snapshot de um dia (F7) e o caso D = aquele dia, NAS TRES COLUNAS; a posicao na data
      de uma corpaction (F9) e o mesmo caso, usando so a primeira. UMA implementacao com um
      parametro, NUNCA DUAS: copiar as tres colunas de posicao_corrente para o snapshot de
      um dia passado grava quantidade de D com preco medio e custo de HOJE.
      A ORDEM DE PERCURSO E PARTE DA DEFINICAO DA DOBRA: data_evento crescente, desempate
      registrado_em, desempate final `id`. A media ponderada E SENSIVEL A ORDEM (compra
      10@100, venda 5, compra 10@200 da pm 166,67; a mesma cesta com a segunda compra
      antes das outras da pm 150), entao "dobra do livro" sem ordem declarada nao nomeia
      UMA funcao. As duas primeiras chaves sao as MESMAS da 7.5 (extrato), de proposito.
      A TERCEIRA existe porque registrado_em NAO desempata sozinho: das seis linhas
      derivadas de um resgate, QUATRO nascem na MESMA TRANSACAO (venda, ir_retido, iof e
      a_liquidar, as quatro com data_evento = dataEvento) e now() no Postgres e hora de
      TRANSACAO — as TRES dessas quatro que caem em caixa:a_liquidar compartilham
      data_evento e registrado_em byte a byte. NAO ESCREVA "as seis nascem na mesma
      transacao": as duas pernas de liquidacao sao de OUTRA transacao, gravadas em D+1
      UTIL por um job (desvio (1) da lista canonica, F5). A conclusao nao muda — tres
      linhas empatadas ja bastam. Hoje esse empate e inofensivo (em caixa:* a V4 fixa
      pm = 1,000000 e custo = quantidade, que nao dependem de ordem), mas a dobra e
      definida como FUNCAO TOTAL, nao pela inofensividade do empate de hoje.
      E E A MESMA TRIPLA que o F8 usa para ORDENAR E PARA PAGINAR o extrato de
      movimentacao: (data_evento, registrado_em, id), nas duas coisas. NAO ESCREVA "e o
      mesmo par (data_evento, id) que o cursor do F8 usa": (data_evento, id) REMOVE
      registrado_em da chave em vez de refina-lo — registrado_em e now() (hora de
      transacao) e id e bigserial alocado no INSERT, entao dois escritores concorrentes
      na mesma (cliente, instrumento, data_evento) se ordenam AO CONTRARIO nos dois
      campos, e o segundo escritor existe POR DESENHO a partir do F5 (o IHostedService de
      liquidacao escreve enquanto o consumidor escreve).
      data_evento DO `ajuste`: E O DA LINHA REVERTIDA. A V2 ja faz o ajuste simetrico
      exato nas duas colunas de valor; a data entra na mesma simetria, porque o estorno
      declara que o fato NAO ACONTECEU, e fato que nao aconteceu nao acontece "hoje". Quem
      registra QUANDO a correcao chegou e registrado_em. REJEITADO: carimbar o ajuste com
      a data de chegada — o snapshot de um dia passado continuaria exibindo um trade
      cancelado PARA SEMPRE, porque o gatilho 1 do F7 dispara recalcular(desde =
      data_evento do movimento) e com data_evento = hoje ele nunca volta ao dia errado.
      FRONTEIRA DO CORTE NO PAR REVERTIDO, E ELA E CONSEQUENCIA E NAO RESTRICAO: o par
      (linha revertida, ajuste que a reverte) so sai da dobra quando AS DUAS linhas tem
      data_evento <= D — e com a data herdada as duas caem SEMPRE do mesmo lado de
      qualquer corte. A regra fica escrita porque e ela que reprova a implementacao que
      carimba o ajuste com a data de chegada: essa quebra as duas coisas de uma vez, parte
      o par no corte e apaga o gatilho 1. NAO invente teste em que so a metade revertida
      do par esteja dentro do corte: com esta decisao ele nao existe.
      REGRA DE FRONTEIRA DE VALOR, OBRIGATORIA porque duas linhas da tabela escrevem uma
      DIVISAO. preco_medio so e DEFINIDO para quantidade > 0, e a divisao custo/quantidade
      so acontece nesse ramo:
        quantidade resultante = 0, QUALQUER TIPO -> preco_medio = 0 e custo_total = 0.
            Estado canonico, independente do caminho (e o mesmo a que a baixa proporcional
            da venda chega), e e ele que impede custo_total / 0 numa compra/aporte que traz
            a posicao DE NEGATIVA PARA ZERO — que e o conserto que o F4 prescreve para
            "venda sem compra", nao uma borda hipotetica.
        quantidade resultante < 0 -> preco_medio INALTERADO, nunca recalculado, portanto
            NUNCA NEGATIVO; custo_total acumula pela regra do tipo. Sem isso, venda sem
            compra de 10 seguida de compra 4 @ 100 da custo 400, quantidade -6 e
            preco_medio -66,67 — e o F5 tributa sobre ele. ("Base = preco medio DO LIVRO"
            (7.3) e ELIPSE: o preco medio e o INSUMO da base, e a base e o GANHO
            (valorFinanceiro - pm x quantidade). A formula esta fechada no F5.) Com quantidade negativa as tres colunas sao PROVISORIAS em
            posicao_corrente E NAO NO LIVRO: a projecao se refaz por dobra, mas o
            ir_retido/iof que o F5 gravar a partir de um pm provisorio esta em tabela
            append-only e SO SAI POR ESTORNO (o F5 tem decisao propria sobre isso).
        quantidade ANTERIOR <= 0 e RESULTANTE > 0 (O CRUZAMENTO — e o conserto MAIS
            PROVAVEL, nao a borda mais rara) -> preco_medio = O PRECO UNITARIO DA PROPRIA
            LINHA QUE CRUZA (valor_financeiro / qtd_delta) e custo_total = preco_medio x
            quantidade resultante. A posicao RECOMECA ali: a parte da compra que cobre o
            negativo esta pagando uma venda que nunca foi lancada, e nao e custo de
            aquisicao do que sobrou. "Falta lancar a compra antiga" costuma ser lancada
            MAIOR que a venda: venda sem compra de 10 + compra 20 @ 100 daria, na regra
            ingenua, quantidade 10, custo 2000 e pm 200, quando a dobra por data vale
            pm 100 e custo 1000 — e esse erro PASSA nas duas guardas que ja existem
            (200 x 10 = 2000 satisfaz o invariante, e 200 >= 0 satisfaz a varredura).
            Vale para compra e aporte, os unicos tipos que cruzam para positivo aplicando
            delta: cupom tem qtd_delta = 0 e nao cruza, caixa:* e fixado pela V4, e o
            ajuste nunca aplica delta (ele re-dobra a chave, e o cruzamento aparece la
            dentro por esta mesma regra). A FRONTEIRA = 0 VEM ANTES DESTA: resultante
            exatamente 0 e pm 0, estado canonico, nao o preco unitario da linha.
        A PARTICAO E PELO PAR (sinal da quantidade ANTERIOR, sinal da RESULTANTE), e por
            isso nao sobra combinacao: resultante = 0 (qualquer anterior); resultante < 0;
            resultante > 0 vindo de anterior <= 0; resultante > 0 vindo de anterior > 0,
            que e a tabela por tipo acima.
      INVARIANTE CONFERIVEL que sai disso: depois de compra/aporte/venda/resgate, COM
      QUANTIDADE > 0, custo_total = pm x quantidade (a menos do arredondamento de 2 casas).
      Um teste que afirma essa igualdade pega a implementacao que baixa uma coluna e esquece
      a outra, e NAO PEGA preco medio negativo: -66,67 x -6 = 400 e algebricamente
      verdadeiro. Para quantidade < 0 o invariante NAO E AFIRMADO — quem cobre esse caso e a
      regra de fronteira acima, com o teste dos TRES desfechos da mesma venda sem compra de
      10 — compra que zera (10 @ 100 -> qtd 0, pm 0, custo 0), compra que nao zera (4 @ 100
      -> qtd -6, custo 400, pm inalterado) e COMPRA QUE CRUZA (20 @ 100 -> qtd 10, pm 100,
      custo 1000, jamais pm 200) — e a assercao preco_medio >= 0 SEMPRE. O terceiro e o
      unico que o invariante E a varredura de sinal aprovam com o valor errado.

      A DOBRA DO `ajuste` — a decisao desta secao. O ajuste DESFAZ a linha que
      ref_estorno aponta. A dobra percorre o livro IGNORANDO OS PARES (linha revertida,
      ajuste que a reverte), com "revertida" resolvido recursivamente: uma linha e
      EFETIVA quando nao existe ajuste EFETIVO apontando para ela (e o que faz estorno DE
      estorno restaurar o original, que a V5 permite de proposito). Como o par soma ZERO
      nas duas colunas (V2), "Σ qtd_delta = quantidade" continua valendo LITERALMENTE e o
      I4 nao muda uma virgula; o que muda e so o calculo de pm/custo_total, que deixa de
      depender da ordem em que o estorno chegou.
      OS NUMEROS QUE OBRIGAM ESSA FORMA:
        compra 10 @ 100        -> qtd 10, custo 1000, pm 100
        venda   4 por Y = 600  -> qtd  6, custo  600, pm 100  (o preco subiu para 150)
        estorno da venda: ajuste qtd_delta +4, valor -600 (simetrico, V2)
          regra ingenua ("qtd_delta > 0, logo compra"):
             custo = 600 + 600 = 1200, pm = 120                 <- ERRADO
          dobra desta secao (o par venda+ajuste sai da dobra):
             qtd 10, custo 1000, pm 100                         <- correto
      Com a regra ingenua o pm fica 20% errado E PERMANENTE: o F5 tributa com "base =
      preco medio do livro" (7.3) e o F7 grava esse pm em CADA SNAPSHOT, que e documento
      que o cliente ve (P2) — e o `ajuste`, cuja razao de existir e desfazer, nao desfaz.
      REJEITADO: regra inversa por tipo em O(1) — acerta o exemplo e ERRA SOB
      INTERCALACAO (compra 10@100, venda 4, compra 10@200, estorno da venda devolve
      custo 3250 quando o livro sem o par vale 3000), e fica ambigua no estorno DE
      estorno.

  V2. O MAPEAMENTO operacao (5.1) -> tipo (7.1). Os dois vocabularios NAO coincidem.
      CONVENCAO DE SINAL, uma vez para todas as linhas: qtd_delta CARREGA O SINAL (entra
      positivo, sai negativo) e valor_financeiro e a MAGNITUDE BRUTA daquela linha, nao
      negativa — e a convencao da 7.1 e da 7.3 ("vencimento -> qtd_delta = -qtd, valor =
      qtd x PU final": valor positivo com quantidade negativa). UNICA EXCECAO: o
      `ajuste`, que carrega o SIMETRICO EXATO da linha revertida NAS DUAS COLUNAS, de
      modo que o par some ZERO em ambas.

      MOVIMENTO PRINCIPAL DO FATO:
      aplicacao -> compra   instrumento do evento, qtd_delta = +quantidade,
                            valor = valorFinanceiro (bruto)
      resgate   -> venda    instrumento do evento, qtd_delta = -quantidade,
                            valor = valorFinanceiro (bruto)
      aporte    -> aporte   INSTRUMENTO DO EVENTO, qtd_delta = +quantidade,
                            valor = valorFinanceiro (bruto)
      estorno   -> ajuste   instrumento do movimento estornado (LOOKUP),
                            qtd_delta e valor simetricos ao estornado,
                            data_evento = a DO MOVIMENTO ESTORNADO (V1)
      `aporte` E OPERACAO COM INSTRUMENTO — aplicacao adicional no titulo. A diferenca
      contra `aplicacao` e SO O ROTULO no extrato de movimentacao (7.5); nao procure
      diferenca de comportamento, nao existe. NAO grave aporte como caixa:BRL com
      qtd_delta = +valorFinanceiro: isso DESCARTARIA o instrumentoId (que Operacoes
      validou contra o catalogo do Hub) e a quantidade, numa tabela sem UPDATE, para
      sempre. Evidencia: Operacao.Create em
      ../operacoes/src/Operacoes.Domain/Operacoes/Operacao.cs exige instrumentoId
      nao-vazio e quantidade > 0 para TODO tipo, aporte incluido.

      LINHAS DERIVADAS — UMA LINHA POR MOVIMENTO, com instrumento, sinal e valor
      fechados. Resgate de q unidades do instrumento X por Y bruto, IR t, IOF f:
        <tradeId>            venda        X                 -q        Y
        ir:<tradeId>         ir_retido    caixa:a_liquidar  -t        t
        iof:<tradeId>        iof          caixa:a_liquidar  -f        f
        aliq:<tradeId>       a_liquidar   caixa:a_liquidar  +Y        Y
        liq:<tradeId>:aliq   liquidacao   caixa:a_liquidar  -(Y-t-f)  Y-t-f   (D+1 util)
        liq:<tradeId>:brl    liquidacao   caixa:BRL         +(Y-t-f)  Y-t-f   (D+1 util)
      Cupom (F9) troca so o principal: (tipo=cupom, instrumento X, qtd_delta 0, valor =
      q x valorPorUnidade), e o dinheiro entra por aliq:cupom:... Vencimento:
      (tipo=resgate, instrumento X, qtd_delta = -q, valor = q x PU final). As linhas de
      reversao (est:..., F5) sao tipo='ajuste' NO MESMO instrumento_id da linha
      revertida, com as duas colunas simetricas, A MESMA data_evento da revertida (V1) e
      ref_estorno apontando para ela.

      a_liquidar E BRUTO, E OS TRIBUTOS DEBITAM caixa:a_liquidar. O direito a receber
      nasce pelo valor cheio e e REDUZIDO PELAS PROPRIAS LINHAS DE TRIBUTO — IR e IOF sao
      retidos NA FONTE. Assim o saldo a receber e uma SOMA DE LINHAS DO LIVRO (+Y -t -f)
      e nao um numero calculado na escrita: divergencia entre tributo lancado e valor a
      receber deixa de ser representavel. REJEITADO: a_liquidar liquido (+(Y-t-f)) com
      ir_retido/iof no instrumento do titulo com qtd_delta = 0 — as linhas de tributo nao
      teriam efeito em projecao nenhuma (documentacao pura) e o tributo voltaria a ser
      desconto embutido, contra a convencao 3 da 7.1.

      A ARITMETICA DO RESGATE — TRES INVARIANTES, E OS DOIS PRIMEIROS SAO INDEPENDENTES
      DE PRECO DE PROPOSITO (patrimonio = Σ quantidade x preco, caixa a preco 1 por V4):
        (1) Σ qtd_delta das linhas do fato em caixa:a_liquidar = Y - t - f depois de D,
            e ZERO depois da liquidacao de D+1;
        (2) Σ qtd_delta(caixa:a_liquidar) + Σ qtd_delta(caixa:BRL) e INALTERADO pelas
            duas pernas — transferencia pura. Corolario: as duas pernas sao caixa:*, a
            preco 1 por definicao, e somam zero, entao A LIQUIDACAO SOZINHA NAO MUDA O
            PATRIMONIO; a variacao entre D e D+1 e EXATAMENTE a variacao de preco da
            posicao residual do titulo, e nada mais;
        (3) a queda de patrimonio entre D-1 e D e t + f SOB A HIPOTESE, DO FIXTURE, DE
            PRECO CONGELADO entre os dois dias. HIPOTESE DE FIXTURE, NUNCA INVARIANTE.
      POR QUE (3) NAO E INVARIANTE, e isso reprova uma implementacao CORRETA se voce o
      escrever como criterio: o snapshot de D-1 vale quantidade x preco(ultimo <= D-1),
      isto e P(D-1), nao o preco de D. Com posicao total Q e caixa preexistente C:
        patrimonio(D-1) = Q.P(D-1) + C
        patrimonio(D)   = (Q-q).P(D) + C + (Y - t - f)
        queda           = Q.P(D-1) - (Q-q).P(D) - Y + t + f
      que so vale t + f se P(D-1) = P(D) E Y = q.P(D). A 8.4 fixa a segunda condicao
      ("livro: venda (qtd -X, valor = X x PU_venda(D))"), o que deixa a PRIMEIRA — preco
      parado de um dia para o outro — como unica hipotese que faz a conta fechar. E preco
      parado e justamente o que nao acontece: a variacao diaria de preco e o motivo de o
      sistema existir. "Entre D e D+1 o patrimonio e constante" tem o mesmo defeito: so e
      verdade porque o exemplo ZERA a posicao. Escreva (3) no FIXTURE, nunca no
      invariante.

      PENDENCIA BLOQUEANTE — caixa:BRL NUNCA E DEBITADO. NENHUMA linha deste roadmap
      debita caixa:BRL: a unica que o escreve e a liquidacao perna 2, sempre +(Y-t-f).
      Nao ha saque, nao ha a_pagar, e aplicacao -> compra grava UMA linha, no instrumento
      do titulo, sem contrapartida. A CONTA: D0 compra 10@100 (patrimonio 1000); D1
      resgate 10 com Y=1000 e t=100 (patrimonio 900); D2 liquidacao (900 em caixa:BRL);
      D3 reaplica 9@100 -> o extrato diz 9x100 + 900 = 1800 e o cliente tem 900.
      SE VOCE ABRIU ESTA FASE E ESTA PENDENCIA AINDA ESTA ABERTA, PARE E PERGUNTE: o F3
      NAO FECHA O VOCABULARIO DO LIVRO SEM ISTO, e movimentos e append-only (nao ha como
      apendar depois a perna que deveria ter nascido junto).
      POR QUE VOCE NAO PODE DECIDIR: o modelo e COERENTE se `aplicacao` significa
      "dinheiro entrou de fora e virou titulo" e ERRADO se a aplicacao consumiu o produto
      de uma liquidacao anterior — as duas leituras sao legitimas, e o TradeRegistered da
      5.1 NAO DIZ QUAL E. Operacoes sabe e nao publica. Escolher aqui e re-derivar rio
      abaixo o que o produtor ja sabe (PADROES 10.32). A correcao e um campo OPCIONAL na
      5.1 do ../plataforma-docs, OUTRO REPO — mesma forma do estornaTradeId, que o F3 do
      operacoes acrescentou a 5.1 ANTES do codigo, como pre-requisito e nao consequencia.
      AS DUAS SAIDAS ALTERNATIVAS, com o custo: (a) perna simetrica (compra e aporte
      debitam caixa:BRL) mostra CAIXA NEGATIVO em toda aplicacao com dinheiro novo,
      porque nao existe evento de deposito na 5.1; (b) livro-caixa PARCIAL declarado
      (caixa:BRL e so o produto de liquidacoes nao reinvestido) SUPERESTIMA o patrimonio
      em toda reaplicacao, mas como LIMITE DECLARADO em vez de erro silencioso — e derruba
      toda afirmacao de "soma dos instrumentos + caixa = patrimonio".

      DUAS ARMADILHAS, e as duas ja apareceram em rascunho:
      - `resgate` do EVENTO nao e `resgate` do LIVRO. No enum, `resgate` e VENCIMENTO
        (a 7.3: "vencimento -> livro: (tipo=resgate, qtd_delta = -qtd, valor = qtd x PU
        final)"). O resgate voluntario que o cliente pede em Operacoes e uma VENDA.
      - `aplicacao` nao existe no enum da 7.1: aplicacao E compra. `aporte` existe, por
        V1, porque e o rotulo que o extrato preserva.

      A DISPENSA DECLARADA DO ESTORNO — a UNICA desta V2, e sem ela a guarda estrita
      abaixo fica ambigua exatamente no caso que mais importa. O TradeRegistered de
      estorno carrega instrumentoId, quantidade e valorFinanceiro PROPRIOS (sao campos
      OBRIGATORIOS do envelope da 5.1, nao opcionais) e o mapeamento manda usar os
      valores do movimento estornado, POR LOOKUP. Regra escrita: esses tres campos do
      evento de estorno sao CONFERIDOS contra o movimento original e NAO SAO USADOS para
      gravar — instrumentoId = instrumento_id do revertido, quantidade = |qtd_delta| do
      revertido, valorFinanceiro = valor_financeiro do revertido. DIVERGIR EM QUALQUER UM
      NAO E CASO FELIZ: hoje a reversao TOTAL seria gravada em silencio sobre um payload
      que pede outra coisa (estorno parcial, ou erro em Operacoes), numa tabela sem
      UPDATE. Desfecho: NADA e gravado, a chave de dedupe NAO e consumida (I13), a
      mensagem estaciona com o motivo `estorno_divergente` e ALERTA — valor que entra na
      lista fechada e sem default de x-custodia-motivo do F4.

      GUARDA ESTRITA (invariante I3): NENHUM CAMPO DO TradeRegistered PODE SER
      DESCARTADO SEM REGRA ESCRITA NESTA V2 — e "conferido contra o original e nao
      usado", acima, E uma regra escrita, que o teste estrito afirma COMO TAL e nao como
      dispensa implicita. Foi um descarte silencioso (instrumentoId e
      quantidade do aporte) que passou pela versao anterior deste roadmap. O teste da
      direcao estrita tem que afirmar isso — cada campo do payload ou aparece numa coluna
      do movimento, ou tem aqui a regra escrita que o dispensa —, senao ele CODIFICA o
      proximo erro de mapeamento em vez de pega-lo.

  V3. ref_externa e NOT NULL (a 7.1 a escreve nullable — DESVIO POR CORRECAO,
      registre-o) e a chave e derivada POR MOVIMENTO, nao por fato. A forma e sempre
      <papel>:<fato>[:<perna>] — o ":" so existe quando ha o que separar, entao com
      <papel> vazio a chave e o <fato> PURO, sem dois-pontos a esquerda. Onde:
        <fato>  = <tradeId>                       (fatos de Operacoes)
                | cupom:<instrumentoId>:<data>    (corpaction — NAO TEM tradeId)
                | venc:<instrumentoId>:<data>
        <papel> = vazio (movimento principal, INCLUSIVE o do estorno) | ir | iof |
                  aliq | liq | est:ir | est:iof | est:aliq | est:liq
                  (NAO existe papel `est` sozinho — a enumeracao completa nunca o usa)
        <perna> = aliq | brl   (SO onde um movimento escreve DUAS linhas)
      ENUMERACAO COMPLETA, que o F5 e o F9 vao copiar:
        trade:      <tradeId> · ir:<tradeId> · iof:<tradeId> · aliq:<tradeId> ·
                    liq:<tradeId>:aliq · liq:<tradeId>:brl
        cupom:      cupom:<instrumentoId>:<data> · ir:cupom:... · aliq:cupom:... ·
                    liq:cupom:...:aliq · liq:cupom:...:brl
        vencimento: venc:<instrumentoId>:<data> · ir:venc:... · iof:venc:... ·
                    aliq:venc:... · liq:venc:...:aliq · liq:venc:...:brl
        estorno (o <fato> e o tradeId DO ESTORNO):
                    <tradeIdEstorno> · est:ir:<tradeIdEstorno> ·
                    est:iof:<tradeIdEstorno> · est:aliq:<tradeIdEstorno> ·
                    est:liq:<tradeIdEstorno>:aliq · est:liq:<tradeIdEstorno>:brl
      POR QUE A PERNA ENTRA NO SUFIXO: a liquidacao e de DUAS pernas (-Y em
      caixa:a_liquidar, +Y em caixa:BRL) e movimentos tem UM instrumento_id por linha —
      com `liq:<tradeId>` as duas linhas COLIDEM no UNIQUE (cliente_id, ref_externa),
      que e o defeito que esta decisao existe para prevenir.
      POR QUE A FAMILIA DE CORPACTION EXISTE: cupom e vencimento produzem, POR CLIENTE,
      o mesmo conjunto de 4-5 linhas que um resgate, e nenhuma tem tradeId.
      A chave e montada por concatenacao em ORDEM FIXA e NUNCA e lida de volta — nao
      faca parsing dela. Um resgate vira 4-5 linhas; com a mesma chave elas colidem, e
      com NULL o Postgres NAO CONSTRANGE — cada derivada duplicaria para sempre numa
      tabela sem DELETE.

  V4. caixa:BRL e caixa:a_liquidar sao IDS LOCAIS desta casa, NAO ids do Hub — a ADR-4
      proibe uma TERCEIRA IDENTIDADE para instrumento DO HUB (de-para, uuid interno);
      caixa nao existe no Hub e nao tem id la para traduzir. Nao restrinja
      instrumento_id de um jeito que os rejeite.
      E eles tem PRECO 1,000000 POR DEFINICAO — nao e observacao, nao e forward-fill,
      nao e "ultimo <= D". Consequencias que precisam ser escritas juntas: (1) caixa:*
      nunca recebe PriceObserved e nunca e pedido ao Hub no bootstrap do F6; (2) fica
      FORA do alerta de "preco ausente" do F7, senao ele dispara todo dia para sempre;
      (3) snapshots_posicao.preco e NOT NULL, entao sem isto NAO NASCERIA linha de
      snapshot de caixa nenhuma — o F5 escrituraria o limbo D->D+1 no livro e o F7 o
      apagaria do documento, e o extrato de posicao do F8 ficaria sem a parcela de caixa.
      NAO LEIA a igualdade "soma dos instrumentos + caixa = patrimonio diario" (7.5) como
      assercao fechada: e exatamente ela que a PENDENCIA BLOQUEANTE do caixa:BRL, acima,
      deixa em aberto. O que este item (3) afirma e so que SEM PRECO POR DEFINICAO NAO
      EXISTE LINHA DE CAIXA NO SNAPSHOT, e isso vale sob qualquer desfecho da pendencia.
      Em posicao_corrente, linha de caixa COM quantidade > 0 tem preco_medio = 1,000000 e
      custo_total = quantidade.
      A REGRA DE FRONTEIRA DA V1 PRECEDE ESTA, INCLUSIVE PARA caixa:* — NAO ESCREVA a
      frase acima sem o qualificador. caixa:a_liquidar volta a ZERO depois de toda
      liquidacao (invariante (1) da aritmetica do resgate), entao esse e o estado
      PERMANENTE da linha e nao uma borda: quantidade = 0 => preco_medio = 0 e
      custo_total = 0, PARA QUALQUER TIPO, caixa incluido. A V4 vale so para
      quantidade > 0. Sem esta precedencia, o handler incremental do F4/F5, o comando de
      reconstrucao e a RECONCILIACAO DO F7 podem escolher regras diferentes, e a
      reconciliacao — o unico detector automatico de divergencia deste sistema — passa a
      alertar EM TODA OPERACAO NORMAL. (A regra do CRUZAMENTO exclui caixa:*
      nominalmente; as regras "= 0" e "< 0" NAO excluem, e era essa assimetria que fazia
      parecer que elas nao se aplicavam.)
      CHECK ENUMERANDO os ids de caixa permitidos (caixa:BRL, caixa:a_liquidar), pela
      PADROES 10.21: como a identidade e gravada CRUA (sem ToLowerInvariant — item 4 de
      DECISOES JA TOMADAS, mais abaixo; o vocabulario vai de V1 a V5 e nao existe "V6"), um
      `caixa:brl` digitado uma vez viraria um SEGUNDO instrumento de caixa para sempre,
      e o patrimonio passaria a somar duas linhas onde havia uma. Um caixa:USD futuro
      custa uma migration de uma linha.

  V5. INDICE UNIQUE (ref_estorno) WHERE ref_estorno IS NOT NULL, NOMEADO com
      HasDatabaseName (PADROES 10.23). Sem ele, DOIS ajustes podem apontar para o MESMO
      movimento: cada estorno chega com tradeId proprio, os dois passam pelo UNIQUE
      (cliente_id, ref_externa) sem conflito, e a posicao e revertida DUAS VEZES numa
      tabela sem UPDATE nem DELETE. E a constraint que a PADROES 10.21 nomeia junto com
      as outras, no incidente que esta fase cita como fundamento, e o molde operacoes ja
      a tinha. ELA NAO FECHA A PORTA DE CORRECAO: estorno DE estorno aponta para uma
      linha DIFERENTE (o ajuste anterior), nao colide, e continua PERMITIDO de proposito
      — a excecao nomeada da 10.21. Prove as duas coisas.

  ============================================================================

  DECISOES JA TOMADAS, que voce implementa e nao rediscute:

  1. movimentos e APPEND-ONLY por TRIGGER na migration (UPDATE e DELETE bloqueados),
     NUNCA por REVOKE: a role `custodia` e dona do database e roda migrations no boot,
     entao o revoke seria decoracao e quebraria migration de dados no laco de deploy
     (motivo verificado no F2 do operacoes). Deixe o TRUNCATE aberto de proposito
     (trigger de linha nao dispara em TRUNCATE) — e o que torna fixture possivel.
     Estorno de estorno continua PERMITIDO de proposito (PADROES 10.21, a excecao).

  2. DECISAO A, metade irreversivel: CHECK tornando ref_estorno NOT NULL quando
     tipo = 'ajuste' e NULL nos demais tipos; FK COMPOSTA com cliente_id (ajuste nao
     cruza cliente); CHECK de nao-auto-referencia. Um estorno orfao tem que ser
     IMPOSSIVEL de nascer. Ao declarar a FK composta, nomeie o indice de cobertura com
     HasDatabaseName, senao o EF gera um IX_ em PascalCase que ninguem escreveu
     (PADROES 10.23).

  3. ref_externa: implemente exatamente a V3 acima (NOT NULL, chave por MOVIMENTO, com
     as pernas e a familia de corpaction). Nao reescreva a convencao.

  4. Identificadores gravados CRUS, com Trim() e NADA MAIS (PADROES 10.24, ADR-4). SEM
     ToLowerInvariant, ao contrario do molde Hub.Domain.Instrumentos.InstrumentoId: la o
     slug e do proprio Hub, aqui instrumento_id e identidade de OUTRO contexto. Trim
     remove ruido de transporte; baixar caixa transforma o valor. (E por isso que o
     CHECK de caixa da V4 e obrigatorio.)

  5. As QUATRO projecoes (posicao_corrente, preco_atual, historico_precos,
     snapshots_posicao) nascem SEM FK para movimentos e SEM trigger — elas sao
     DESCARTAVEIS, e FK atrapalharia o TRUNCATE + reconstrucao que as define.

  5b. posicao_corrente tem TRES colunas de valor (quantidade, preco_medio, custo_total)
     e as tres nascem aqui. QUEM AS PREENCHE E O F4, na mesma transacao; QUEM FECHA A
     REGRA DE DOBRA E VOCE, na segunda metade da V1, para os DEZ tipos — escreva isso
     no lugar onde o proximo executor vai ler (nome de teste, estrutura), nao em
     comentario. Coluna sem dono e o que produz o defeito: o F5 tributa com "base =
     preco medio DO LIVRO" (7.3) e o F7 grava preco_medio e custo em cada snapshot
     (7.4); um executor do F5 encontrando a coluna vazia inventaria a regra de preco
     medio DENTRO do calculo de imposto. E REGRA DE DOBRA COBRINDO SO DOIS DOS DEZ TIPOS
     produz o mesmo defeito com a suite verde — foi o que a versao anterior deste arquivo
     fazia.

  5c. preco_atual MANTEM A PK DA 7.1 (instrumento_id) e guarda SO o campoPosicao — a DDL
     de la da UMA LINHA POR INSTRUMENTO, com uma coluna `campo` singular. Quem guarda
     TODOS os campos e o historico_precos, com a chave inteira da 5.1. NAO troque a PK
     para (instrumento_id, campo): o F6 vai enunciar a monotonicidade da projecao POR
     INSTRUMENTO por causa desta decisao, e ela precisa estar fechada antes do schema.

  5d. GUARDA CONTRA data_evento NO FUTURO, por TRIGGER, nao por CHECK: o Postgres NAO
     ACEITA `CHECK (data_evento <= ...)` com funcao nao-IMMUTABLE. Ponha-a numa trigger
     BEFORE INSERT, ao lado da de imutabilidade. Movimento com data no
     futuro poe no livro um fato que ainda nao aconteceu, e o livro e append-only
     (PADROES 10.21). E segunda linha de defesa — a 6.1 camada 2 ja rejeita na borda, em
     Operacoes, que e OUTRO REPO — e nao bloqueia caminho nenhum deste roadmap: o job do
     F5 insere a liquidacao QUANDO ELA VENCE, nunca antes.

  5e. O FUSO HORARIO DE NEGOCIO E `America/Sao_Paulo` (UTC-3), e VOCE O FIXA AQUI porque e
     aqui que a primeira `date` vira dado (ARQUITETURA 11: "relogio dos jobs em UTC-3;
     D+1 de liquidacao conta DIAS UTEIS"). A trigger do item 5d compara contra
     `(now() AT TIME ZONE 'America/Sao_Paulo')::date`, NUNCA contra `current_date`:
     current_date e o dia no fuso do SERVIDOR, que num container padrao e UTC. TODA coluna
     `date` deste schema e uma data nesse fuso, e F5 e F7 HERDAM a regra ("hoje", "dia
     util" e "12:00" sao nesse fuso; a regra do alerta das 12:00 do F7 carrega o offset,
     porque o avaliador do Grafana roda em UTC). SEM ISSO, entre 00:00 e 03:00 BRT o dia
     BRT ja virou e o UTC nao, e o job de CICLO CURTO do F5 — que roda 24 h por dia —
     tentaria inserir data_evento = hoje_BRT e a trigger o REJEITARIA como data futura,
     todo dia, por tres horas, com um desfecho que nao tem nome em lugar nenhum.
     NAO "resolva" isso com TZ=America/Sao_Paulo no container: isso poe em configuracao de
     ambiente o que e regra de DADO, some no primeiro compose que esquecer a variavel, e
     nao alcanca o avaliador do Grafana, que e de outro repo.

  6. /health/ready endurecido: alem de CanConnectAsync, existencia FISICA das tabelas E
     DAS TRIGGERS, derivada de db.Model (GetEntityTypes, GetDeclaredTriggers) — NUNCA
     lista escrita a mao (PADROES 10.22). GetPendingMigrationsAsync prova historico, nao
     schema. Objeto cuja ausencia permite corrupcao silenciosa vai para a SONDA, nao
     para a lista do "nao cobre". Molde:
     ../operacoes/src/Operacoes.API/Extensions/PendingMigrationsHealthCheck.cs.

  7. Constantes de precisao/escala dos numeric num dono so, com teste que le
     numeric_precision/numeric_scale do information_schema e confronta (PADROES 10.25 —
     o caso de ESCALA e pior que o de magnitude: o Postgres arredonda em silencio).

  REGRA DO REPO, que sai desta fase e vale para as seguintes: A CHAVE DE DEDUPE SO SE
  CONSOME QUANDO O MOVIMENTO GRAVADO ESTA CORRETO E COMPLETO.

  NAO ENTRA: consumidor, handler, tributo, snapshot calculado, extrato, worker,
  endpoint. A validacao de magnitude/escala no Dominio e do F4 (nasce com a entidade);
  aqui nascem as CONSTANTES e o teste que as confronta com o banco.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta — no F2 do operacoes cada rodada de
  correcao gerou um defeito novo. Peca ao guardiao que confira tambem os textos que VOCE
  escreveu. Commite antes de rodar o revisor: ele muta a implementacao de proposito, e
  um git checkout numa entrega nao commitada apaga o trabalho em silencio.
  ```

  <br>**Pronto:** migrations aplicando no boot; `\d movimentos`, `\d posicao_corrente`,
  `\d preco_atual`, `\d historico_precos`, `\d snapshots_posicao` conferidos contra a
  §7.1. Direção **estrita**, toda executada contra Postgres real e não afirmada:
  (a) `UPDATE` e `DELETE` em `movimentos` **recusados** pela trigger;
  (b) `INSERT` de `tipo='ajuste'` com `ref_estorno IS NULL` **recusado** — é a prova de que
  a decisão A ficou no schema e não na disciplina;
  (c) `INSERT` de ajuste apontando para movimento de **outro cliente** recusado;
  (d) `INSERT` com `ref_estorno = id` recusado;
  (e) `INSERT` com `ref_externa` nula, vazia ou só espaços recusado — entrada malformada
  não é sinônimo de ausente (§10.24);
  (f) as **constantes** de precisão/escala batendo com `numeric_precision`/`numeric_scale`
  lidos do `information_schema`, e o banco rejeitando (magnitude) ou **arredondando em
  silêncio** (escala) exatamente como a constante prevê — `qtd_delta` com escala 9 e com 11
  dígitos inteiros (§10.25). *O cruzamento **Domínio × banco** (`SchemaTests.DominioEBanco_
  Concordam*` do molde) é do Pronto do **F4**, não deste:* o "NÃO ENTRA" desta fase adia a
  validação de escala do Domínio para lá, e sem validação de Domínio não existe veredito de
  Domínio para comparar — a versão anterior deste Pronto exigia um veredito que a própria
  fase tinha adiado;
  (g) **controle negativo da sonda**: `DROP TRIGGER` da imutabilidade por fora deixa o
  `/health/ready` **Unhealthy**, e recriá-la o devolve a Healthy — foi essa exata lacuna
  que a primeira sonda do `operacoes` deixou passar;
  (h) varredura de `pg_indexes` reprovando **qualquer** índice fora da convenção, não só os
  que alguém lembrou de nomear (§10.23);
  (i) varredura de `pg_tables` provando que **não existe** tabela de clientes nem de
  instrumentos neste banco — a direção estrita de I11/ADR-4;
  (j) um teste que lê `pg_get_constraintdef` do CHECK de `tipo` e compara com **a lista
  literal da V1** (`compra | venda | aporte | cupom | resgate | ir_retido | iof |
  a_liquidar | liquidacao | ajuste`), que é a mesma lista do Domínio — as duas cópias não
  podem divergir, e a lista **está escrita neste arquivo** justamente para o teste ter
  contra o que comparar em vez de ser inventada pelo executor;
  (k) **o conjunto completo de um resgate gravado numa transação só** — venda, `ir_retido`,
  `iof`, `a_liquidar` e **as DUAS pernas** da liquidação — provando que **nenhuma
  `ref_externa` colide**. O Pronto anterior só testava `ref_externa` nula/vazia, e a
  colisão das duas pernas passaria intacta por ele. **E o mesmo conjunto serve de prova
  aritmética da V2**, com os números da tabela de linhas derivadas, nos **dois invariantes
  independentes de preço**: `Σ qtd_delta` de
  `caixa:a_liquidar` = `Y − t − f` depois de D e **zero** depois da liquidação; `Σ qtd_delta`
  de `caixa:BRL` = `Y − t − f`; e `Σ qtd_delta(caixa:a_liquidar) + Σ qtd_delta(caixa:BRL)`
  **inalterado** pelas duas pernas — transferência pura, e é dele que sai "a liquidação
  sozinha não muda o patrimônio", sem hipótese nenhuma sobre preço. *A versão anterior deste
  Pronto exigia "patrimônio igual em D e em D+1", que só fecha a preço constante e com a
  posição inteira liquidada — critério que reprova a implementação correta.* É a aritmética
  escrita na V2, executada — e é ela que impede a próxima fase de
  gravar `a_liquidar` líquido por conta própria;
  (l) o conjunto completo de um **cupom** e o de um **vencimento** gravados do mesmo jeito
  (chaves da família de corpaction), e o conjunto de **reversão de um resgate**
  (`est:ir:` … `est:liq:…:brl`) — as três famílias da V3 exercitadas, não só a de trade;
  (m) **V5 nos dois sentidos:** segundo `INSERT` de `ajuste` apontando para o **mesmo**
  movimento **recusado** pelo banco (estrita), e um `ajuste` apontando para um `ajuste`
  anterior **aceito** (controle positivo — sem ele a constraint poderia estar fechando a
  última porta de correção, que é a exceção que a §10.21 nomeia);
  (n) **V4:** `INSERT` com `instrumento_id = 'caixa:brl'` (caixa baixa) **recusado** pelo
  CHECK, e `'caixa:BRL'` e `'caixa:a_liquidar'` **aceitos** — controle negativo e positivo;
  (o) `INSERT` de `tipo = 'aporte'` **aceito**, com `instrumento_id` **do título** e
  `qtd_delta > 0` (é a prova de que o desvio por correção da V1 chegou ao banco e de que o
  `aporte` não virou linha de caixa, que era o erro da versão anterior deste arquivo);
  (p) `INSERT` com `data_evento = (now() AT TIME ZONE 'America/Sao_Paulo')::date + 1`
  **recusado** pela trigger, e a **mesma expressão sem o `+ 1`** **aceita** — controle
  negativo e positivo do dado sem conserto, a §10.21 aplicada ao futuro e não só ao passado.
  **E o fuso faz parte da asserção, não é enfeite:** um teste escrito com `current_date`
  passaria com a trigger comparando em UTC, que é o defeito que a decisão do fuso fecha. O
  teste que **separa** as duas implementações é o de fronteira: com a sessão do Postgres em
  `SET TIME ZONE 'UTC'` e o relógio dentro da janela 00:00–03:00 BRT (injetada, não
  esperada), o `INSERT` com a data **BRT de hoje** tem de ser **aceito** — com
  `current_date` ele seria recusado como futuro;

- [ ] **F4** — consumidor de `trades.registered`: o livro, e a política para o evento fora
  de ordem. **Dependência externa nova: o `operacoes` publicando — e ele já publica.**

  `BackgroundService` consumindo a `custodia.prices`, **ack manual** após persistir o
  efeito, `BasicQos(prefetchCount: 1)`, processamento serial, uma instância.
  Redeclaração da topologia no boot com os **mesmos** argumentos do F2 (406 se divergirem,
  que é falha desejável). Handler de `TradeRegistered`: uma linha em `movimentos` com o
  **fato**, traduzida pelo **mapeamento V2 do F3** — `aplicacao → compra`,
  `resgate → venda`, `aporte → aporte` **no instrumento do evento** (não em `caixa:BRL`:
  nenhum campo do payload se descarta sem regra escrita na V2), `estorno → ajuste` com
  `ref_estorno` resolvido por lookup de `ref_externa = estornaTradeId` do **mesmo**
  cliente —, mais
  `posicao_corrente` atualizada **na mesma transação, nas TRÊS colunas**. Dedupe por
  `UNIQUE (cliente_id, ref_externa)`. Validação de magnitude **e escala** dos `numeric` no
  Domínio (§10.25), agora que existe escritor. Métricas de consumo, e o alerta de
  profundidade/idade da fila, da DLQ, do retry e do parking — o substituto agendado no F1.

  **O vocabulário é o do F3, literalmente, e usar outro nome para a mesma linha é
  defeito.** Vale sobretudo para a armadilha V2: `resgate` na §5.1 é uma **venda** no
  livro; `resgate` no enum do livro é **vencimento**, e vencimento é do F9.

  **O primeiro boot desta fase DRENA o backlog acumulado desde o F2.** Isso é o caso
  normal, não o excepcional: o teste de integração tem que exercitar **fila cheia**, e a
  medição de recurso tem que ser feita **durante o dreno**, que é o pico.

  **Decisões desta fase:**

  - **Decisão A, a metade comportamental** (o racional inteiro e os rejeitados estão na
    Decisão A do topo deste arquivo; aqui fica a regra operativa). Estorno órfão →
    **republicado na `custodia.retry`** pelo `custodia.retry.in`, e a mensagem original
    **confirmada** — não grava nada e **não consome a chave de dedupe** (I13). Estourado o
    teto de voltas, vai para a `custodia.parked` com motivo `estorno_orfao_expirado` e
    alerta, nunca para a DLQ. **`nack(requeue: true)` está proibido aqui: head-of-line
    blocking.** Três detalhes de implementação sem os quais o desenho não funciona, e nenhum
    deles é dedutível do parágrafo acima:
    1. **copie integralmente os headers da mensagem recebida no republish.** O `x-death` é
       do broker, mas o republish é um **publish novo** e só carrega o que o publicador
       setar; sem a cópia o contador zera a cada volta, o teto nunca fecha e o
       `estorno_orfao_expirado` **nunca é emitido** — laço infinito com a suíte verde;
    2. **ordem obrigatória: publica → espera o publisher confirm → só então ack.**
       `trades.registered` não tem recuperação por contrato; ack antes do confirm é perda
       definitiva e silenciosa. Confirm negado ou timeout ⇒ `nack(requeue: true)` da
       original — aqui o requeue é o certo, porque a alternativa é perder. **E o confirm
       negado PERSISTENTE tem ramo próprio, senão esta regra produz os dois desfechos que a
       Decisão A rejeita:** com prefetch 1 e consumo serial, o `nack(requeue: true)`
       reentrega **na hora**, e se a causa não for transitória — o caso previsto é a
       `custodia.retry` com `x-overflow: reject-publish` cheia (saída (i) do F2), cujo
       reject **é** um confirm negado — o resultado é `nack → reentrega → publish → reject
       → nack` sem atraso: laço quente num host de um núcleo (§10.13) e `x-delivery-limit`
       queimado em milissegundos, levando à DLQ que este roadmap declara sem história de
       dreno. Regra: **teto de tentativas de republish para a mesma entrega**; estourado, a
       mensagem vai para a `custodia.parked` pelo `custodia.parking` com motivo
       **`retry_indisponivel`** e alerta;
    3. **o teto é o que entrega valor, não a cura.** O órfão por corrida é raro (FK composta
       em Operações, relay em ordem de `id`, um confirm por vez); o que vai acontecer de
       verdade são os casos **incuráveis**, e para eles o retry só adia. Cortar escopo aqui
       significa **reduzir as voltas**, nunca remover o parking.
  - **Corolário da §10.32, e é ele que torna o lookup insubstituível:** o payload de
    `trades.registered` (§5.1) **não carrega o tipo da operação estornada**. O sinal do
    ajuste (somar ou subtrair) só é conhecido pelo lookup do movimento original no livro.
    Escrito aqui para fechar a porta à "simplificação" de gravar o estorno só com a
    referência externa — ela pareceria resolver a comutatividade e reintroduziria a
    dependência de ordem por outra porta, além de exigir campo novo na §5.1 e mudança em
    **Operações** antes deste código. A §5.1 já registra que a Custódia "tem o slot
    (`movimentos.ref_estorno`) e a máquina para preenchê-lo... e resolve o `movimentos.id`
    com um lookup".
  - **O lookup é em DOIS PASSOS, senão dois desfechos que a fase exige "nomeados e
    distinguíveis" ficam indistinguíveis.** Com um lookup só (`ref_externa = estornaTradeId`
    **do mesmo cliente**), "`estornaTradeId` de outro cliente" e "`estornaTradeId`
    inexistente" produzem a mesma coisa — zero linhas — e o cross-client viraria órfão,
    ficaria 10 voltas de 30 s na `custodia.retry` e estacionaria como
    `estorno_orfao_expirado`, quando **esperar nunca cura** um estorno que aponta para outro
    cliente. Regra: (1) busca no **mesmo cliente** — achou, grava; (2) não achou, busca em
    **qualquer cliente** — achou, é `estorno_cliente_divergente`: **parking imediato e
    alerta, sem passar pelo retry**; (3) não achou em ninguém: é órfão, e vale a Decisão A.
    **E achado o movimento no mesmo cliente, o passo (1) ainda não terminou: os três campos
    próprios do payload de estorno (`instrumentoId`, `quantidade`, `valorFinanceiro`) são
    CONFERIDOS contra ele** — é a dispensa declarada da V2 do F3. Divergindo qualquer um,
    **nada é gravado, a chave de dedupe não é consumida (I13)** e a mensagem estaciona como
    `estorno_divergente`, com alerta. Sem esse passo, um estorno parcial (ou um erro em
    Operações) grava a reversão **total** em silêncio, numa tabela sem UPDATE, e não há
    motivo nomeado que o distinga de um estorno correto.
  - **Violação do V5 tem desfecho, e sem ele vira mensagem-veneno.** Um segundo estorno do
    **mesmo** movimento chega com `tradeId` próprio, passa pelo `UNIQUE (cliente_id,
    ref_externa)` — é exatamente o argumento que sustenta o V5 — e estoura no
    `UNIQUE (ref_estorno)`, **em toda reentrega**. Sem tratamento nomeado isso queima o
    `x-delivery-limit` e cai na DLQ, que este roadmap declara sem história de dreno. Regra:
    violação daquele índice único é motivo `estorno_duplicado` → **parking + alerta**, e o
    caso entra na direção estrita.
  - **Envelope `v` desconhecido tem política, e ela não é "tenta assim mesmo".** A §5.1 diz
    que campo novo é sempre opcional e que **mudança incompatível incrementa `v`**; nenhuma
    fase decidia o que o consumidor faz com um `v` que ele não conhece. Regra: `v` não
    suportado → motivo `versao_nao_suportada`, **parking e alerta**, nunca ack-e-descarta e
    nunca parsing otimista — o parking é o que preserva a mensagem até alguém ensinar o
    consumidor a lê-la. Entra também na direção estrita.
  - **Valor monetário chega como STRING DECIMAL, nunca float JSON (§5.1)** — o parsing é
    `decimal` com `InvariantCulture`, e falha de parsing é `payload_invalido`, não zero. A
    prova estrita de "decimal fora da escala da coluna" pressupõe que esse parsing existe e
    é exato; com `double` no meio do caminho ela testaria outra coisa.
  - **Estacionamento (`custodia.parked`), e o corolário que propaga:** evento de tipo que
    esta fase ainda não sabe escriturar — `prices.*`, `corpactions.*`, `eod.ready`, todos
    bindados desde o F2 — é **republicado pelo exchange `custodia.parking`, que entrega na
    `custodia.parked`, e só então confirmado** (nunca direto na fila: a default exchange com
    routing key = nome da fila está **rejeitada nominalmente** no F2).
    Nunca ack-e-descarta (perderia corpaction, que é irrecuperável) e nunca `nack` em laço:
    com `x-delivery-limit` declarado, requeue por semanas estoura o limite e dead-letra a
    mensagem, que é exatamente a perda que se quer impedir — e até lá é laço quente com
    prefetch 1 num host de **um** núcleo (§10.13). *Rejeitado:* bindar só o que já se trata
    e acrescentar binding por fase — reabre exatamente a janela de perda silenciosa que o
    F2 fechou.
  - **O motivo viaja num cabeçalho OBRIGATÓRIO, `x-custodia-motivo`, SEM default** — e é
    quem estaciona que o preenche, no ponto onde a informação é conhecida (§10.32: "faça a
    distinção viajar com o dado, num campo obrigatório; default faz um produtor futuro
    esquecer de marcar, em silêncio"). Valores desta fase: `tipo_nao_tratado_prices`,
    `tipo_nao_tratado_corpactions`, `tipo_nao_tratado_eod`, `estorno_orfao_expirado`,
    `estorno_cliente_divergente`, `estorno_duplicado`, `estorno_divergente`,
    `retry_indisponivel`, `versao_nao_suportada`,
    `payload_invalido`. **São DEZ nesta fase, e a lista é fechada e sem default**; duas fases
    seguintes acrescentam **um valor cada**, e os dois já estão nomeados aqui para a regra
    "sem default" não ser furada por uma fase que só diz "com motivo nomeado": o **F7**
    acrescenta **`intervalo_acima_do_teto`** (o handler de `eod.ready` recusando um intervalo
    de materialização acima do teto configurado, em vez de entrar em laço de reentrega que
    nunca fecha) e o **F9** acrescenta **`acao_desconhecida`**. **Com os dois, são DOZE no
    roadmap inteiro** — recontados contra este arquivo, não copiados. Os dois últimos a entrar,
    com a decisão que os criou: **`estorno_divergente`** é a dispensa declarada do estorno
    na V2 do F3 — os três campos próprios do payload de estorno são **conferidos** contra o
    movimento original, e divergência não pode ser aplicada em silêncio; **`retry_indisponivel`**
    é o ramo de **confirm negado persistente** da Decisão A, sem o qual o republish em
    `custodia.retry` cheia vira laço quente e DLQ. *Acrescentar valor a esta lista é edição
    em mais de um lugar de propósito: a prosa aqui, o prompt abaixo e o desfecho nomeado na
    direção estrita — uma lista declarada fechada que cresce em um lugar só é uma lista com
    default informal.* *Rejeitado:* o drenador decidir por **routing key** — é
    re-derivação do que quem estacionou já sabia, e apodrece no primeiro caso novo (dois
    motivos diferentes podem chegar pela mesma routing key: um `prices.td` parado por tipo
    não tratado e um `prices.td` parado por payload inválido não se drenam juntos).
  - **A drenagem é um laço de consumo, e a condição de parada dele é classificada** —
    §10.31, a terceira armadilha do `CLAUDE.md`. Mecânica decidida aqui, porque toda fase
    seguinte a usa: o drenador é comando administrativo dentro do container, recebe **um
    motivo** como argumento, lê `messages_ready` da `custodia.parked` **antes de começar**,
    consome **no máximo** esse tanto, processa as do motivo pedido e **republica as
    demais no fim da própria fila** pelo `custodia.parking`, preservando o cabeçalho
    `x-custodia-motivo` e carimbando `x-custodia-passagem-id` com o id da passagem corrente
    (sobrescrevendo o de passagens anteriores).
    **TRÊS nomes, um por coisa.** Na versão anterior um único `N` carregava três sentidos no
    mesmo bloco — estoque, teto do laço e contagem do motivo — e foi por isso que a prosa, o
    prompt e o corolário passaram a dizer coisas diferentes. **`N`** = o **estoque** lido
    antes de começar (`messages_ready`), que é também o **teto do laço**; **`n_motivo`** =
    quantas mensagens **do motivo pedido** foram examinadas **e processadas** nesta
    passagem; **`residual_motivo`** = quantas mensagens **do motivo pedido** foram examinadas e
    **republicadas sem processar** (falha de handler, payload que volta a estacionar).
    **O procedimento de decisão é uma ÁRVORE de QUATRO perguntas, nesta ordem, e cada FOLHA
    dela é um desfecho — são SEIS:** (1) *`N` está acima do teto configurado?* (2) *a
    passagem examinou as `N`?* — e, quando a resposta a (2) é **não**, a subpergunta (2b)
    *por quê: uma mensagem desta passagem deu a volta, ou outra coisa?* — (3) *quanto vale
    `residual_motivo`?* (4) *quanto vale `n_motivo`?*. *Duas versões anteriores deste bloco
    erraram aqui, e o erro foi o mesmo nas duas — perguntar menos do que os rótulos exigem, e
    depois declarar a lista exaustiva. A primeira dizia "quatro desfechos, exaustivos por
    construção" e perguntava só "as `N` foram examinadas?" e "quanto vale `n_motivo`?": o
    residual não era olhado, e o estado `N` examinadas + `n_motivo ≥ 1` +
    `residual_motivo > 0` — uma mensagem do motivo pedido examinada e **não** processada —
    ficava **sem classe**, que é o "sucesso com conjunto parcial" que a §10.31 manda devolver
    como falha. A segunda acrescentou o PARCIAL e fechou esse ramo, mas deixou o **vizinho**:
    "as `N` foram examinadas? **não**" não selecionava rótulo nenhum, porque os dois `LIMITE`
    eram definidos por critérios que não estavam entre as perguntas, e existe estado real que
    responde "não" e **não** é nenhum dos dois — a `custodia.parked` **purgada** durante a
    passagem (a fila esvazia em `k < N`, o teto não estourou e nada deu a volta), o
    cancelamento/shutdown do container no meio, a queda de conexão com o broker. É por isso
    que a pergunta sobre as `N` virou duas e existe um sexto rótulo.*
    - **COMPLETUDE** = as `N` foram examinadas, `residual_motivo = 0` e
      `n_motivo ≥ 1` → sucesso, **e a passagem publica o residual por motivo** (é essa a
      origem da contagem, não um gauge de processo).
    - **PARCIAL** = as `N` foram examinadas e `residual_motivo > 0` → **falha**, para
      **qualquer** valor de `n_motivo`. É o caso da mensagem daquele motivo que o handler
      examinou e **não** conseguiu processar, e ele cobre também o extremo em que **todas**
      falharam (`n_motivo = 0` com `residual_motivo > 0`) — que não é "vazio do motivo"
      coisa nenhuma. Vem **antes** da pergunta sobre `n_motivo` de propósito: processar 9 de
      10 é conjunto parcial, e conjunto parcial devolvido como sucesso é a §10.31 literal.
    - **VAZIO_DO_MOTIVO** = as `N` foram examinadas, `residual_motivo = 0` e `n_motivo = 0`,
      **para qualquer `N`, `N = 0` incluído** → **INCONCLUSIVO, nunca sucesso**. É o desfecho que faltava: fila
      com mensagens de **outros** motivos e **zero** do motivo pedido — o estado normal de
      qualquer **segunda** execução do drenador — não era COMPLETUDE (falta `n_motivo ≥ 1`),
      não era LIMITE e não era "fila vazia", e ficava **sem classe**, que é exatamente o que
      a §10.31 proíbe. E ancorar a guarda em `N = 0` era ancorá-la na variável errada:
      **uma** mensagem de outro motivo na fila já faz `N ≠ 0` e desliga a guarda, justamente
      nos cenários que ela existe para pegar — binding do F2 que nunca funcionou, consumidor
      que deu ack-e-descarta (o típico: `corpactions` descartado com `prices` estacionado),
      drenador que nunca rodou, `custodia.parked` purgada.
    - **LIMITE por teto** = `N` acima do teto configurado → **falha**, nunca sucesso com
      conjunto parcial. É a **pergunta (1)**, respondida **antes** de a passagem começar, com
      o `N` que ela acabou de ler — por isso ela não compete com a pergunta (2): quando o
      teto estoura, nenhuma mensagem chegou a ser examinada, e o desfecho já está decidido.
    - **LIMITE por volta** = uma mensagem que já foi examinada **NESTA** passagem reaparece
      antes de as `N` terem sido examinadas → **falha**. *Só é detectável porque o drenador
      carimba um `x-custodia-passagem-id` (sobrescrito a cada passagem) em tudo que ele
      republica: consumir uma mensagem que já traz o id da passagem **corrente** é a
      definição de "deu a volta". A formulação anterior — "uma mensagem que volta a aparecer
      **sem ter sido examinada**" — era autocontraditória (voltar a aparecer implica ter
      sido consumida e republicada, isto é, examinada) e, sem identidade preservada no
      republish, inimplementável: o ramo sobrava como "`N` acima do teto" e mais nada.*
      **ESTA FOLHA É INALCANÇÁVEL SOB A MECÂNICA PRESCRITA, e isso é propriedade escrita, não
      descuido — sem esta frase o Pronto (iv) manda provar o impossível e o executor de boa-fé
      só fecha mutando a implementação.** A `custodia.parked` é FIFO e não tem outro
      consumidor: as `N` mensagens originais ocupam as `N` primeiras posições e tudo que a
      passagem republica entra **atrás** delas; consumindo **no máximo `N`**, a passagem nunca
      alcança a posição `N+1`. *Contraexemplos tentados, e nenhum abre:* mensagem estacionada
      **durante** a passagem também entra atrás; `messages_ready` só pode ler **a menos**, não
      a mais; e passagem concorrente carimba **outro** id, que não é o da passagem corrente.
      **Então por que a folha existe:** ela é a **guarda de defesa em profundidade** contra a
      implementação que **não** respeita o teto — "consuma até a fila esvaziar" é a leitura
      mais natural de "drenar", e sob ela a passagem republica e reconsome o que ela mesma
      republicou, **para sempre**, sem nenhuma parada classificada. Tirar a folha é tirar o
      **nome** desse laço infinito. **Consequência mecânica, e ela é obrigatória para o ramo
      ser exercitável de boa-fé:** o `x-custodia-passagem-id` **não é gerado inline** — vem de
      uma **costura injetável** (provider do id da passagem, ou argumento opcional
      `--passagem-id` do comando administrativo), de modo que o teste possa carimbar a
      mensagem **antes** e fixar o id da passagem. É a mesma costura que o Pronto (v) exige
      para purgar a fila entre a leitura de `N` e o consumo, e ela é do desenho, não do teste.
    - **INTERROMPIDA** = a passagem terminou **sem** examinar as `N` e **sem** que nenhuma
      mensagem da passagem corrente tenha reaparecido → **falha, nunca sucesso e nunca
      COMPLETUDE**. É a folha do "outra coisa" da subpergunta (2b), e ela tem ocupantes
      reais, os três já nomeados neste arquivo: a `custodia.parked` **purgada** durante a
      passagem — `N` foi lido antes de começar e a fila esvazia em `k < N` —, o
      cancelamento/shutdown do container no meio da passagem, e a queda de conexão com o
      broker. *Sem este rótulo, "a fila acabou antes das `N`" não tem para onde ir, e o
      executor mapeia o ramo inteiro para `LIMITE por teto` — que é falso, porque o teto não
      estourou. E "fila vazia" não pode socorrer aqui: ela está barrada como completude na
      linha seguinte, de propósito.* Ela **não publica** `custodia_parked_mensagens`, pela
      regra geral da alínea abaixo — e não por ser um caso especial.
    - **QUEM PUBLICA `custodia_parked_mensagens{motivo=}`, enunciado PELA ÁRVORE e não por
      rótulo — e é a árvore que torna a regra conferível.** A métrica é o **resultado de uma
      varredura completa**: só pode publicá-la a passagem que **examinou as `N`**. São,
      portanto, exatamente **três** folhas — **COMPLETUDE**, **PARCIAL** e
      **VAZIO_DO_MOTIVO** (as três descendentes de "as `N` foram examinadas? **sim**"), e as
      **outras três** — **LIMITE por teto**, **LIMITE por volta** e **INTERROMPIDA** — **não
      publicam**. *Note que PARCIAL e VAZIO_DO_MOTIVO publicam apesar de serem **falha** e
      **inconclusivo**: o que autoriza a publicação é ter examinado as `N`, não o veredito da
      passagem — e essa é justamente a razão de a regra ser enunciada pela árvore. Escrevê-la
      só para a folha nova (era o que este bloco fazia) deixa quatro folhas sem regra, e uma
      implementação que publique em `LIMITE por volta` — onde o número medido é
      estruturalmente menor que o real — fecha verde.* O que se perde publicando de uma
      varredura incompleta é sempre o mesmo: o número **certo** da passagem anterior é
      sobrescrito por um **menor**, e o Pronto das fases seguintes (`residual = 0`) passa a
      fechar por medição truncada.
    - "Fila vazia" **não serve** como completude: drenar seletivamente obriga a reconsumir
      e republicar o que aquela fase não trata, e essas mensagens voltam para a mesma fila.
    **O que os seis desfechos cobrem, e o que eles NÃO cobrem.** Toda parada da passagem é
    uma **folha** da árvore acima — e é assim que se confere que não sobrou parada sem
    classe: percorrendo a árvore, nunca contando rótulos. A folha que a fecha é o "outra
    coisa" de (2b), **catch-all por construção**. *O contraexemplo
    tentado, e ele sobrevive, por isso está escrito:* `residual_motivo` é medido **sobre as
    `N` da passagem**, não sobre a fila no instante em que a passagem termina — uma mensagem
    daquele motivo **estacionada durante** a passagem não está nas `N`, não é examinada, e a
    passagem pode devolver COMPLETUDE com ela na fila. Isso é **limite declarado do
    instrumento**, não desfecho novo: o drenador de um motivo roda quando a fase já trata
    aquele tipo, isto é, quando o consumidor **parou** de estacionar por ele; se estacionar
    durante a passagem, o sintoma é a passagem seguinte devolvendo PARCIAL ou
    VAZIO_DO_MOTIVO com residual, e não um sucesso permanente. O teste da fase **planta a
    mensagem antes de rodar** e não publica durante — é essa hipótese que o fixture registra.
    **Segundo limite declarado, e ele fica escrito para não ser lido como buraco:** a
    passagem que **morre antes de classificar** não devolve desfecho nenhum — o comando não
    escreve saída. Isso não é um sétimo rótulo, é **ausência de resultado**, e a regra é a
    mesma da §10.31: ausência de desfecho nunca se lê como sucesso, e quem a observa é o
    operador que rodou o comando e não recebeu classificação. O que a árvore garante é o que
    ela diz: toda passagem que **chega a classificar** cai numa das seis folhas.
  - **Corolário que vale para toda fase seguinte, e a forma dele importa mais que o
    número:** a fase que passa a tratar um tipo **drena o estacionamento daquele motivo**, e
    o Pronto dela é *"a **passagem completa** do drenador daquele motivo devolve
    **COMPLETUDE** — as `N` examinadas, `residual_motivo = 0` e **`n_motivo ≥ 1`**, pelo
    menos uma mensagem daquele motivo examinada e processada"*. **Residual `> 0` é PARCIAL e
    é falha**, ainda que `n_motivo` seja alto: a fase não fecha tendo drenado 9 de 10. **A cláusula do `n_motivo ≥ 1` é
    metade do corolário, e sem ela ele passa por vacuidade:** "as `N` foram examinadas e
    todas as que restam são de outros motivos" é **verdadeiro por vacuidade** sempre que
    `n_motivo = 0`, e a passagem devolveria COMPLETUDE com residual 0 sem ter feito nada.
    Esse estado é produzido por (i) um binding do F2 que nunca funcionou, (ii) um consumidor
    que deu ack-e-descarta em vez de estacionar, (iii) um drenador que nunca rodou e (iv)
    alguém que purgou a `custodia.parked` — a mesma família do "gauge que zera no deploy"
    que o parágrafo abaixo combate, entrando pela porta ao lado —, e **ele não depende de a
    fila estar vazia**: nos casos (ii) e (iv) é comum haver mensagens de outros motivos
    junto. Regra: **`n_motivo = 0` é VAZIO_DO_MOTIVO, INCONCLUSIVO, nunca sucesso** — a
    condição é "zero mensagens **do motivo pedido**", não "fila vazia" —, e o teste da fase
    **planta pelo menos uma mensagem daquele motivo antes de rodar** (controle positivo,
    §10.8). O peso disso é maior no F9:
    corpaction é o **único** evento estacionado que não se recupera por REST, e fechar a
    fase com a `custodia.parked` vazia por engano é perdê-la em definitivo com o checkbox
    marcado. **Não** é "o gauge está em 0", e **não** é
    "0 mensagens de `prices.#`" no broker. As três formas erradas e por quê: o broker conta
    **mensagens, não tipos** (o motivo viaja em header, invisível para ele), e contar por
    routing key só seria verificável esvaziando a fila destrutivamente; e um **gauge em
    memória zera em todo deploy** — o `== 0` passaria por vacuidade justamente depois de um
    restart, que é a família de "Pronto que passa por vacuidade" que o F1 já nomeia ao
    rejeitar `git status`. Dois contadores (`parked_total − drained_total`) têm o mesmo
    defeito, por reset de processo. Quem **sabe** o estoque por motivo é a passagem completa
    do drenador: ela lê `messages_ready` = N antes de começar, examina as N e conhece o
    header de cada uma. Então **é ela que publica o residual por motivo ao fim da passagem**,
    e a métrica `custodia_parked_mensagens{motivo=}` é o registro desse resultado — um
    número medido numa varredura, não um contador acumulado em memória.
  - **Consumo serial** (prefetch 1, um handler, uma instância). *Rejeitado:* prefetch alto
    com handlers paralelos — throughput que ninguém pediu (§11: "backpressure inexistente
    por design; volumes diários minúsculos") e que **fabrica** o fora de ordem que a decisão
    anterior teve que absorver. **Premissa que expira:** no dia em que houver duas
    instâncias compartilhando a fila (a §5 permite), a política de fora de ordem passa a ser
    exercitada de verdade, e esta é a primeira decisão a revisitar; o sintoma será estorno
    parado na DLQ sem causa aparente.
  - **`preco_medio` e `custo_total` são desta fase, com a regra de baixa escrita.**
    `posicao_corrente` tem **três** colunas de valor, e o rascunho anterior atribuía só a
    primeira: I4 falava só de `qtd_delta`, o Pronto conferia só quantidade, e ninguém
    dizia quem mantém as outras duas. Não é detalhe adiável — o F5 calcula o imposto **a
    partir** do `preco_medio` (a §7.3 escreve "base = preço médio do livro", que é **elipse**:
    a base é o **ganho**, `valorFinanceiro − pm × quantidade`; a fórmula está fechada na
    decisão da base do F5, e não se deduz desta frase) e o F7 grava `preco_medio` e `custo`
    em cada snapshot (§7.4). **A regra de dobra NÃO se escreve aqui: ela está fechada na segunda metade da
    V1 do F3, POR TIPO, PARA OS DEZ — copie-a, não a reescreva.** O que esta fase faz é
    aplicá-la na mesma transação que a quantidade: os **nove** tipos que não são `ajuste`
    aplicam-se **incrementalmente**, em O(1), **e só quando o movimento que chega é o último
    da chave na ordem da dobra** — `data_evento ≥ MAX(data_evento)` daquela `(cliente_id,
    instrumento_id)`; **movimento com `data_evento` anterior faz o handler re-dobrar a chave
    inteira**, exatamente como já faz para `ajuste`. Ao gravar um **`ajuste`** o handler
    **recalcula as três colunas daquela chave dobrando o livro** — a mesma dobra do comando
    de reconstrução, com escopo de uma chave, porque a dobra da V1 ignora os pares (linha
    revertida, `ajuste`) e porque o `ajuste` herda a `data_evento` da revertida (V1), o que o
    torna retroativo quase sempre. *A cláusula do `MAX(data_evento)` não é otimização nem
    zelo: a média ponderada é sensível à ordem, e sem ela `compra 10 @ 100` (D1), `venda 4`
    (D2) e a retroativa `compra 10 @ 200` (D0) deixam a projeção com `custo 2600` / `pm
    162,50` enquanto a dobra por data vale `custo 2400` / `pm 150` — **I4 fica falso** e a
    reconciliação do F7 alerta para sempre sobre o movimento retroativo que o próprio F7
    chama de típico.* *A versão anterior deste
    arquivo escrevia a regra em duas frases — "na compra…, na venda…" — e deixava oito dos
    dez tipos sem dobra: `aporte` inflava o preço médio para sempre e o `ajuste` de uma
    venda o corrompia em 20% no exemplo da V1, com o F5 tributando e o F7 fotografando a
    coluna errada.* Linha de `caixa:*`:
    `preco_medio = 1,000000`, `custo_total = quantidade` (V4 do F3). **Testes obrigatórios,
    e são CINCO — a lista aqui e a do prompt são a mesma lista, e acrescentar item em um só
    lugar é o defeito que esta fase já cometeu uma vez:**
    **compra-compra-venda-compra** (não só compra-venda); **aporte** (que dobra como
    compra); **compra → venda a preço diferente do custo → estorno da venda**, que é o
    único cenário que distingue a dobra certa da ingênua; **os TRÊS valores de fronteira**
    da V1 (compra que zera, compra que não zera, compra que **cruza** para positivo); e
    **o movimento retroativo**, provando que ele **re-dobra** e não aplica delta — o único
    que separa a cláusula do `MAX(data_evento)` da sua ausência. *Rejeitado:* deixar as
    colunas NULL "até alguém precisar" — a metade permissiva de I4 fica verde com
    `preco_medio` NULL para sempre, e quem descobre é o F5, dentro do cálculo de imposto.
  - **Decisão B, primeira metade:** comando administrativo dentro do container para
    reconstruir `posicao_corrente`, **nas três colunas**. Sem endpoint, sem SQL manual
    documentado no README (comentário não é executável).
  - **Camada 3 da §6.1 / ADR-11:** resgate maior que a posição, venda sem compra — o
    movimento **entra no livro do mesmo jeito** e a inconsistência é **sinalizada** por
    métrica + log estruturado + alerta. Rejeitar aqui reintroduziria na Custódia a validação
    que a ADR-10 tirou de Operações, e para registro manual posição negativa costuma
    significar "falta lançar a compra antiga". *Ausência decidida:* a sinalização **não é
    evento novo** — publicar tornaria a Custódia produtora, exigindo exchange, routing key
    e contrato que a §5.1 não define, mais outbox e relay que a ADR-10 dispensa. Se um dia
    virar evento, é fase própria, com o contrato entrando na §5.1 **antes** do código —
    como o `estornaTradeId` entrou no F3 do `operacoes`.
  - **Toda condição de parada rotulada** COMPLETUDE ou LIMITE — **mais o VAZIO_DO_MOTIVO, o
    PARCIAL e o INTERROMPIDA no drenador, que são os SEIS da árvore de decisão acima, e esta
    lista é a mesma do prompt**: os três foram descobertos olhando o procedimento pergunta a
    pergunta, e não escrevendo "exaustivo" ao lado da lista — o PARCIAL faltava porque o
    procedimento não perguntava pelo residual, e o INTERROMPIDA faltava porque ele não
    perguntava **por que** as `N` não foram examinadas —, e limite devolve
    **falha**, nunca sucesso com conjunto parcial (§10.31 — achada três vezes por três
    portas no F5 do `operacoes`, sempre com a suíte verde).

  **NÃO ENTRA:** IR/IOF, `a_liquidar`, `liquidacao` (F5); `prices.*` e `preco_atual` (F6);
  snapshot e worker (F7); extrato (F8); corpaction (F9); e nenhum endpoint, nem
  administrativo, nem "só para reconstruir". **E fica escrito para não ser reportado como
  defeito:** até o F7 existir, movimento com `data_evento` no passado atualiza a posição e
  **não tem o que recalcular**, porque não há snapshot. Isso está correto, é assim de
  propósito, e não se "conserta" aqui — o handler apenas registra e emite métrica.

  **Exceção declarada a I13, e por que ela é aceitável:** o F4 grava o fato do resgate
  **antes** de o F5 saber derivar `ir_retido`/`iof`/`a_liquidar`/`liquidacao` — e grava o
  `ajuste` de um estorno **antes** de o F5 saber reverter esses derivados. A pergunta
  da §10.21 — "o que for gravado errado até a próxima fase tem conserto?" — responde **sim**
  aqui, e só por isso: as linhas faltantes se **apendam** depois, sem tocar nenhuma linha
  existente e sem UPDATE, porque a convenção de `ref_externa` fechada no F3 (V3, incluindo
  a família `est:`) torna o backfill do F5 idempotente por construção. Se essa convenção não
  existisse, a resposta seria não e o recorte seria dívida sem prazo. **E o erro que a janela
produz é conservador e visível:** com `a_liquidar` bruto (V2 do F3), um resgate gravado sem
os derivados tira o título da posição e ainda **não** põe o direito a receber, então o
patrimônio do dia fica **menor** que o real — nunca maior, nunca "plausível e certo". O F5 fecha a janela com
  **guarda permanente**, não com script de uma vez, e **as duas metades da janela** — resgate
  sem derivados **e** estorno sem reversão dos derivados.

  **Configuração: as CINCO listas mudam juntas**, mais o `.env.example`.
  `RabbitMq__{Host,User,Password}` entram (1) no `docker-compose.yml` e no
  `docker-compose.prod.yml` (com `:?` em produção, sem default), (2) no `envs:` do
  ssh-action, (3) **no bloco `env:` do mesmo step**, mapeando `${{ secrets.* }}` — sem ele
  a variável é encaminhada inexistente e a guarda `-z` acusa "secret vazio" com o secret
  cadastrado —, (4) no `printf` do `.env` do deploy (que é reescrito **por completo** —
  variável obrigatória fora do mesmo `printf` derruba o serviço no `up` seguinte) e (5) nas
  dummies do config gate (nem a mais nem a menos). São o **mesmo secret** que o F2 já
  cadastrou nas listas (2) e (3): normalize uma vez na origem com `tr -d '\r\n'` (§10.4) e
  reuse. Descomente e reescreva o bloco do `.env.example`. **Avise no PR que é mudança
  quebrante** para todo mundo que já tem `.env` local, citando as linhas novas, e rode
  `docker compose config -q` contra o `.env` **real**.

  **Os alertas desta fase (profundidade e idade da fila, da DLQ, do retry e do parking, e
  a contagem por motivo) só existem quando chegam à nuvem** — procedimento no
  `LEIA-ME-KIT.md`, seção "No repo do `tesouro-direto`".
  **Recurso:** medir com `docker stats` **durante o dreno do backlog**, não em regime — o
  teto de 192m foi medido para uma API pequena sem consumidor.

  **Âncoras:** `ARQUITETURA` §7.3 (handler de TradeRegistered), §5 (ack manual, redelivery
  esperada e inócua, work-sharing), §5.1 (contrato, `estornaTradeId`), §6.1 camada 3,
  ADR-3, ADR-10, ADR-11; `PADROES` §10.26 (dedupe dá idempotência, **não**
  comutatividade — o parágrafo "Não tente pular o veneno" descreve exatamente este
  cenário), §10.31, §10.32, §10.21, §10.24, §10.25, §10.13; `CLAUDE.md`, as três armadilhas
  da seção "O que a Custódia é"; `LEIA-ME-KIT` "Especificar só a metade permissiva",
  "Teste manual em tabela append-only", "Rodar o revisor contra entrega não commitada".

  **Prompt:**
  ```
  Implemente o consumidor da fila custodia.prices e o handler de TradeRegistered
  (ARQUITETURA 7.3, Fluxo 2 da 8.3).

  ATENCAO: NAO HA MOLDE DE CONSUMIDOR na plataforma — hub e operacoes PUBLICAM. Leia
  ../hub-precos/src/Hub.Infrastructure/Messaging/RabbitMqEventPublisher.cs e
  ../operacoes/src/Operacoes.Infrastructure/Messaging/RabbitMqEventPublisher.cs para
  conexao, configuracao, serializacao e conversao de excecao em Result; o laco de
  consumo, o ack manual e a politica de mensagem-veneno sao codigo NOVO.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  O QUE ENTRA:
  - BackgroundService com basic.consume, ACK MANUAL apos persistir o efeito,
    BasicQos(prefetchCount: 1), processamento serial, UMA instancia.
  - Redeclaracao da topologia no boot com os MESMOS argumentos do F2 (divergencia
    devolve 406 PRECONDITION_FAILED, que e falha alta e desejavel). E a SEGUNDA linha de
    defesa; a primeira e o passo de deploy do F2.
  - Handler de TradeRegistered: UMA linha em movimentos com o fato, traduzida pelo
    MAPEAMENTO V2 FECHADO NO F3 (copie literalmente, nao reescreva):
      aplicacao -> compra   instrumento do evento, qtd_delta = +quantidade,
                            valor = valorFinanceiro (bruto)
      resgate   -> venda    instrumento do evento, qtd_delta = -quantidade,
                            valor = valorFinanceiro (bruto)
      aporte    -> aporte   INSTRUMENTO DO EVENTO, qtd_delta = +quantidade,
                            valor = valorFinanceiro (bruto). NAO e caixa:BRL e NAO e
                            +valorFinanceiro: gravar assim DESCARTARIA o instrumentoId e
                            a quantidade numa tabela sem UPDATE. A diferenca contra
                            `aplicacao` e SO O ROTULO no extrato.
      estorno   -> ajuste   instrumento do movimento estornado (LOOKUP de
                            ref_externa = estornaTradeId DO MESMO CLIENTE),
                            qtd_delta e valor SIMETRICOS ao estornado (o par soma zero
                            nas duas colunas)
    ARMADILHA: `resgate` do EVENTO e uma VENDA no livro. `resgate` no enum do livro e
    VENCIMENTO, e vencimento e do F9. Nao use a mesma palavra para as duas coisas.
    ref_externa = <tradeId> (a convencao V3 do F3). E posicao_corrente atualizada NA
    MESMA TRANSACAO, NAS TRES COLUNAS.
  - preco_medio e custo_total. A REGRA DE DOBRA NAO SE ESCREVE AQUI: ela esta fechada na
    SEGUNDA METADE DA V1 DO F3, POR TIPO, PARA OS DEZ — copie a tabela de la, nao a
    reescreva. NAO IMPLEMENTE "switch (tipo) { case compra: ...; case venda: ...; }": e a
    leitura literal do texto ANTIGO deste roadmap, deixa OITO tipos sem dobra, INFLA o
    preco medio para sempre no `aporte` (que dobra IGUAL a compra) e CORROMPE o preco
    medio no `ajuste` de uma venda.
    O QUE ESTA FASE FAZ e APLICAR aquela dobra na mesma transacao que a quantidade:
      - os NOVE tipos que nao sao `ajuste` aplicam-se INCREMENTALMENTE, em O(1), MAS SO
        QUANDO O MOVIMENTO QUE CHEGA E O ULTIMO DA CHAVE NA ORDEM DA DOBRA:
        data_evento >= MAX(data_evento) daquela (cliente_id, instrumento_id). Se a
        data_evento for ANTERIOR, RE-DOBRE A CHAVE INTEIRA, igual ao ajuste.
        NA PRIMEIRA LINHA DA CHAVE O MAX E NULO: trate esse caso EXPLICITAMENTE — MAX nulo
        => APLICA O DELTA INCREMENTAL, porque nao ha historia anterior e a linha que chega
        E a ultima da ordem por definicao. Em SQL `data_evento >= NULL` e NULL (o WHERE
        trata como falso); em C# com DateOnly? isso vira NullReferenceException ou `false`,
        e `false` manda a primeira linha de TODA chave nova para a re-dobra — correto por
        acidente, com O(1) virando O(n) sem ninguem notar. A media
        ponderada E SENSIVEL A ORDEM: compra 10@100 (D1), venda 4 (D2) e a retroativa
        compra 10@200 (D0) dao, por delta, custo 2600 e pm 162,50, quando a dobra por data
        vale custo 2400 e pm 150 — I4 fica FALSO e a reconciliacao do F7 alerta para
        sempre sobre o movimento retroativo que o F7 chama de tipico;
      - ao gravar um `ajuste`, RECALCULE as tres colunas daquela chave (cliente_id,
        instrumento_id) DOBRANDO O LIVRO — a mesma dobra do comando de reconstrucao, com
        escopo de uma chave —, porque a dobra da V1 IGNORA OS PARES (linha revertida,
        ajuste que a reverte), e ignorar uma linha no meio da historia muda as medias
        seguintes: nao da para aplicar como delta. E o ajuste HERDA a data_evento da
        revertida (V1), entao ele quase sempre cai tambem na regra do MAX acima.
    Linha de caixa:*: preco_medio = 1,000000 e custo_total = quantidade.
    COPIE TAMBEM A REGRA DE FRONTEIRA DA V1, que e onde a tabela divide: preco_medio so e
    DEFINIDO para quantidade > 0; quantidade resultante = 0 (qualquer tipo) -> preco_medio 0
    e custo_total 0; quantidade resultante < 0 -> preco_medio INALTERADO, nunca recalculado,
    nunca negativo; quantidade ANTERIOR <= 0 e RESULTANTE > 0 (o CRUZAMENTO) -> preco_medio
    = o preco unitario da propria linha que cruza (valor/qtd_delta) e custo_total =
    preco_medio x quantidade resultante. E O CORTE: aqui voce aplica a dobra com
    D = INFINITO (posicao_corrente); o corte por data e do F7.
    TESTES OBRIGATORIOS, e sao CINCO — esta lista e a da prosa desta fase sao A MESMA
    LISTA: (1) COMPRA-COMPRA-VENDA-COMPRA, nao so compra-venda — e a PRIMEIRA compra desse
    cenario e tambem o caso do MAX(data_evento) NULO, que a assercao nomeia: ela tem que
    aplicar delta incremental, nao re-dobrar;
    (2) APORTE, provando que ele dobra igual a compra (o teste de preco medio anterior
    nao o incluia, e o Pronto (d2) so conferia instrumento e quantidade); (3) COMPRA ->
    VENDA A PRECO DIFERENTE DO CUSTO -> ESTORNO DA VENDA, exigindo que AS TRES COLUNAS
    voltem ao estado anterior a venda (compra 10 @ 100, venda 4 por 600, estorno: tem que
    voltar a qtd 10, custo 1000, pm 100 — a regra ingenua devolve custo 1200 e pm 120).
    E o unico cenario que distingue a dobra certa da ingenua, e nenhum outro teste desta
    fase o cobre; (4) OS TRES VALORES DE FRONTEIRA (Pronto d3), todos a partir da mesma
    venda sem compra de 10: compra 10 @ 100 = quantidade 0, pm 0, custo 0, SEM DIVISAO POR
    ZERO; compra 4 @ 100 = quantidade -6, custo 400, PM INALTERADO (jamais -66,67); e
    COMPRA QUE CRUZA, 20 @ 100 = quantidade 10, pm 100, custo 1000 (JAMAIS pm 200) — com
    varredura afirmando preco_medio >= 0 na tabela inteira. O invariante custo = pm x qtd
    NAO pega o segundo (-66,67 x -6 = 400 e verdadeiro) e NEM O TERCEIRO (200 x 10 = 2000
    tambem e), e a varredura de sinal aprova o terceiro tambem: e por isso que ele e teste
    nomeado; (5) MOVIMENTO RETROATIVO, provando que ele RE-DOBRA a chave e nao aplica
    delta — e o unico que separa a clausula do MAX(data_evento) da ausencia dela.
    Sem isso o F5 vai calcular imposto contra uma coluna vazia ou errada e inventar a regra
    dentro do calculo, e o F7 vai gravar preco_medio errado em cada snapshot — documento que
    o cliente ve (P2). ("Base = preco medio do livro", 7.3, e ELIPSE: o preco medio e o
    INSUMO da base, e a base e o GANHO — a formula esta fechada no F5, nao a deduza daqui.)
  - Validacao de magnitude E ESCALA dos numeric no Dominio (PADROES 10.25).
  - Comando administrativo dentro do container para reconstruir posicao_corrente a
    partir do livro, aplicando A MESMA DOBRA DA V1 DO F3 (por tipo, para os dez,
    ignorando os pares revertidos, COM CORTE D = INFINITO — que e o que "sem filtro por
    relogio" do I4 quer dizer), NAS TRES COLUNAS — reconstruir uma e deixar as outras
    duas e divergencia plantada pela propria ferramenta de conserto. SEM ENDPOINT — a
    fronteira da ADR-10 e ARQUITETURAL, nao de autenticacao.
  - Metricas de consumo e alerta de profundidade/idade da fila, da DLQ, do RETRY e do
    parking, mais custodia_parked_mensagens{motivo=}.

  DECISOES JA TOMADAS:

  A) Estorno orfao (o trade estornado ainda nao chegou): NAO grava nada e NAO CONSOME A
     CHAVE DE DEDUPE. E REPUBLICADO PELO EXCHANGE custodia.retry.in, que entrega na fila
     custodia.retry (com x-message-ttl e DLX de
     volta para custodia.prices, declarada no F2), e a mensagem original e CONFIRMADA,
     liberando a cabeca da fila; passado o TTL o broker a devolve sozinho.
     `nack(requeue: true)` ESTA PROIBIDO AQUI, e o motivo tem nome: HEAD-OF-LINE
     BLOCKING. Com UMA fila, UM consumidor, prefetch 1 e consumo serial, a mensagem
     devolvida volta para a FRENTE e e reentregue na hora, e o trade original que
     curaria a condicao esta ATRAS dela, na MESMA fila, e nunca e entregue — a condicao
     nao "se cura em segundos", ela nao se cura. Nao ha atraso em basic.nack, e um sleep
     antes do nack travaria TODO o consumo (laco quente com prefetch 1 num host de UM
     nucleo, PADROES 10.13); e sem atraso o x-delivery-limit queima em milissegundos e a
     mensagem cai na DLQ, que e o "DLQ na primeira tentativa" que esta rejeitado.
     Retry e nao DLQ direto porque fora de ordem AQUI e artefato de redelivery e nao de
     causalidade: em Operacoes a FK composta impede registrar estorno antes do original
     e o relay publica em ordem de id aguardando cada confirm um a um (PADROES 10.26) —
     com a cabeca da fila liberada, o original e processado no proprio ciclo.
     TETO: conte as voltas pelo header x-death que o PROPRIO BROKER escreve ao
     dead-letrar (PADROES 10.32 — a distincao vem marcada, nao re-derivada). Estourado o
     teto, a mensagem vai para custodia.parked com motivo `estorno_orfao_expirado` e
     ALERTA, nunca para a DLQ: estacionar visivelmente e o modo de falha certo em
     at-least-once (PADROES 10.26), e a DLQ deste roadmap nao tem historia de dreno.
     TRES DETALHES SEM OS QUAIS ISTO NAO FUNCIONA, e nenhum e dedutivel do paragrafo:
     (1) AO REPUBLICAR NA custodia.retry.in, COPIE INTEGRALMENTE OS HEADERS DA MENSAGEM
         RECEBIDA. O x-death e escrito pelo broker ao dead-letrar, mas o que voce faz e
         um PUBLISH NOVO, e publish novo so carrega o que o publicador setar: sem a
         copia o contador ZERA A CADA VOLTA, o teto de 10 nunca fecha, a mensagem circula
         entre custodia.prices e custodia.retry a cada 30 s para sempre, e
         estorno_orfao_expirado NUNCA E EMITIDO — laco infinito com a suite verde.
     (2) ORDEM OBRIGATORIA: PUBLICA -> ESPERA O PUBLISHER CONFIRM -> SO ENTAO ACK.
         trades.registered NAO tem caminho de recuperacao por contrato: ack antes do
         confirm e PERDA DEFINITIVA E SILENCIOSA, o modo de falha exato que o F2 existe
         para impedir. Confirm negado ou timeout => nack(requeue: true) da ORIGINAL —
         aqui o requeue e o certo, porque a alternativa e perder.
         E O CONFIRM NEGADO PERSISTENTE TEM RAMO PROPRIO, SENAO ESTA REGRA PRODUZ OS DOIS
         DESFECHOS QUE A DECISAO A REJEITA. Com prefetch 1 e consumo serial, o
         nack(requeue: true) reentrega NA HORA; se a causa nao for transitoria — e o caso
         previsto e a custodia.retry ter nascido com x-overflow: reject-publish no F2 e
         estar CHEIA, cujo reject E um confirm negado — o resultado e
         nack -> reentrega -> publish -> reject -> nack SEM ATRASO NENHUM: LACO QUENTE
         com prefetch 1 num host de UM nucleo (PADROES 10.13), e o x-delivery-limit
         queima em milissegundos levando a mensagem para a DLQ, que e o "DLQ na primeira
         tentativa" REJEITADO. Regra: TETO DE TENTATIVAS DE REPUBLISH para a mesma
         entrega; estourado, a mensagem vai para a custodia.parked PELO custodia.parking,
         com motivo `retry_indisponivel` e ALERTA. Preserva a mensagem, sem laco quente e
         sem DLQ.
     (3) O TETO E O QUE ENTREGA VALOR, NAO A CURA: orfao por corrida e raro (FK composta
         em Operacoes, relay em ordem de id, um confirm por vez). O que vai acontecer de
         verdade sao os casos INCURAVEIS (original perdido na janela sem binding, ou
         morto na DLQ), e para esses o retry so adia. Se um dia for preciso cortar
         escopo, reduza as VOLTAS (2 ou 3 cobrem a corrida real); NUNCA remova o parking.

  B) O SINAL do ajuste (somar ou subtrair) depende do tipo da operacao estornada, e o
     payload da 5.1 NAO carrega esse tipo. Por isso o lookup e insubstituivel. NAO
     "simplifique" gravando o estorno so com a referencia externa: isso exigiria campo
     novo na 5.1 e mudanca em Operacoes ANTES deste codigo, e a 5.1 ja registra que a
     maquina do lookup existe.
     O LOOKUP E EM DOIS PASSOS, e sem isso dois desfechos que o Pronto exige "nomeados e
     distinguiveis" ficam INDISTINGUIVEIS (os dois devolvem zero linhas):
       1. ref_externa = estornaTradeId NO MESMO CLIENTE -> achou: grava o ajuste;
       2. nao achou -> busca em QUALQUER cliente. Achou: `estorno_cliente_divergente`,
          PARKING IMEDIATO E ALERTA, SEM passar pelo retry — esperar nunca cura um
          estorno que aponta para outro cliente, e mandado ao retry ele gastaria 10
          voltas de 30 s para estacionar com o motivo ERRADO
          (estorno_orfao_expirado);
       3. nao achou em ninguem -> e orfao, e vale a decisao A.
     E O PASSO (1) NAO TERMINA AO ACHAR: os TRES campos proprios do payload de estorno
     (instrumentoId, quantidade, valorFinanceiro — OBRIGATORIOS no envelope da 5.1, nao
     opcionais) sao CONFERIDOS contra o movimento encontrado: instrumentoId = o
     instrumento_id dele, quantidade = |qtd_delta| dele, valorFinanceiro =
     valor_financeiro dele. E a DISPENSA DECLARADA da V2 do F3 ("conferidos e nao
     usados"). Divergindo QUALQUER UM: NADA e gravado, a chave de dedupe NAO e consumida
     (I13), e a mensagem estaciona como `estorno_divergente` com ALERTA. Sem esse passo,
     um estorno parcial (ou um erro em Operacoes) grava a reversao TOTAL em silencio,
     numa tabela sem UPDATE, e nenhum motivo nomeado o distingue de um estorno correto.
     E A VIOLACAO DO V5 TEM DESFECHO PROPRIO: um segundo estorno do MESMO movimento chega
     com tradeId proprio, passa pelo UNIQUE (cliente_id, ref_externa) — e esse justamente
     o argumento do V5 — e estoura no UNIQUE (ref_estorno) EM TODA REENTREGA. Sem
     tratamento nomeado isso vira MENSAGEM-VENENO: queima o x-delivery-limit e cai na
     DLQ, que este roadmap declara sem historia de dreno. Violacao daquele indice ->
     motivo `estorno_duplicado`, parking + alerta.

  C) Evento de tipo que esta fase ainda nao sabe escriturar (prices.*, corpactions.*,
     eod.ready — todos bindados desde o F2) e REPUBLICADO PELO EXCHANGE custodia.parking,
     que entrega na fila custodia.parked, e SO ENTAO
     confirmado. Publique sempre em EXCHANGE, nunca direto numa fila: a default exchange
     com routing key = nome da fila foi REJEITADA NOMINALMENTE no F2.
     Nunca ack-e-descarta, nunca nack em laco (com x-delivery-limit, requeue
     por semanas dead-letra a mensagem, e ate la e laco quente com prefetch 1 num host de
     UM nucleo — PADROES 10.13).
     O MOTIVO viaja num cabecalho OBRIGATORIO, `x-custodia-motivo`, SEM DEFAULT, escrito
     por quem estaciona, no ponto onde a informacao e conhecida (PADROES 10.32: default
     faz um produtor futuro esquecer de marcar, em silencio). Valores desta fase:
     tipo_nao_tratado_prices, tipo_nao_tratado_corpactions, tipo_nao_tratado_eod,
     estorno_orfao_expirado, estorno_cliente_divergente, estorno_duplicado,
     estorno_divergente, retry_indisponivel, versao_nao_suportada, payload_invalido.
     SAO DEZ NESTA FASE. A LISTA E FECHADA E SEM DEFAULT; duas fases seguintes acrescentam
     UM VALOR CADA, os dois ja nomeados aqui: o F7 acrescenta intervalo_acima_do_teto (o
     handler de eod.ready recusando intervalo de materializacao acima do teto, em vez de
     entrar em laco de reentrega que nunca fecha) e o F9 acrescenta acao_desconhecida. COM
     OS DOIS, SAO DOZE NO ROADMAP INTEIRO.
     Os dois ultimos a entrar, com a decisao que os criou: `estorno_divergente` e a
     dispensa declarada do estorno na V2 do F3 (os tres campos proprios do payload sao
     CONFERIDOS contra o movimento original); `retry_indisponivel` e o ramo de CONFIRM
     NEGADO PERSISTENTE da decisao A, sem o qual o republish numa custodia.retry cheia
     vira laco quente + DLQ. NAO deixe o drenador decidir por ROUTING
     KEY: e re-derivacao do que quem estacionou ja sabia, e apodrece no primeiro caso
     novo — dois motivos diferentes chegam pela MESMA routing key (um prices.td parado
     por tipo nao tratado e um prices.td parado por payload invalido nao se drenam
     juntos).
     A DRENAGEM E UM LACO DE CONSUMO E PRECISA DA CLASSIFICACAO DA PADROES 10.31.
     Mecanica, decidida aqui porque toda fase seguinte a usa: comando administrativo
     dentro do container, recebe UM motivo como argumento, le messages_ready da
     custodia.parked ANTES de comecar, consome NO MAXIMO esse tanto, processa as do
     motivo pedido e REPUBLICA AS DEMAIS no fim da propria fila pelo custodia.parking,
     preservando o cabecalho x-custodia-motivo e CARIMBANDO x-custodia-passagem-id com o
     id da passagem corrente (sobrescrevendo o de passagens anteriores).
     TRES NOMES, UM POR COISA — nao chame as contagens de N, que foi como a versao
     anterior deste prompt divergiu da prosa:
       N               = ESTOQUE lido antes de comecar (messages_ready), e tambem o TETO
                         do laco.
       n_motivo        = mensagens DO MOTIVO PEDIDO examinadas E PROCESSADAS nesta passagem.
       residual_motivo = mensagens DO MOTIVO PEDIDO examinadas e REPUBLICADAS SEM PROCESSAR
                         (falha de handler, payload que volta a estacionar).
     O PROCEDIMENTO DE DECISAO E UMA ARVORE DE QUATRO PERGUNTAS, NESTA ORDEM, E CADA FOLHA
     DELA E UM DESFECHO — SAO SEIS:
       (1) N esta acima do teto configurado?
       (2) a passagem examinou as N?
       (2b) se NAO: por que — uma mensagem DESTA passagem deu a volta, ou OUTRA COISA?
       (3) quanto vale residual_motivo?
       (4) quanto vale n_motivo?
     DUAS versoes anteriores deste prompt erraram aqui, e o erro foi o mesmo — perguntar
     menos do que os rotulos exigem e depois declarar a lista exaustiva. A primeira dizia
     "QUATRO DESFECHOS, EXAUSTIVOS" e perguntava so "as N foram examinadas?" e "quanto vale
     n_motivo?": nao olhava o residual, que a propria definicao de COMPLETUDE exige, e o
     estado "N examinadas + n_motivo >= 1 + residual > 0" ficava SEM CLASSE — o sucesso com
     conjunto parcial da 10.31. A segunda acrescentou o PARCIAL e deixou o ramo VIZINHO:
     "as N foram examinadas? NAO" nao selecionava rotulo nenhum, porque os dois LIMITE eram
     definidos por criterios que nao estavam entre as perguntas — e existe estado real que
     responde NAO e nao e nenhum dos dois (custodia.parked PURGADA durante a passagem,
     shutdown no meio, queda de conexao com o broker). Por isso a pergunta virou duas e ha
     um SEXTO rotulo.
       COMPLETUDE       = as N foram examinadas, residual_motivo = 0 E n_motivo >= 1 ->
                          sucesso, E A PASSAGEM PUBLICA O RESIDUAL POR MOTIVO.
       PARCIAL          = as N foram examinadas e residual_motivo > 0 -> FALHA, para
                          QUALQUER valor de n_motivo. E a mensagem daquele motivo que o
                          handler examinou e NAO conseguiu processar; cobre tambem o
                          extremo em que TODAS falharam (n_motivo = 0 com residual > 0),
                          que NAO e vazio do motivo. Vem ANTES da pergunta sobre n_motivo
                          de proposito: processar 9 de 10 e conjunto parcial.
       VAZIO_DO_MOTIVO  = as N foram examinadas, residual_motivo = 0 e n_motivo = 0, PARA
                          QUALQUER N, INCLUSIVE N = 0 -> INCONCLUSIVO, NUNCA SUCESSO. "As N foram examinadas e
                          todas as que restam sao de outros motivos" e VERDADEIRO POR
                          VACUIDADE sempre que n_motivo = 0, e a passagem devolveria
                          COMPLETUDE e residual 0 sem ter feito nada — o mesmo defeito do
                          gauge que zera no deploy, entrando pela porta ao lado. FILA COM
                          MENSAGENS DE OUTROS MOTIVOS E ZERO DO MOTIVO PEDIDO E ESTE
                          DESFECHO, e e o estado normal de qualquer SEGUNDA execucao: NAO
                          ancore a guarda em N = 0, porque UMA mensagem de outro motivo ja
                          faz N != 0 e desliga a guarda justamente nos cenarios que ela
                          nomeia (binding do F2 que nunca funcionou, consumidor que deu
                          ack-e-descarta — corpactions descartado com prices estacionado —,
                          drenador que nunca rodou, custodia.parked purgada). O TESTE
                          PLANTA PELO MENOS UMA MENSAGEM DO MOTIVO ANTES DE RODAR (controle
                          positivo, PADROES 10.8).
       LIMITE POR TETO  = N acima do teto configurado -> FALHA, nunca sucesso com conjunto
                          parcial. E A PERGUNTA (1), respondida ANTES de a passagem
                          comecar, com o N que ela acabou de ler — por isso ela nao
                          compete com a (2): quando o teto estoura, NENHUMA mensagem
                          chegou a ser examinada.
       LIMITE POR VOLTA = uma mensagem que JA FOI EXAMINADA NESTA PASSAGEM reaparecendo
                          antes de as N terem sido examinadas (detectavel porque ela ja traz
                          o x-custodia-passagem-id CORRENTE) -> FALHA.
                          A formulacao anterior — "uma mensagem que volta a aparecer SEM TER
                          SIDO EXAMINADA" — era autocontraditoria (voltar a aparecer implica
                          ter sido consumida e republicada, isto e, examinada) e, sem
                          identidade preservada no republish, INIMPLEMENTAVEL: sobrava so "N
                          acima do teto".
                          ESTA FOLHA E INALCANCAVEL SE VOCE IMPLEMENTAR O TETO CERTO, E ISSO
                          E DE PROPOSITO. A custodia.parked e FIFO e nao tem outro consumidor:
                          as N originais ocupam as N primeiras posicoes, tudo que voce
                          republica entra ATRAS delas, e consumindo NO MAXIMO N voce nunca
                          alcanca a posicao N+1. (Mensagem estacionada DURANTE a passagem
                          tambem entra atras; messages_ready so pode ler A MENOS; passagem
                          concorrente carimba OUTRO id.) A FOLHA EXISTE COMO GUARDA DE
                          DEFESA EM PROFUNDIDADE contra a implementacao que NAO respeita o
                          teto — "consuma ate a fila esvaziar" e a leitura mais natural de
                          "drenar", e sob ela voce republica e reconsome o que voce mesmo
                          republicou PARA SEMPRE, sem parada classificada nenhuma. Tirar a
                          folha e tirar o NOME desse laco infinito.
                          POR ISSO O x-custodia-passagem-id VEM DE UMA COSTURA INJETAVEL
                          (provider do id da passagem, ou argumento opcional --passagem-id do
                          comando), NAO gerado inline: e o que permite o teste carimbar a
                          mensagem ANTES e fixar o id da passagem, sem mutar a implementacao.
                          Mesma costura que o Pronto (v) usa para purgar a fila entre a
                          leitura de N e o consumo. A COSTURA E DO DESENHO, NAO DO TESTE.
       INTERROMPIDA     = a passagem terminou SEM examinar as N e SEM que nenhuma mensagem
                          da passagem corrente tenha reaparecido -> FALHA, NUNCA SUCESSO E
                          NUNCA COMPLETUDE. E a folha do "OUTRA COISA" da pergunta (2b), e
                          ela tem ocupantes REAIS: custodia.parked PURGADA durante a
                          passagem (N foi lido antes de comecar e a fila esvazia em k < N),
                          cancelamento/shutdown do container no meio, queda de conexao com
                          o broker. SEM ESTE ROTULO voce mapeia esse ramo inteiro para
                          LIMITE POR TETO, que e FALSO (o teto nao estourou), e "fila
                          vazia" nao socorre: ela esta barrada como completude logo abaixo.
                          Ela NAO PUBLICA custodia_parked_mensagens, pela regra geral logo
                          abaixo — e nao por ser caso especial.
     QUEM PUBLICA custodia_parked_mensagens{motivo=} — A REGRA E PELA ARVORE, NAO POR
     ROTULO: a metrica e RESULTADO DE UMA VARREDURA COMPLETA, entao publicam EXATAMENTE as
     TRES folhas em que as N FORAM EXAMINADAS — COMPLETUDE, PARCIAL e VAZIO_DO_MOTIVO — e
     NAO publicam as outras tres: LIMITE POR TETO (nao examinou nenhuma), LIMITE POR VOLTA e
     INTERROMPIDA (examinaram MENOS que as N). PARCIAL e VAZIO_DO_MOTIVO publicam APESAR de
     serem falha e inconclusivo: o que autoriza a publicacao e TER EXAMINADO AS N, nao o
     veredito. NAO ESCREVA A REGRA SO PARA A INTERROMPIDA: assim quatro folhas ficam sem
     regra e uma implementacao que publique em LIMITE POR VOLTA fecha verde com um numero
     estruturalmente MENOR que o real, sobrescrevendo o numero certo da passagem anterior — e
     e desse numero que sai o "residual = 0" do Pronto das fases seguintes.
     "Fila vazia" NAO SERVE como completude: drenar seletivamente obriga a reconsumir e
     republicar o que esta fase nao trata, e essas mensagens voltam para a mesma fila.
     LIMITE DECLARADO DO INSTRUMENTO, e ele NAO e um desfecho novo: residual_motivo e
     medido SOBRE AS N DA PASSAGEM, nao sobre a fila no instante do fim — uma mensagem
     daquele motivo estacionada DURANTE a passagem nao esta nas N e nao e examinada. O
     drenador de um motivo roda quando a fase ja trata aquele tipo (o consumidor parou de
     estacionar por ele); o teste PLANTA a mensagem ANTES de rodar e nao publica durante.
     A CONTAGEM POR MOTIVO SAI DESSA PASSAGEM, NAO DE UM GAUGE EM MEMORIA. O consumidor
     so sabe o que ele mesmo estacionou; um gauge zera em todo deploy, e o "== 0" do
     Pronto das fases seguintes passaria POR VACUIDADE justamente depois de um restart
     (dois contadores, parked_total - drained_total, tem o mesmo defeito). Quem conhece o
     ESTOQUE por motivo e a passagem completa: ela le messages_ready = N antes de
     comecar, examina as N e le o header de cada uma. Entao exporte
     custodia_parked_mensagens{motivo=} COMO RESULTADO DA PASSAGEM, e o Pronto das fases
     seguintes e "a passagem completa do drenador do motivo X devolve COMPLETUDE e
     residual 0, COM n_motivo >= 1" — nao "o gauge esta em 0" e nao inspecao de routing key
     no broker (o broker conta MENSAGENS, nao tipos). A clausula do n_motivo >= 1 e METADE
     DO COROLARIO: sem ela o Pronto passa por vacuidade SEMPRE QUE n_motivo = 0 (com a fila
     vazia ou com ela cheia de outros motivos), e no F9 isso significa perder corpaction em
     definitivo com o checkbox marcado (sao os unicos eventos estacionados que nao voltam
     por REST).

  D) Camada 3 da 6.1 / ADR-11: resgate maior que a posicao e venda sem compra ENTRAM no
     livro e sao SINALIZADOS por metrica + log estruturado + alerta. NAO rejeite:
     rejeitar reintroduziria na Custodia a validacao que a ADR-10 tirou de Operacoes. E
     NAO publique evento: a Custodia nao e produtora (sem exchange, sem routing key na
     5.1, sem outbox).

  E) Toda condicao de parada de laco ROTULADA, e limite devolve FALHA, nunca sucesso com
     conjunto parcial (PADROES 10.31). Os rotulos sao COMPLETUDE e LIMITE em todo laco
     desta fase, MAIS o VAZIO_DO_MOTIVO, o PARCIAL e o INTERROMPIDA no drenador — SAO OS
     SEIS DA ARVORE DE DECISAO ACIMA, e esta lista e a MESMA da prosa desta fase:
     acrescentar rotulo em um lugar so e o defeito que esta fase ja cometeu duas vezes.
     Parada sem classe e o defeito, e "nao me encaixei em nenhum dos dois" nao pode virar
     sucesso por omissao.

  NAO ENTRA: ir_retido, iof, a_liquidar, liquidacao (F5); prices.* (F6); snapshot e
  worker (F7); extrato (F8); corpaction (F9); nenhum endpoint. E ESCREVA NO CODIGO POR
  ESTRUTURA (nao por comentario, via nome de teste): ate o F7 existir, movimento com
  data_evento no passado atualiza a posicao e NAO TEM O QUE RECALCULAR, porque nao ha
  snapshot. Isso esta correto e nao se conserta aqui.

  PROVA — o invariante central NAS DUAS DIRECOES, e a estrita e a que pega regressao
  (no operacoes uma validacao degenerou para "chegou alguma coisa, entao aceito" com 484
  testes verdes):
    PERMISSIVA: todo TradeRegistered entregue aparece no livro exatamente uma vez,
      inclusive sob reentrega e inclusive drenando um backlog.
    ESTRITA: NENHUMA linha nasce de payload com operacao desconhecida, campo
      obrigatorio ausente, estorno sem estornaTradeId, estornaTradeId DE OUTRO CLIENTE
      (-> estorno_cliente_divergente), estornaTradeId inexistente (-> orfao/retry),
      segundo estorno do MESMO movimento (-> estorno_duplicado), ESTORNO CUJO
      instrumentoId/quantidade/valorFinanceiro DIVERGEM do movimento original
      (-> estorno_divergente), envelope `v` nao
      suportado (-> versao_nao_suportada), ou decimal fora da escala da coluna. Cada um
      com desfecho NOMEADO e DISTINGUIVEL — nunca "algo deu errado", e nunca dois casos
      com o mesmo nome.
    ESTRITA, A SEGUNDA METADE, e ela e a que pega erro de MAPEAMENTO: NENHUM CAMPO DO
      TradeRegistered E DESCARTADO SEM REGRA ESCRITA NA V2 DO F3. O teste percorre os
      campos do payload e exige, para cada um, ou uma coluna do movimento que o recebeu,
      ou a regra escrita que o dispensa. A UNICA DISPENSA E A DO ESTORNO
      (instrumentoId/quantidade/valorFinanceiro "conferidos e nao usados"), e o teste a
      afirma COMO DISPENSA DECLARADA — provando a CONFERENCIA, com um caso divergente que
      tem que virar estorno_divergente. "Vem do lookup" nao e dispensa; e origem. Foi um descarte silencioso (instrumentoId e
      quantidade do `aporte`) que passou por uma versao anterior deste roadmap; sem esta
      metade, o teste CODIFICA o proximo erro de mapeamento em vez de pega-lo.
    Valor monetario chega como STRING DECIMAL, nunca float JSON (5.1): parse para
      `decimal` com InvariantCulture, e falha de parse e `payload_invalido`, nunca zero.
      A prova de "decimal fora da escala" pressupoe esse parsing exato — com double no
      meio do caminho ela testa outra coisa.
    E AQUI ENTRA O CRUZAMENTO DOMINIO x BANCO que o F3 nao podia fazer (la nao havia
      validacao de Dominio): o teste do tipo SchemaTests.DominioEBanco_Concordam*, com
      as constantes de precisao/escala do F3 de um lado e numeric_precision/numeric_scale
      do information_schema do outro, mais o veredito do Dominio sobre o MESMO valor
      (PADROES 10.25).
  Antes de confiar na suite, confira de que estados o FAKE do broker e CAPAZ: um fake
  construido a partir de um bool so sabe dizer sim e nao, e o defeito mora no "sim
  parcial". Me mostre prova por mutacao das duas direcoes.

  O PRIMEIRO BOOT DRENA O BACKLOG acumulado desde o F2 — esse e o caso NORMAL. O teste
  de integracao exercita fila CHEIA, e a medicao de recurso (docker stats) e feita
  DURANTE o dreno, que e o pico.

  Configuracao: AS CINCO LISTAS mudam juntas com RabbitMq__{Host,User,Password} —
  (1) os dois composes (`:?` em producao, sem default), (2) o `envs:` do ssh-action,
  (3) O BLOCO `env:` DO MESMO STEP mapeando ${{ secrets.* }}, (4) o printf do .env
  (reescrito POR COMPLETO), (5) as dummies do config gate (nem a mais nem a menos).
  A (3) e a que se esquece: so no `envs:` a variavel e encaminhada INEXISTENTE, chega
  vazia e a guarda `-z` acusa "secret vazio" com o secret cadastrado e correto.
  Mesmo secret do F2, que ja cadastrou as listas (2) e (3): normalize UMA vez na origem
  com tr -d '\r\n'. Descomente e reescreva o bloco do .env.example. AVISE NO PR que e
  mudanca quebrante para quem ja tem .env local, citando as linhas novas, e rode
  `docker compose config -q` contra o .env REAL, nao contra o sintetico do gate.

  ALERTAS: editar infra/grafana/cloud/rules-custodia.yaml DESTE repo nao basta — o
  apply-cloud.sh le a copia de ../tesouro-direto-api/infra/grafana/cloud/. Edite as
  DUAS, rode o publicador com GC_GRAFANA_URL, GC_GRAFANA_TOKEN e TELEGRAM_BOT_TOKEN
  exportados e com o VALOR do token conferido, e prove pela API do Grafana Cloud que as
  regras estao PUBLICADAS. No hub os alertas ficaram semanas sem existir na nuvem porque
  os arquivos nunca tinham sido copiados, e o publicador pulava o bloco em silencio.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) um `POST /v1/operacoes` real no Operações aparecendo no
  livro da Custódia **por evento** — que é literalmente o critério de pronto do item 3 da
  §9, e só é verificável com os dois serviços; reentrega da mesma mensagem sendo no-op
  (uma linha, não duas); `posicao_corrente` batendo com a dobra da **V1 do F3** **nas três
  colunas**, nos três cenários que a dobra exige: compra-compra-venda-compra, **um
  `aporte`** (que dobra como compra) e **compra → venda a preço diferente do custo →
  estorno da venda**, este último exigindo que as três colunas voltem ao estado anterior à
  venda. Mais o invariante que atravessa todos eles: depois de
  compra/aporte/venda/resgate, `custo_total = preco_medio × quantidade`. (**Estrita**)
  nenhuma linha nasce de payload com `operacao` desconhecida, campo obrigatório ausente,
  `estorno` sem `estornaTradeId`, `estornaTradeId` de outro cliente, `estornaTradeId`
  inexistente, ou decimal fora da escala — cada um com desfecho nomeado;
  **prova por mutação** das duas direções, precedida da conferência de que estados o fake
  é capaz de representar. Mais:
  (a) **o cenário que a Decisão A existe para resolver, e que o desenho anterior não
  passaria:** estorno órfão publicado e, **atrás dele na MESMA fila**, o `TradeRegistered`
  original — o teste prova que o original **é entregue e processado enquanto o órfão
  espera**, e que a volta do órfão produz o ajuste correto **sem UPDATE nenhum**. Com
  `nack(requeue: true)` este teste trava, e é exatamente isso que ele existe para pegar;
  (b) o órfão **não grava e não consome a chave**, e estourado o teto de voltas ele aparece
  no residual da passagem do drenador com motivo `estorno_orfao_expirado` — não na DLQ.
  **E a prova de que o teto é alcançável, que é o que a versão anterior não tinha:** a
  **segunda** volta do mesmo órfão chega com `x-death[0].count == 2`. Se o republish não
  copiar os headers, esse número fica `1` para sempre e o teste reprova — é ele que separa
  "circula até estacionar" de "circula para sempre com a suíte verde";
  (b2) **ack só depois do confirm:** com o publisher confirm forçado a falhar (ou a
  demorar além do timeout), a mensagem original **não é confirmada** e volta por
  `nack(requeue: true)` — provado por injeção, porque o caminho feliz não distingue esta
  implementação da que dá ack antes;
  (b3) `estornaTradeId` de **outro cliente** estaciona **imediatamente** como
  `estorno_cliente_divergente`, **sem** passar pelo retry (o teste conta as voltas: tem de
  ser zero), e um segundo estorno do **mesmo** movimento estaciona como
  `estorno_duplicado` em vez de queimar o `x-delivery-limit`; um `v` desconhecido estaciona
  como `versao_nao_suportada`; e um estorno cujo `instrumentoId`, `quantidade` ou
  `valorFinanceiro` **divergem** do movimento original estaciona como `estorno_divergente`
  **sem gravar nada** — com **controle positivo**: o mesmo estorno com os três campos
  batendo grava o ajuste normalmente. Os quatro no mesmo teste, para que confundir um com o
  outro reprove;
  (b4) **confirm negado PERSISTENTE não vira laço quente:** com o publisher confirm forçado
  a negar sempre, o republish para o retry para no **teto de tentativas** e a mensagem
  estaciona como `retry_indisponivel` — nunca reentregando em laço e nunca caindo na DLQ.
  Provado por injeção, contando as tentativas;
  (c) **bijeção sobre um conjunto de eventos CONHECIDO**, não varredura genérica: publicar
  N eventos, drenar, e afirmar os dois sentidos (nenhum evento do conjunto sem linha,
  nenhuma linha fora do conjunto), **com controle positivo** — uma linha plantada fora do
  conjunto tem que **reprovar** a varredura. *A formulação anterior ("não existe movimento
  cuja `ref_externa` não resolva a um evento recebido") era infalsificável: não há inbox
  neste desenho, `ref_externa` é derivada do evento que criou a linha, e a asserção era
  verdadeira por construção para toda linha que o handler gravou — passava com o handler
  quebrado;*
  (d) o **drenador do parking** exercitado com mensagens de **dois motivos diferentes na
  mesma fila**, provando que drena um e devolve o outro, que a passagem completa devolve
  **COMPLETUDE com o residual por motivo publicado** (é dela que a contagem sai — não de um
  gauge, que zeraria no deploy seguinte). **Os SEIS desfechos, cada um com seu teste, mais
  a varredura sobre o PROCEDIMENTO — não sobre "todas as paradas possíveis":** o teste
  percorre as **seis FOLHAS da árvore de decisão** — (1) `N` acima do teto; (2) `N` não
  examinadas com volta detectada; (2b) `N` não examinadas por outra coisa; (3)
  `residual_motivo > 0`; (4a) `n_motivo = 0`; (4b) `n_motivo ≥ 1` — e exige que cada folha
  devolva **o** rótulo daquela folha, nunca ausência de rótulo. *A versão anterior deste
  Pronto mandava enumerar as **oito** combinações de três respostas binárias (`N`
  examinadas? × `residual_motivo` × `n_motivo`) e exigir "um dos cinco rótulos" — e isso
  **carimbava o buraco como coberto**: as quatro linhas com `N` não examinadas não são
  determináveis por aquelas três respostas, então o executor as mapearia todas para
  `LIMITE por teto` e o teste passaria verde sobre o ramo que não tinha regra. Enumerar
  folhas do procedimento e enumerar combinações de respostas não é a mesma coisa, e é a
  diferença entre conferir e carimbar (§10.22).* (i) COMPLETUDE, com
  `residual_motivo = 0` e `n_motivo ≥ 1`; (i-b) **PARCIAL → falha**, com uma mensagem do
  motivo pedido que o handler **rejeita de propósito**, provando que a passagem **não**
  devolve sucesso com 9 de 10 processadas — e provando também o extremo `n_motivo = 0` com
  `residual > 0`, que não é VAZIO_DO_MOTIVO; (ii) **VAZIO_DO_MOTIVO → INCONCLUSIVO**, provado nos **dois** estados que
  o produzem — fila **vazia** (`N = 0`) e, o que a versão anterior deste Pronto não cobria,
  **fila com mensagens só de outros motivos** (`N > 0`, `n_motivo = 0`), que é o estado
  normal da **segunda** execução e o que uma guarda ancorada em `N = 0` deixa passar como
  sucesso; (iii) **LIMITE por teto**, devolvendo falha e não sucesso parcial; (iv)
  **LIMITE por volta**, provado **pela costura injetável do `x-custodia-passagem-id`** — o
  fixture fixa o id da passagem (provider injetado ou `--passagem-id`) e planta na fila uma
  mensagem **já carimbada com esse id**, e a passagem tem de devolver `LIMITE por volta` ao
  consumi-la. *A prova é assim porque, com o teto do laço = `N` e a `custodia.parked` FIFO
  sem outro consumidor, **nenhuma mensagem republicada pela passagem corrente pode reaparecer
  dentro dela** — a folha é inalcançável por desenho, e é guarda contra a implementação que
  ignora o teto (ver a mecânica). Um Pronto que mandasse "fazer uma mensagem dar a volta de
  verdade" só fecharia com o executor **mutando a implementação**, que é o padrão que este
  arquivo combate; sem a costura, o ramo não é exercitável de boa-fé nenhuma;* (v)
  **INTERROMPIDA → falha**, com a `custodia.parked` **purgada** depois de a
  passagem ler `N` e antes de ela examinar as `N` — a fila esvazia em `k < N`, o teto não
  estoura e nada dá a volta. *Este é o teste que separa o rótulo novo dos dois `LIMITE`: sem
  ele, uma implementação que devolvesse `LIMITE por teto` para esse estado passaria, e é
  exatamente o que a versão anterior deste Pronto premiava.* **A purga entre a leitura de `N`
  e o consumo precisa de costura nomeada, e é a mesma família da anterior:** um ponto de
  suspensão injetável entre os dois passos (não `Thread.Sleep`, que torna o teste dependente
  de tempo). **E a métrica entra nas TRÊS alíneas (iii), (iv) e (v), não só nesta:** nenhuma
  das três publica `custodia_parked_mensagens`, provado lendo o valor anterior e exigindo que
  ele **não** tenha sido sobrescrito — a regra é pela árvore (publicam só as folhas que
  examinaram as `N`), e afirmá-la numa folha só deixa as outras duas verdes com a métrica
  truncada;
  (d2) **um `aporte` gravando o `instrumento_id` do evento e a `quantidade` do evento, E
  dobrando `preco_medio`/`custo_total` como uma compra** — conferir só instrumento e
  quantidade era o buraco da versão anterior deste Pronto: o `aporte` passava com a coluna
  de custo intacta e o preço médio inflado. Mais a varredura da segunda metade estrita,
  provando que nenhum campo do payload foi descartado sem regra escrita na V2;
  (d3) **os TRÊS valores de fronteira da dobra, porque o invariante `custo = pm × qtd` não
  pega nenhum dos três:** `venda sem compra` de 10 seguida de **compra que zera** (10 @ 100)
  → `quantidade 0`, `preco_medio 0`, `custo_total 0`, **sem exceção de divisão**; a mesma
  venda seguida de **compra que não zera** (4 @ 100) → `quantidade −6`, `custo_total 400` e
  `preco_medio` **inalterado**, jamais `−66,67`; e a mesma venda seguida da **compra que
  CRUZA para positivo** (20 @ 100) → `quantidade 10`, `preco_medio 100`, `custo_total 1000`,
  jamais `pm 200`. Mais a asserção `preco_medio ≥ 0` **em toda
  a tabela**, por varredura. **O terceiro é o que precisa estar escrito, porque as duas
  guardas o aprovam com o valor errado:** `200 × 10 = 2000` satisfaz o invariante e `200 ≥ 0`
  satisfaz a varredura — o segundo, ao menos, a varredura pegaria. O caso não é sintético: o
  F4 admite `quantidade < 0` no livro e prescreve a compra retroativa como conserto, e o
  conserto **maior que a venda** é o mais provável dos três;
  (d4) **o movimento retroativo re-dobrando a chave em vez de aplicar delta:**
  `compra 10 @ 100` em D1, `venda 4` em D2 e depois `compra 10 @ 200` com `data_evento` em
  D0 → `custo_total 2400` e `preco_medio 150`, **jamais** `2600` / `162,50`. É o único teste
  que separa a cláusula do `data_evento ≥ MAX(data_evento)` da ausência dela, e sem ele I4
  fica falso em silêncio até a reconciliação do F7 alertar sobre dado correto;
  (e) o comando de reconstrução rodado com a projeção propositalmente corrompida
  (`UPDATE` em `posicao_corrente`, permitido porque ela **não** é o livro) devolvendo **as
  três colunas** ao valor do livro;
  (f) `docker stats` medido **durante** o dreno. Limpeza (`TRUNCATE`) decidida **antes**
  do POST, com as tabelas conferidas em 0/0.

- [ ] **F5** — consequências contábeis do resgate: IR, IOF e a liquidação D+1.
  **Dependência externa nova: nenhuma.**
  **PENDÊNCIA BLOQUEANTE ABERTA: a definição de `prazo` — leia a pendência nas decisões desta
  fase ANTES de despachar; ela amarra modelagem por lotes contra custo médio, e sem ela dois
  critérios de Pronto desta fase passam por vacuidade.**

  No mesmo handler de `TradeRegistered`, quando `operacao = resgate` (que no livro é
  `tipo = venda`, V2 do F3): `ir_retido` e `iof` (**base = o GANHO da alienação**,
  `valorFinanceiro − preco_medio × quantidade`, com piso em zero — ver a decisão da base,
  abaixo; alíquota regressiva pelo `prazo`, cuja definição é **PENDÊNCIA BLOQUEANTE desta
  fase**; IOF nos primeiros 30 dias), mais `a_liquidar` em D; e, em **D+1 útil**, a
  `liquidacao` de **duas pernas** (`caixa:a_liquidar` −Z, `caixa:BRL` +Z, com
  `Z = Y − IR − IOF`, o **saldo** do fato em `caixa:a_liquidar`), gravada por um
  **job de liquidação** desta fase. Caixa como instrumento (`caixa:BRL`,
  `caixa:a_liquidar`, com preço 1 por definição — V4 do F3). Calendário de dias úteis.
  **Reversão dos derivados quando um resgate é estornado.** Backfill idempotente da janela
  F4→F5. Regras fiscais **portadas** do simulador da TD API (§11: portar, não reinventar) e
  conferidas contra ele.

  **O vocabulário é o do F3, literalmente.** As chaves desta fase são as da V3:
  `ir:<tradeId>`, `iof:<tradeId>`, `aliq:<tradeId>`, **`liq:<tradeId>:aliq`** e
  **`liq:<tradeId>:brl`** — a liquidação tem **duas** chaves porque tem duas pernas e
  `movimentos` tem **um** `instrumento_id` por linha; com uma chave só elas colidiriam no
  `UNIQUE (cliente_id, ref_externa)`. E as chaves de reversão são `est:ir:<tradeIdEstorno>`,
  `est:iof:…`, `est:aliq:…`, `est:liq:…:aliq`, `est:liq:…:brl`.

  **Ordem, instrumento e sinal de cada linha estão na tabela de LINHAS DERIVADAS da V2 do
  F3 — copie-a, não a reinvente.** O movimento da venda carrega o valor **bruto** no
  instrumento do título; `ir_retido` e `iof` são linhas próprias **em `caixa:a_liquidar`,
  com `qtd_delta` negativo**, nunca desconto embutido (§7.1); e `a_liquidar` entra
  **BRUTO**, porque IR e IOF são retidos na fonte e são as próprias linhas de tributo que o
  reduzem — o valor a receber é uma **soma do livro**, não um número calculado na escrita.
  Em D nascem a venda, os tributos e o `a_liquidar`; em **D+1 útil** nasce a `liquidacao`,
  que **zera o saldo** de `caixa:a_liquidar` daquele fato e credita `caixa:BRL`. O snapshot
  de D mostra o limbo honestamente (Fluxo 3, §8.4), e a **aritmética de D e D+1 — os dois
  invariantes independentes de preço, mais a hipótese de fixture** — está escrita na V2.

  **Decisões desta fase:**

  - **A BASE DO IR É O GANHO DA ALIENAÇÃO, NÃO O PREÇO MÉDIO — e a frase da §7.3 é ELIPSE.**
    A §7.3 escreve "base = preço médio do livro"; lida literalmente, ela manda cobrar ~22,5%
    **do custo inteiro** em vez do lucro. O preço médio é o **insumo** da base. **Fórmula, e
    ela é a decisão:** `base = valorFinanceiro_da_venda − preco_medio × quantidade`,
    **com piso em zero**. **A evidência é do simulador que esta própria fase manda portar**
    (`../tesouro-direto-api`), e ela é literal — **conferida linha a linha no arquivo, não
    citada de memória**: `src/TesouroDireto.Application/Tributos/TributosPadrao.cs` linhas
    **46** e **71** declaram os dois tributos como `BaseCalculo.**Rendimento**` — não preço
    —; `src/TesouroDireto.Domain/Simulador/SimuladorService.cs` linha **138** calcula
    `rendimentoBruto = Math.Round(valorBruto − input.ValorInvestido, 2)`; e
    `TributosPadrao.cs:51-57` traz o IR como `TipoCalculo.FaixaPorDias` com as faixas
    `(0,180) (181,360) (361,720) (721,999_999)`, e o IOF como `TabelaDiaria` sobre o vetor
    `IofAliquotas` de **29** alíquotas diárias (linha **8** em diante). *Esta fase manda "portar e conferir contra o
    simulador"; portar o motor com a base errada é conferir uma coisa contra outra.*
    **Base negativa — venda com prejuízo — tem regra, e ela não estava escrita:** piso em
    zero, `IR = 0` e `IOF = 0`, e as linhas `ir:` e `iof` **NÃO EXISTEM** (não se grava zero),
    exatamente como já vale para o IOF ausente. *Ausência decidida:* prejuízo **não** gera
    crédito, compensação nem linha de qualquer espécie nesta fase — compensação de perdas é
    regime que exige histórico fiscal próprio, e nada neste roadmap o modela; registrar a
    ausência aqui é o que impede alguém de improvisá-la dentro do cálculo de imposto.
    **Compra RETROATIVA que muda a base (ou o `prazo`) de uma venda já tributada:** mesma
    família do `preco_medio` provisório da decisão seguinte — **grava, sinaliza, e o conserto
    é estorno do resgate e relançamento; nunca UPDATE**, que a trigger barraria de qualquer
    modo. O sinal é o mesmo *"resgate tributado sobre preço médio provisório"*, com o motivo
    do sinal dizendo qual dos dois casos ocorreu.

  ### PENDÊNCIA BLOQUEANTE DO F5 — a definição de `prazo`

  **Estado:** aberta. **Bloqueia:** o F5 (e o F9, que reusa o motor). **Quem decide:** o dono
  — amarra **modelagem por lotes contra custo médio**, e o custo médio é o que a V1 do F3 já
  fechou. **Não despache o F5 sem isto.**

  A alíquota do IR depende de **dias corridos desde a AQUISIÇÃO**. Uma posição construída por
  N compras em datas diferentes **não tem uma data de aquisição**, e a V1 do F3 escolheu
  guardar **custo médio** (`preco_medio`, `custo_total`) e **não lotes** — a informação que
  `prazo` exige foi agregada fora. Grep no arquivo inteiro antes desta rodada: `prazo`
  aparecia **onze** vezes e **nenhuma** o definia.

  **A conta que mostra o tamanho:** FIFO, data média ponderada, lote mais antigo ou lote mais
  novo mudam a alíquota entre **22,5% e 15%** — **50% de diferença no imposto**, gravado numa
  tabela **sem UPDATE**.

  **As opções, com o custo de cada uma:**

  - **(a) FIFO por LOTES materializados** — é o que a corretora faz, e é o único que bate com
    o extrato dela, que é o nosso único conferente externo. **Custo:** exige uma estrutura de
    lotes que a V1 não tem. **E ela faria o inventário "há DUAS tabelas fora da §7.1" (F3 e
    F7) virar TRÊS** — se for esta a escolha, os três lugares que declaram "duas" mudam
    juntos, e a tabela nasce nesta fase (é projeção reconstruível do livro, então **não** cai
    na §10.21: o custo é retrabalho de fase, não dado perdido).
  - **(b) FIFO DERIVADO do livro, sem materializar lotes** — a mesma regra de (a), computada
    na dobra: o livro **tem** as datas e as quantidades, e o custo é O(movimentos daquela
    chave), que este arquivo já paga em **todo `ajuste`**. **Custo:** o cálculo do imposto
    passa a percorrer o livro, e a dobra da V1 ganha uma saída a mais (a fila de lotes), o que
    exige dizer se essa saída faz parte da dobra ou é função ao lado. Nenhum inventário
    cresce. *É a que eu recomendaria, e a recomendação fica registrada como recomendação.*
  - **(c) data média ponderada por quantidade** — cabe no custo médio que já existe e não
    custa nada. **Custo:** não corresponde a **nenhuma** regra fiscal real; diverge do extrato
    da corretora por construção, e o Pronto "conferido contra o simulador" passa a ser
    impossível de honrar.
  - **(d) `data_evento` da PRIMEIRA compra da chave, derivada do livro** — barato e
    determinístico. **Custo:** **superestima o prazo** e portanto recolhe imposto **a menos**,
    que é a direção errada de errar; e uma compra retroativa anterior muda o `prazo` de vendas
    já tributadas.

  **Por que bloqueia, e não é zelo:** sem `prazo` definido, **dois critérios de Pronto desta
  fase passam por VACUIDADE**. O (c) exige "o dia exato da virada de faixa, o dia 30 do IOF" —
  não se testa a fronteira de uma grandeza indefinida: o executor **define** `prazo` enquanto
  escreve o teste, e o teste certifica o que ele acabou de decidir. E o (i), "alíquotas
  conferidas contra o simulador", é **inexecutável**: o `SimulacaoInput`
  (`src/TesouroDireto.Domain/Simulador/SimulacaoInput.cs`, conferido) modela **uma
  aplicação** — um `DataCompra` e um `ValorInvestido`, campos escalares — e não sabe
  responder sobre uma posição de custo médio com N compras — a conferência só existe depois de `prazo` ter uma
  definição que se possa traduzir em `DataCompra`.

  - **Tributar a partir de um `preco_medio` PROVISÓRIO: grava, sinaliza, e o conserto é
    estorno — nunca UPDATE.** O `preco_medio` **do livro** é o insumo da base (decisão
    acima), e a V1 do
    F3 declara as três colunas **provisórias** enquanto a `quantidade` for negativa. Essa
    palavra tranquiliza para `posicao_corrente`, que se refaz por dobra, e **não vale para o
    livro**: `ir_retido` e `iof` gravados a partir de um `preco_medio` provisório ficam numa
    tabela append-only e **só saem por estorno**. Regra desta fase: o resgate **entra do
    mesmo jeito** — recusar reintroduziria aqui a validação que a ADR-10 tirou de Operações,
    e é a camada 3 da §6.1 —, o tributo é calculado com o `preco_medio` que existe, e o caso
    **é sinalizado** (métrica + log estruturado + alerta) como *"resgate tributado sobre
    preço médio provisório"*, distinto do sinal de posição negativa do F4 porque aqui já
    houve **linha de tributo gravada**. **O caminho de reparo é o que esta fase já
    implementa:** estorno do resgate (que reverte os derivados, `est:ir:…`, `est:iof:…`) e
    relançamento **depois** de a compra faltante entrar. *Rejeitado:* estacionar o resgate
    até a posição ficar positiva — o parking é para o que não se sabe escriturar, e aqui se
    sabe; e a mensagem ficaria parada sem que nada garantisse que alguém lançaria a compra.
    *Rejeitado:* recalcular o tributo por UPDATE quando a compra retroativa chegar — é edição
    destrutiva de fato, proibida nominalmente pela §9 dos padrões, e no livro é impossível
    por trigger.
  - **Calendário de dias úteis: tabela de configuração própria neste banco**, populada por
    migration/seed. *Rejeitado:* consultar a TD API ou o Hub no caminho de escrita — poria
    fonte externa dentro do processamento de um evento já aceito, e a §13 item 4 registra o
    endpoint de feriados como melhoria **futura**, explicitamente não pré-requisito (a
    ADR-12 ainda proíbe ler o banco da TD API). **Não há default seguro:** sem decidir,
    "D+1 útil" degenera para "hoje + 1 dia corrido" em silêncio, e o erro só aparece num
    feriado, meses depois, num movimento já gravado em tabela append-only. Registrar que é
    dado de **configuração** e não projeção: não se reconstrói do livro. E o calendário
    também serve o F7 — "dia útil" do snapshot sai daqui.
    *Rejeitado nominalmente:* derivar "dia útil" de "dia em que houve preço" — a §7.1
    proíbe misturar o critério de "dia sem pregão" com o de "repete o preço".
  - **A `liquidacao` é gravada EM D+1 ÚTIL, por um job de liquidação — não antecipada no
    instante do resgate. Isto é um DESVIO POR CORREÇÃO do texto literal da §7.3 —
    registre-o e grave-o na memória ao fechar a fase**, na mesma convenção dos outros
    desvios rotulados deste arquivo. **Esta enumeração é a LISTA CANÔNICA de desvios do
    roadmap — desvio que não estiver aqui não está rotulado —, e são DEZ, contando o
    desta fase:**
    (1) a `liquidacao` em D+1 útil por job, aqui, contra a §7.3;
    (2) o `aporte` no enum, na V1 do F3, contra a §7.1;
    (3) o `ref_externa NOT NULL`, na V3 do F3, contra a §7.1;
    (4) o **recorte `[desde, U]`** do worker, no F7, contra a §7.4 (`U` = último dia já
    materializado, no lugar do `hoje` que a §7.4 escreve);
    (5) o **intervalo `[U_anterior + 1, D]`** do handler de `eod.ready`, no F7, contra a §7.3
    (que escreve `EodPricesReady(D)` no **singular**);
    (6) a tabela **`eod_processado`**, no F7, contra a **ADR-5** e a **§9 do `PADROES`**
    (watermark em coluna de controle, no lugar do derivado) — **o único desta lista que viola
    um antipadrão NOMINAL da §9, e por isso o que carrega a cláusula inteira: aprovado pelo
    `advisor` com QUATRO condições escritas na fase** (`U` atrasa e nunca adianta, com **a
    unidade de completude sendo o DIA**: a linha `eod_processado(d)` entra na **mesma
    transação que fecha o dia `d` inteiro**, percorrido em ordem crescente — *a formulação
    anterior desta condição, "só no commit que fecha a última unidade do batch", foi
    revogada pelo `advisor`: ela tratava o batch como unidade de completude e contradizia a
    própria condição (iii) em operação normal*; a regra do par
    `snapshots_posicao` ↔ `eod_processado` na direção inversa; a DDL com
    `PRIMARY KEY (data_ref)`; e o escopo estreito — o desvio autoriza **este** watermark e só
    ele, e o backfill de preço do F6 continua derivado) —, **e a gravar na memória ao fechar a
    fase**, na mesma convenção do desvio (8);
    (7) a **derivação da ausência de preço**, no F7, contra a §10.32;
    (8) o cursor com `X-Total-Count` ausente, no F8, contra a §2 do `PADROES`;
    (9) a posição na data derivada do **livro** e não de `posicao_corrente`, no F9, contra a
    §7.3;
    (10) o **TETO do intervalo do handler de `eod.ready`** (default 90 dias úteis,
    `IConfiguration`), no F7, contra a §7.3: acima do teto o handler **recusa materializar um
    `D` que o Hub anunciou** — não processa, devolve **LIMITE** (§10.31), alerta, e a mensagem
    vai para a `custodia.parked` com o motivo `intervalo_acima_do_teto`; a materialização
    daquele `D` passa a depender do comando administrativo `materializar --desde --ate`
    (isento do teto) seguido do drenador daquele motivo, que reentrega o evento. **Por que é
    desvio e não interpretação:** a §7.3 prescreve o mecanismo (o ramo `EodPricesReady(D)`
    **dentro** do handler), e aqui o resultado **não** é alcançado por outro meio — há uma
    janela, entre o parking e a ação do operador, em que o `D` anunciado não está
    materializado, e pelo FATO 2 nenhum `eod.ready` futuro o reanuncia. É a mesma forma do
    desvio (1). **A garantia que sobra, e é ela que torna o desvio aceitável:** a mensagem
    nunca é descartada, o `D` nunca é perdido, e o ciclo parking → comando → drenador →
    reentrega fecha com peças que aquela fase já tem — o Pronto (f2) prova o **ciclo inteiro**,
    não só a recusa. **A gravar na memória ao fechar a fase**, na mesma convenção dos desvios
    (6) e (8). *Rejeitado dobrar isto dentro do desvio (5):* (5) diz "materializa **MAIS** que
    `D`" e o teto diz "materializa **MENOS** que `D`" — juntar duas divergências opostas numa
    linha faz a lista deixar de ser enumerável, e uma fase futura que revisitasse o intervalo
    herdaria ou perderia o teto em silêncio.
    *A lista não é "tudo que diverge da `ARQUITETURA`": é a dos desvios que este roadmap
    **decidiu e rotulou**. Fase que criar outro acrescenta **duas** coisas, e uma sem a outra
    é defeito: o rótulo dentro da própria fase (senão o executor lê a `ARQUITETURA` e encontra
    a contradição sem explicação) e a linha aqui (senão a lista deixa de ser canônica e vira
    lista com default informal, o mesmo defeito que a lista de motivos do F4 já nomeia).
    Contraexemplo tentado contra o "dez": varri as ocorrências de "DESVIO/desvio" no arquivo
    e todas as marcações de desvio adotado caem numa destas dez — as demais ocorrências são
    alternativas **rejeitadas** ou citações destas mesmas dez. Esse método só acha o que já
    está rotulado, então os candidatos são procurados **por fora** dele e adjudicados por
    escrito, para a lista não crescer por descuido nem encolher por omissão. **São CINCO
    adjudicações até aqui — duas que entraram, três que não:** a **ordem do
    extrato de movimentação** do F8 **não** é item novo — ele ordena e pagina pela tripla
    `(data_evento, registrado_em, id)`, que respeita literalmente as duas chaves da §7.5 e só
    acrescenta o desempate que ela deixa em aberto; o **alerta das 12:00 medido em
    `processado_em`** também **não** é item novo, é **interpretação**; o **TETO do intervalo
    do handler** (F7) **É** desvio e virou o item (10) acima; e a **extensão do job de
    reconciliação** (o alerta `MAX(snapshots_posicao.data) > U`, F7) **NÃO** é desvio —
    **nenhuma fonte prescreve o conteúdo do job de reconciliação**, que é decisão deste
    roadmap (Decisão B) e não da `ARQUITETURA`; acrescentar-lhe uma comparação é escolher o
    meio de satisfazer um resultado que ninguém prescreveu, e por isso não entra; e a
    **varredura de defasagem de snapshot** (F7) **NÃO** é desvio — a §7.4 prescreve os três
    gatilhos e o worker único, e não prescreve **por que meio** um gatilho chega ao worker;
    a varredura não é um quarto gatilho (chama o mesmo `recalcular`, com o mesmo recorte
    `[desde, U]`, e sobre `revisao > 0`), é a entrega **durável** dos gatilhos 1 e 2. *Se ela
    materializasse dia que o `eod.ready` não fechou, seria desvio da ADR-9 e não caberia numa
    linha de lista — não materializa, e é por isso que o recorte dela está escrito.* *A quarta
    adjudicação está escrita aqui porque a decisão de não entrar é tão adjudicação quanto a de
    entrar: candidato examinado e recusado em silêncio é indistinguível de candidato não
    examinado.*
    **O critério que separa uma coisa da outra, e ele vale para as próximas fases:** quando a
    fonte prescreve o **MECANISMO** — colunas, chaves de ordenação, forma do contrato —,
    trocá-lo é **desvio** e entra nesta lista; quando ela prescreve o **RESULTADO** — um
    alerta, uma garantia —, escolher o meio que o satisfaz é **interpretação** e não entra. O
    alerta da §12 é do segundo tipo: `processado_em` é o único campo que responde *"chegou
    evento hoje?"*, e a leitura literal (`data_ref`) deixaria o alerta disparando todo dia
    útil, para sempre — satisfazer o resultado prescrito não é divergir dele. **A lista
    passou a DEZ**, recontados contra este arquivo — o item novo é o teto do F7, adjudicado
    acima. A §7.3 põe "`+ a_liquidar → liquidacao` (D+1 útil)" **dentro do handler**; esta
    fase move a segunda metade para um `IHostedService`. O apoio é a **§8.4**, que põe a
    liquidação em outro momento com todas as letras ("Note over C: dia D+1 útil") — a §7.3 é
    a linha do tempo comprimida, o Fluxo 3 é ela esticada. **Sem o rótulo**, o executor que
    ler a §7.3 (como o próprio prompt manda) encontra a contradição sem explicação e tende a
    seguir a `ARQUITETURA`. *A decisão foi invertida em relação ao rascunho anterior, e o
    motivo é que a versão antecipada contradizia I4 e o Pronto desta própria fase.* Gravar
    em D uma linha com `data_evento` no futuro põe no livro um fato que **ainda não
    aconteceu**; e como `posicao_corrente` é, pela §7.1, Σ `qtd_delta` do livro **sem
    recorte de data**, a projeção passaria a mostrar, já em D, o dinheiro em `caixa:BRL` e
    o `caixa:a_liquidar` zerado — **o limbo D→D+1 sumiria da posição corrente**, contra o
    Pronto desta fase e contra I4, que o Pronto do F4 manda provar.
    *Rejeitado:* manter a gravação antecipada e redefinir `posicao_corrente` como Σ
    `qtd_delta` com `data_evento ≤ hoje`. Quatro custos, e nenhum é hipotético: (i) a
    projeção deixa de ser **dobra pura** do livro e passa a depender do **relógio**,
    mudando sozinha à meia-noite sem que nenhum movimento tenha entrado; (ii) o handler do
    F4, que atualiza a projeção incrementalmente na mesma transação, não teria como aplicar
    a linha futura no momento da escrita — **exigiria um job de virada diária de qualquer
    modo**, então a alternativa não elimina o job, só o move para cima de uma projeção;
    (iii) a **reconciliação do F7** — o único detector automático de divergência que este
    sistema tem — passaria a depender do relógio e alertaria em falso a cada virada com
    liquidação pendente; (iv) o **extrato de movimentação do F8** é `SELECT` ordenado por
    `data_evento` (§7.5), e mostraria hoje, ao cliente, uma liquidação que não ocorreu —
    contra P2. O §8.4 também põe a liquidação em outro momento, com todas as letras
    ("Note over C: dia D+1 útil").
    **Como o job é, para "job que atrasa" não ser objeção:** `IHostedService` idempotente
    que seleciona as linhas `a_liquidar` cuja data de liquidação (D+1 útil, derivada do
    `data_evento` da própria linha pelo calendário desta fase — determinística) é ≤ hoje,
    **que não foram revertidas por um `ajuste`** e que ainda não têm a perna
    `liq:<fato>:brl`, e insere as **duas** pernas com `data_evento` = a data de liquidação.
    Rodar duas vezes é no-op pela `ref_externa`. A condição de parada é classificada
    (§10.31): **COMPLETUDE** = varri todas as `a_liquidar` vencidas e cada uma tem as duas
    pernas; **LIMITE** = teto de linhas por ciclo → **falha**, nunca sucesso parcial. E a
    **mesma consulta que o job usa é a guarda permanente**: `a_liquidar` vencida sem
    `liq:<fato>:brl` vira **métrica e alerta**, então um job que atrasa **aponta para si
    mesmo** em vez de deixar o livro incompleto em silêncio. *Não confunda com o worker da
    §7.4:* aquele é do F7 e recalcula snapshots; este só apenda duas linhas por
    liquidação vencida.
    **O filtro "que não foram revertidas" não é detalhe, e ele olha DUAS linhas, não uma.**
    Se o estorno de um resgate chegar em D, antes de a liquidação existir, o job **não pode**
    liquidar um `a_liquidar` já revertido — senão credita dinheiro em `caixa:BRL` de uma
    operação que não existe mais, numa tabela onde não há DELETE. **E a condição é: nem a
    linha `aliq:<fato>` nem o MOVIMENTO PRINCIPAL do fato podem estar revertidos.** A segunda
    metade não é redundância: na janela F4→F5 o estorno de um resgate produziu `ajuste`
    **só sobre o principal**, porque `aliq:` ainda não existia (é a exceção declarada a I13
    do F4). Assim que o backfill desta fase criar o `aliq:` daquele resgate, ele nasce
    **vencido** (data passada) e **não revertido** — e um filtro que olhasse só a própria
    linha mandaria o job creditar `caixa:BRL` de um resgate que **não existe mais**, que é
    exatamente o dano que este parágrafo existe para impedir, entrando pela porta do reparo.
    *Corolário que precisa estar escrito: enquanto a linha `aliq:<fato>` não existe, o fato
    **não tem ponto de serialização** — o `SELECT … FOR UPDATE` abaixo trava uma linha que
    ainda não nasceu. É por isso que a metade (i) do backfill roda com o job **parado**, e
    não só com o filtro certo.* (Este é, aliás, mais um custo da gravação
    antecipada: lá a linha de liquidação já estaria no livro e precisaria de uma reversão
    própria de um fato que nunca aconteceu.)
    **E o filtro sozinho NÃO basta, porque o job e o consumidor são CONCORRENTES.** O job é
    um `IHostedService`, com thread própria; o handler de estorno roda no `BackgroundService`
    do consumidor. São dois processos no mesmo container, e entre o `SELECT` do job e o seu
    `INSERT` há uma janela — que a decisão (c), de **ciclo curto**, torna frequente. Os dois
    desfechos são permanentes num livro sem DELETE: (i) o estorno commita **entre** o
    `SELECT` e o `INSERT` e o job liquida um `a_liquidar` já revertido — exatamente o dano
    que o filtro existe para impedir, agora por corrida em vez de por esquecimento;
    (ii) o job insere **entre** a decisão do handler ("não há `liq` a reverter") e a
    gravação do conjunto `est:`, e o conjunto `est:liq:` nunca é escrito — o
    `UNIQUE (ref_estorno)` não ajuda, porque nada foi gravado.
    **Regra: a linha `aliq:<fato>` é o PONTO DE SERIALIZAÇÃO do fato.** Tanto o job quanto o
    handler de estorno a travam com `SELECT … FOR UPDATE`, **na mesma transação** em que
    gravam. **Dependência oculta, escrita aqui e no F3 para não ser desligada por engano:**
    o `SELECT … FOR UPDATE` exige, no Postgres, privilégio de escrita sobre `movimentos`, e
    só é possível porque a imutabilidade do livro é por **trigger** e **nunca por `REVOKE`**
    (decisão do F3). Um `REVOKE UPDATE` "de endurecimento" derruba a serialização **sem
    quebrar teste nenhum**. Assim, qualquer que seja a ordem: se o job chega primeiro, o
    handler encontra as duas pernas já no livro e as reverte junto; se o estorno chega primeiro, o job encontra
    o `a_liquidar` revertido e não liquida. *Rejeitado:* `SERIALIZABLE` no nível da
    transação — resolve, mas com retry sob falha de serialização em dois caminhos que hoje
    não têm essa política, e é martelo maior que o prego. *Rejeitado:* rodar o job dentro do
    laço serial do consumidor — amarraria a cadência do job ao fluxo de mensagens, e ele
    precisa rodar com a fila parada. **O Pronto (f), sequencial, passa com a corrida
    intacta:** a prova é por injeção, com barreira entre o `SELECT` e o `INSERT`.
    **Três condições do job que são de dado irreversível, e por isso ficam escritas aqui:**
    1. **Catch-up depois de dias fora do ar grava `data_evento` NO PASSADO** — e a §7.3 diz
       que movimento com `data_evento < hoje` **dispara `recalcular`**. Se o job "não
       recalcula nada", como dizia o rascunho, os snapshots dos dias perdidos ficam com
       `caixa:a_liquidar` **para sempre** e nada os conserta. Regra: quando a linha que ele
       insere é **retroativa**, o job **enfileira `recalcular(cliente, instrumento,
       desde=data_liquidacao)`** — o gatilho 1 do worker do F7, sem caminho novo. Antes de o
       F7 existir não há snapshot e não há o que recalcular; a partir dele o job passa a
       enfileirar, e isso é dependência **declarada**, não descoberta.
       **O ENFILEIRAMENTO É CAMINHO RÁPIDO, NUNCA A GARANTIA — e isto tem de estar escrito
       aqui, porque "enfileira" lido sozinho vira `Channel<T>` em memória.** Um restart entre
       o commit das duas pernas e o recálculo **perde a chamada para sempre**: as pernas já
       estão no livro, o `eod.ready` seguinte percorre `[U_anterior + 1, D]` e **não volta**
       aos dias já materializados, e o caso 1 acima diz o que sobra — snapshots com
       `caixa:a_liquidar` **para sempre**. Isso violaria o **P2 da `ARQUITETURA`** ("crash em
       qualquer ponto se resolve na próxima execução"). **A garantia é a *varredura de
       defasagem de snapshot* do F7**, que é **derivada do dado** (compara `registrado_em` do
       movimento com `calculado_em` do snapshot vigente daquele dia) e refaz o que a fila
       perdeu, sem estado de controle nenhum. O job não precisa saber disso para funcionar; o
       Pronto (c2) é que precisa, e ele exercita **crash entre o commit do job e o
       recálculo**.
    2. **Calendário exaurido é falha alta, nunca modo degradado.** Feriado e fim de semana
       funcionam por construção (nada vence naquele dia). O que mata é a data **fora do
       horizonte do seed**: aí o job tem de **falhar alto e alertar**, jamais cair em "+1 dia
       corrido" — o erro que só aparece meses depois, num movimento já gravado em tabela
       append-only. A guarda de horizonte ("alerta quando o calendário acaba em menos de N
       dias") é desta fase, não do dia em que faltar.
    3. **O intervalo do ciclo é fixado em relação ao `eod.ready` do mesmo dia.** Rodando uma
       vez por dia e tarde demais, o snapshot de D+1 nasce **sem** `caixa:BRL` e cai no caso
       1 — passaria a precisar de recálculo todo dia, por desenho. Decisão: ciclo **curto**
       (ordem de minutos), de modo que a liquidação de D+1 já esteja no livro quando o
       `eod.ready(D+1)` chegar. O custo é irrisório — é O(`a_liquidar` em aberto) — e a
       alternativa custa um recálculo diário permanente.
  - **Estorno de um resgate reverte TODOS os derivados, por INSERT — e esta fase é quem
    sabe quais são.** O F4 grava o estorno como **uma** linha `ajuste` sobre o movimento
    principal; mas o resgate original produziu aqui `ir_retido`, `iof`, `a_liquidar` e (se
    já liquidado) as duas pernas da `liquidacao`. Sem reverter os derivados, o livro fica
    com **tributo retido e dinheiro creditado em `caixa:BRL` de uma operação que não existe
    mais** — e, sendo append-only, para sempre. O roadmap tratou com cuidado o estorno
    **fora de ordem** (Decisão A) e não tratava o estorno **correto** de uma operação com
    consequências contábeis, que é o caso comum.
    Regra: um `ajuste` sobre um fato que produziu derivados gera o **conjunto simétrico**
    de linhas de reversão, cada uma com `tipo = 'ajuste'`, `ref_externa` própria da família
    `est:` (V3 do F3), `ref_estorno` apontando para **a linha revertida** e `qtd_delta` /
    `valor_financeiro` simétricos a ela. Tudo por **INSERT** — nenhum UPDATE, que a trigger
    barraria de qualquer jeito. O `UNIQUE (ref_estorno) WHERE ref_estorno IS NOT NULL` (V5)
    garante que cada linha é revertida **no máximo uma vez**, inclusive as derivadas.
    Casos que o teste tem que cobrir, porque o conjunto varia: estorno **antes** da
    liquidação (não há `liq` a reverter, e o `a_liquidar` revertido tira a linha do escopo
    do job), estorno **depois** da liquidação (as duas pernas são revertidas), e resgate
    **sem IOF** (prazo ≥ 30 dias — não existe `iof:` a reverter, e o handler não pode
    tentar).
    *Rejeitado:* reverter só o movimento principal e "acertar depois" — não há depois numa
    tabela sem UPDATE. *Rejeitado:* pôr a regra no F4 — é aqui que os derivados existem, e
    o F4 não os conhece; o que o F4 faz é declarar a exceção a I13 e deixá-la para o
    backfill desta fase.
  - **A tabela regressiva e as faixas ficam num dono só**, com teste de fronteira.
    *Rejeitado:* reimplementar a regra fiscal a partir da descrição em prosa — a §11 manda
    portar do simulador da TD API justamente porque a lógica já existe validada, e
    reinventá-la produz divergência com o extrato real da corretora, que é o único
    conferente externo que temos.
  - **Arredondamento sempre no financeiro (2 casas), nunca na quantidade** (§11).

  **A janela F4→F5 fecha com GUARDA PERMANENTE, não com script de uma vez — e ela tem DUAS
  metades.** (i) resgate sem linha `ir:<tradeId>`; (ii) `ajuste` sobre um resgate sem o
  conjunto `est:` correspondente. As duas consultas viram **métrica e alerta permanentes**,
  porque o que elas detectam pode reaparecer por um bug do handler depois. Some-se a
  terceira, que é do job: `a_liquidar` vencida sem `liq:<fato>:brl`. O backfill é `INSERT`
  (append), nunca `UPDATE`, e é idempotente pela `ref_externa` própria de cada derivado.

  **TRÊS CONDIÇÕES DE EXECUÇÃO DO BACKFILL, e nenhuma é opcional — porque sem elas o reparo
  credita dinheiro de operações que não existem mais.** O cenário é real e nasce do desenho:
  um estorno chegado **na janela F4→F5** gravou `ajuste` sobre o movimento **principal** e
  nada mais, porque `ir:`, `iof:` e `aliq:` não existiam.

  1. **A metade (i) PULA resgates cujo movimento principal esteja revertido.** Sem isso ela
     cria `ir:`, `iof:` e `aliq:` para um resgate que **não existe mais** — e o `aliq:` nasce
     já vencido e não revertido.
  2. **A ORDEM é (i) e DEPOIS (ii), e fica escrita.** A metade (ii) só pode rodar depois da
     (i), porque o `ref_estorno` de cada `est:` precisa da linha alvo já gravada. A janela
     entre as duas é aberta **pelo desenho**, não por acidente: é nela que existem `aliq:`
     recém-criados ainda sem o `est:aliq:` correspondente.
  3. **O backfill roda com o JOB DE LIQUIDAÇÃO PARADO**, e o filtro do job olha o **fato
     principal** (regra acima). São as duas metades da mesma proteção e nenhuma substitui a
     outra: o job é `IHostedService` de **ciclo curto** (decisão (c)) e **sobe no mesmo
     deploy** do backfill, então sem o parar ele varre a janela do item 2 enquanto ela existe;
     e sem o filtro robusto ele voltaria a errar na primeira reentrega depois de religado.
     *O Pronto (h) sozinho não pega nada disso: ele confere idempotência e "três consultas de
     guarda devolvendo 0" **ao final**, e passa verde depois de o dano estar gravado.*

  **NÃO ENTRA:** cupom e vencimento (F9 — mesmo motor de tributação, gatilho diferente);
  snapshot e o worker de recálculo da §7.4 (F7 — o job de liquidação desta fase **não é**
  aquele worker: ele só apenda duas linhas por liquidação vencida, e não recalcula nada).

  **Configuração: nenhuma** das cinco listas muda — o calendário é tabela local, não
  credencial. (Se a decisão do calendário um dia virar dependência HTTP, as cinco listas
  mudam **nessa** fase, não nesta.) **Recurso:** o job de liquidação é O(`a_liquidar` em
  aberto), que é minúsculo; meça com `docker stats`, mas a fase que muda o perfil continua
  sendo o F7.

  **Âncoras:** `ARQUITETURA` §7.1 (convenções: caixa é instrumento, tributos são movimentos
  próprios), §7.3 (ramo `se resgate`), §8.4 Fluxo 3, §6.1 camada 3, §11 (IR/IOF validados
  no simulador; frações e arredondamento; D+1 conta **dias úteis**), §13 item 4;
  `PADROES` §10.25, §10.21, **§10.34** (fuso, herdado do F3 — é ele que decide "hoje" e "dia
  útil" do job de ciclo curto), **§10.35** (fila em memória não é durabilidade — é ela que
  decide o que "enfileira `recalcular`" significa); `LEIA-ME-KIT` "A frase elíptica da
  especificação chega crua ao prompt, e vira dinheiro" (é sobre a base do IR desta fase).

  **Prompt:**
  ```
  Implemente as consequencias contabeis do resgate (ARQUITETURA 7.3 ramo "se resgate",
  Fluxo 3 da 8.4), dentro do handler de TradeRegistered que ja existe.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  VOCABULARIO — copie do F3, nao reescreva. `operacao = resgate` da 5.1 vira
  `tipo = venda` no livro (V2); `resgate` no enum do livro e VENCIMENTO, que e do F9.
  As chaves sao as da V3:
    ir:<tradeId> · iof:<tradeId> · aliq:<tradeId> ·
    liq:<tradeId>:aliq · liq:<tradeId>:brl        (a liquidacao tem DUAS chaves porque
                                                   tem DUAS PERNAS e movimentos tem UM
                                                   instrumento_id por linha; com uma
                                                   chave so elas COLIDEM no UNIQUE)
    est:ir:<tradeIdEstorno> · est:iof:... · est:aliq:... ·
    est:liq:<tradeIdEstorno>:aliq · est:liq:<tradeIdEstorno>:brl
  caixa:BRL e caixa:a_liquidar sao ids LOCAIS com preco 1,000000 POR DEFINICAO (V4).

  ORDEM E FORMA — a tabela de LINHAS DERIVADAS da V2 do F3, copiada, nao reinventada.
  Resgate de q unidades do instrumento X por Y bruto, IR t, IOF f:
    <tradeId>            venda        X                 -q        Y      (dataEvento)
    ir:<tradeId>         ir_retido    caixa:a_liquidar  -t        t      (dataEvento)
    iof:<tradeId>        iof          caixa:a_liquidar  -f        f      (dataEvento)
    aliq:<tradeId>       a_liquidar   caixa:a_liquidar  +Y        Y      (dataEvento)
    liq:<tradeId>:aliq   liquidacao   caixa:a_liquidar  -(Y-t-f)  Y-t-f  (D+1 util, JOB)
    liq:<tradeId>:brl    liquidacao   caixa:BRL         +(Y-t-f)  Y-t-f  (D+1 util, JOB)
  (colunas: ref_externa · tipo · instrumento_id · qtd_delta · valor_financeiro · data)
  qtd_delta CARREGA O SINAL; valor_financeiro e MAGNITUDE BRUTA nao negativa.

  A BASE DO IR E O GANHO DA ALIENACAO, NAO O PRECO MEDIO:
      base = valorFinanceiro_da_venda - preco_medio x quantidade,  com PISO EM ZERO.
  "Base = preco medio DO LIVRO" (7.3) e ELIPSE — o preco medio e o INSUMO da base. Lida
  literalmente, ela manda cobrar ~22,5% DO CUSTO INTEIRO em vez do lucro, numa tabela sem
  UPDATE. A EVIDENCIA ESTA NO SIMULADOR QUE ESTE PROMPT MANDA PORTAR (../tesouro-direto-api),
  e e literal (conferida no arquivo, nao citada de memoria):
  src/TesouroDireto.Application/Tributos/TributosPadrao.cs linhas 46 e 71 declaram os DOIS
  tributos como BaseCalculo.Rendimento (nao preco);
  src/TesouroDireto.Domain/Simulador/SimuladorService.cs linha 138 faz
  rendimentoBruto = Math.Round(valorBruto - input.ValorInvestido, 2);
  TributosPadrao.cs:51-57 traz o IR como TipoCalculo.FaixaPorDias com as faixas (0,180)
  (181,360) (361,720) (721,999_999), e o IOF como TabelaDiaria sobre o vetor IofAliquotas
  de 29 aliquotas diarias (linha 8 em diante). Portar o motor com a base errada e conferir
  uma coisa contra outra.
  BASE NEGATIVA (venda com PREJUIZO): piso em zero, IR = 0 e IOF = 0, e as linhas ir: e
  iof: NAO EXISTEM — nao grave zero, exatamente como ja vale para o IOF ausente. E
  PREJUIZO NAO GERA CREDITO NEM COMPENSACAO nesta fase: e ausencia DECIDIDA, porque
  compensacao de perdas exige historico fiscal proprio que nada neste roadmap modela. NAO
  a improvise dentro do calculo.
  COMPRA RETROATIVA que muda a base (ou o prazo) de uma venda ja tributada: mesma familia
  do preco_medio provisorio da decisao 0 — GRAVA, SINALIZA, e o conserto e ESTORNO do
  resgate e relancamento. NUNCA UPDATE (a trigger barra).

  PARE AQUI SE A PENDENCIA DO `prazo` AINDA ESTIVER ABERTA. A aliquota depende de DIAS
  CORRIDOS DESDE A AQUISICAO, e uma posicao construida por N compras em datas diferentes
  NAO TEM UMA data de aquisicao — a V1 do F3 guardou CUSTO MEDIO, nao lotes. FIFO, data
  media, lote mais antigo ou mais novo mudam a aliquota entre 22,5% e 15%: 50% DE
  DIFERENCA NO IMPOSTO, gravado numa tabela sem UPDATE. A decisao e do DONO (amarra
  modelagem por lotes contra custo medio) e esta escrita na prosa desta fase como
  PENDENCIA BLOQUEANTE, com as quatro opcoes e o custo de cada uma. NAO ESCOLHA VOCE
  ENQUANTO ESCREVE O TESTE: e assim que o Pronto (c) — "o dia exato da virada de faixa" —
  passa por vacuidade, certificando o que voce acabou de decidir.
  a_liquidar E BRUTO e os TRIBUTOS DEBITAM caixa:a_liquidar: IR e IOF sao retidos NA
  FONTE, entao o direito a receber nasce cheio e e reduzido pelas proprias linhas de
  tributo. O saldo a receber e uma SOMA DE LINHAS DO LIVRO (+Y -t -f) — nao um numero
  calculado na escrita —, e e exatamente esse saldo que o job liquida em D+1.
  Tributos sao MOVIMENTOS PROPRIOS, nunca desconto embutido no valor da venda (7.1) — e
  o que deixa o extrato conferivel contra o da corretora.
  A ARITMETICA QUE ISSO TEM QUE FECHAR (esta escrita na V2 do F3, e os DOIS PRIMEIROS
  ITENS SAO INDEPENDENTES DE PRECO DE PROPOSITO):
    (1) Σ qtd_delta das linhas do fato em caixa:a_liquidar = Y - t - f depois de D, e
        ZERO depois da liquidacao;
    (2) Σ qtd_delta(caixa:a_liquidar) + Σ qtd_delta(caixa:BRL) INALTERADO pelas duas
        pernas — transferencia pura, logo A LIQUIDACAO SOZINHA NAO MUDA O PATRIMONIO.
  NAO ESCREVA "entre D-1 e D o patrimonio cai EXATAMENTE por t+f" COMO INVARIANTE, como
  dizia a versao anterior deste prompt: o snapshot de D-1 usa preco(ultimo <= D-1) e o de
  D usa preco(ultimo <= D), entao a queda so vale t+f SE O PRECO NAO SE MOVER — e preco
  se movendo e o motivo de o sistema existir. Uma implementacao CORRETA reprovaria esse
  criterio, e a unica forma de deixa-lo verde seria um fixture com preco congelado e
  posicao inteira liquidada, que nao distingue implementacao nenhuma. Essa igualdade e
  HIPOTESE DO FIXTURE: escreva "com o preco do titulo congelado entre D-1 e D" no
  fixture, e deixe o INVARIANTE com os itens (1) e (2).

  PORTE, NAO REINVENCAO: as regras de IR e IOF ja existem validadas no simulador da TD
  API (ARQUITETURA 11). Porte-as e confira contra ele — reimplementar a partir da prosa
  produz divergencia com o extrato real da corretora. Faixas e tabela regressiva num
  DONO SO, com teste dos casos de FRONTEIRA (o dia exato da virada de faixa, o dia 30 do
  IOF), nao so o caso do meio. Arredondamento SEMPRE no financeiro (2 casas), NUNCA na
  quantidade (PADROES 10.25).

  DECISOES JA TOMADAS:
  0. TRIBUTAR A PARTIR DE UM preco_medio PROVISORIO: GRAVA, SINALIZA, E O CONSERTO E
     ESTORNO — NUNCA UPDATE. O preco_medio DO LIVRO e o INSUMO da base (a base e o GANHO,
     ver acima), e a V1 do F3
     declara as tres colunas PROVISORIAS enquanto a quantidade for negativa. Isso
     tranquiliza para posicao_corrente, que se refaz por dobra, e NAO VALE PARA O LIVRO:
     ir_retido e iof gravados a partir de um pm provisorio ficam em tabela append-only e
     SO SAEM POR ESTORNO. O resgate ENTRA DO MESMO JEITO (recusar reintroduziria aqui a
     validacao que a ADR-10 tirou de Operacoes — e a camada 3 da 6.1), o tributo usa o
     preco_medio que existe, e o caso E SINALIZADO (metrica + log estruturado + alerta)
     como "resgate tributado sobre preco medio provisorio", distinto do sinal de posicao
     negativa do F4 porque aqui JA HOUVE LINHA DE TRIBUTO GRAVADA. O reparo e o que esta
     fase ja implementa: estorno do resgate (revertendo est:ir:..., est:iof:...) e
     relancamento DEPOIS de a compra faltante entrar. NAO estacione o resgate esperando a
     posicao ficar positiva (o parking e para o que nao se sabe escriturar) e NAO
     recalcule o tributo por UPDATE (edicao destrutiva de fato, antipadrao nominal da
     secao 9, e impossivel no livro por trigger).
  1. Calendario de dias uteis: TABELA DE CONFIGURACAO PROPRIA neste banco, populada por
     migration/seed. NAO consulte a TD API nem o Hub no caminho de escrita (poria fonte
     externa dentro do processamento de um evento ja aceito; a 13.4 poe o endpoint de
     feriados como melhoria FUTURA e a ADR-12 proibe ler o banco alheio). E NAO derive
     "dia util" de "dia em que houve preco": a 7.1 proibe misturar esse criterio com o
     de "repete o preco". Este calendario tambem serve o F7.
  2. A liquidacao e gravada EM D+1 UTIL, POR UM JOB — NAO antecipada no instante do
     resgate. ISTO E UM DESVIO POR CORRECAO DO TEXTO LITERAL DA 7.3 ("+ a_liquidar ->
     liquidacao (D+1 util)" DENTRO do handler) — REGISTRE-O e grave na memoria ao fechar
     a fase, igual ao ref_externa NOT NULL do F3. O apoio e a 8.4, que poe a liquidacao
     em outro momento com todas as letras ("Note over C: dia D+1 util"): a 7.3 e a linha
     do tempo comprimida, o Fluxo 3 e ela esticada. Sem o rotulo voce vai encontrar a
     contradicao sozinho e tender a seguir a ARQUITETURA.
     Gravar em D uma linha com data_evento no FUTURO poe no livro um fato que
     ainda nao aconteceu, e como posicao_corrente e Σ qtd_delta do livro SEM RECORTE DE
     DATA (7.1), a projecao mostraria JA EM D o dinheiro em caixa:BRL e o
     caixa:a_liquidar zerado — o limbo D->D+1 sumiria da posicao corrente, contra o
     Pronto desta fase e contra o invariante I4 que o F4 prova. Redefinir
     posicao_corrente como "Σ qtd_delta com data_evento <= hoje" NAO e a saida: tiraria
     dela a natureza de DOBRA PURA (passaria a depender do relogio), exigiria um job de
     virada diaria de qualquer modo (o handler do F4 nao teria como aplicar a linha
     futura na transacao), tornaria a reconciliacao do F7 dependente do relogio, e faria
     o extrato de movimentacao do F8 (SELECT ordenado por data_evento) mostrar hoje ao
     cliente uma liquidacao que nao ocorreu. A 8.4 poe a liquidacao em outro momento,
     com todas as letras ("Note over C: dia D+1 util").
     O JOB: IHostedService idempotente que seleciona as linhas a_liquidar cuja data de
     liquidacao (D+1 util, derivada do data_evento da propria linha pelo calendario da
     decisao 1 — deterministica) e <= hoje, QUE NAO FORAM REVERTIDAS por um ajuste, e
     que ainda nao tem a perna liq:<fato>:brl; insere as DUAS pernas. Rodar duas vezes e
     no-op pela ref_externa. Parada CLASSIFICADA (PADROES 10.31): COMPLETUDE = varri
     todas as vencidas e cada uma tem as duas pernas; LIMITE = teto de linhas por ciclo
     -> FALHA, nunca sucesso parcial. A MESMA CONSULTA e a guarda permanente
     ("a_liquidar vencida sem liq:<fato>:brl" -> metrica + alerta), entao um job que
     atrasa APONTA PARA SI MESMO. Este job NAO e o worker da 7.4 (esse e do F7).
     O FILTRO "que nao foram revertidas" NAO E DETALHE, E ELE OLHA DUAS LINHAS: nem a
     linha aliq:<fato> nem o MOVIMENTO PRINCIPAL do fato podem estar revertidos. Se o
     estorno chegar em D, antes de a liquidacao existir, liquidar um a_liquidar ja
     revertido creditaria dinheiro em caixa:BRL de uma operacao que nao existe mais, numa
     tabela sem DELETE. A SEGUNDA METADE NAO E REDUNDANCIA: na janela F4->F5 o estorno
     produziu `ajuste` SO SOBRE O PRINCIPAL (aliq: nao existia — excecao declarada a I13
     do F4), entao o aliq: criado pelo backfill desta fase nasce VENCIDO e NAO REVERTIDO, e
     um filtro que olhasse so a propria linha mandaria voce creditar caixa:BRL de um
     resgate que nao existe mais. E ENQUANTO A LINHA aliq:<fato> NAO EXISTE, O FATO NAO TEM
     PONTO DE SERIALIZACAO: o SELECT ... FOR UPDATE abaixo trava uma linha que ainda nao
     nasceu — e por isso a metade (i) do backfill roda com o JOB PARADO, e nao so com o
     filtro certo.
     E O FILTRO SOZINHO NAO BASTA, PORQUE O JOB E O CONSUMIDOR SAO CONCORRENTES: o job e
     um IHostedService com thread propria e o handler de estorno roda no BackgroundService
     do consumidor — dois processos no mesmo container, e a decisao (c) (ciclo CURTO)
     torna a janela entre o SELECT e o INSERT do job FREQUENTE. Dois desfechos, os dois
     permanentes num livro sem DELETE: (i) o estorno commita ENTRE o SELECT e o INSERT e
     o job liquida um a_liquidar JA REVERTIDO; (ii) o job insere ENTRE a decisao do
     handler ("nao ha liq a reverter") e a gravacao do conjunto est:, e o est:liq: nunca
     e escrito — o UNIQUE (ref_estorno) nao ajuda, porque nada foi gravado.
     REGRA: A LINHA aliq:<fato> E O PONTO DE SERIALIZACAO DO FATO. O job E o handler de
     estorno a travam com SELECT ... FOR UPDATE, NA MESMA TRANSACAO em que gravam.
     Qualquer que seja a ordem, o resultado e correto: job primeiro -> o handler acha as
     duas pernas e as reverte junto; estorno primeiro -> o job acha o a_liquidar revertido
     e nao liquida. REJEITADO: SERIALIZABLE (resolve, mas exige politica de retry sob
     falha de serializacao em dois caminhos que nao a tem); REJEITADO: rodar o job dentro
     do laco serial do consumidor (amarraria a cadencia do job ao fluxo de mensagens, e
     ele precisa rodar com a fila parada).
     O TESTE SEQUENCIAL ("revertido antes de liquidar nao e liquidado") PASSA COM A
     CORRIDA INTACTA: prove por INJECAO, com barreira entre o SELECT e o INSERT do job.
     O VALOR LIQUIDADO E O SALDO DO FATO em caixa:a_liquidar (soma dos qtd_delta das
     linhas daquele fato: +Y -t -f), nao o valor da linha aliq: sozinha — a_liquidar e
     BRUTO por decisao da V2.
     TRES CONDICOES DO JOB, todas de dado irreversivel:
     (a) CATCH-UP depois de dias fora do ar grava data_evento NO PASSADO, e a 7.3 diz que
         movimento com data_evento < hoje DISPARA recalcular. "O job nao recalcula nada"
         deixaria os snapshots dos dias perdidos com caixa:a_liquidar PARA SEMPRE. Entao:
         quando a linha inserida for RETROATIVA, o job ENFILEIRA recalcular(cliente,
         instrumento, desde=data_liquidacao) — o gatilho 1 do worker do F7, sem caminho
         novo. Antes do F7 nao ha snapshot e nao ha o que recalcular; a partir dele, o
         job enfileira. Isso e dependencia DECLARADA, nao descoberta.
         O ENFILEIRAMENTO E CAMINHO RAPIDO, NUNCA A GARANTIA. NAO leia "enfileira" como
         "Channel<T> em memoria e pronto": um restart entre o commit das duas pernas e o
         recalculo PERDE A CHAMADA PARA SEMPRE — as pernas ja estao no livro, o eod.ready
         seguinte percorre [U_anterior + 1, D] e NAO VOLTA aos dias ja materializados, e o
         resultado e o caso (a) acima: snapshots com caixa:a_liquidar PARA SEMPRE. Isso
         violaria o P2 da ARQUITETURA ("crash em qualquer ponto se resolve na proxima
         execucao"). A GARANTIA E A VARREDURA DE DEFASAGEM DE SNAPSHOT DO F7, DERIVADA DO
         DADO (registrado_em do movimento contra calculado_em do snapshot vigente daquele
         dia), que refaz o que a fila perdeu sem estado de controle nenhum.
     (b) CALENDARIO EXAURIDO E FALHA ALTA, nunca modo degradado. Feriado e fim de semana
         funcionam por construcao (nada vence). O que mata e a data FORA DO HORIZONTE DO
         SEED: FALHE ALTO e alerte, JAMAIS caia em "+1 dia corrido" — esse erro so
         aparece meses depois, num movimento ja gravado em tabela append-only. A guarda
         "alerta quando o calendario acaba em menos de N dias" e desta fase.
     (c) O INTERVALO DO CICLO e fixado em relacao ao eod.ready do MESMO dia: ciclo CURTO
         (ordem de minutos), para que a liquidacao de D+1 ja esteja no livro quando o
         eod.ready(D+1) chegar. Uma vez por dia e tarde demais faz o snapshot de D+1
         nascer sem caixa:BRL e cair no caso (a) TODO DIA, por desenho. O custo e
         irrisorio: e O(a_liquidar em aberto).

  3. ESTORNO DE UM RESGATE REVERTE TODOS OS DERIVADOS, POR INSERT. O F4 grava so a linha
     `ajuste` do movimento principal; o resgate original produziu aqui ir_retido, iof,
     a_liquidar e (se ja liquidado) as duas pernas da liquidacao. Sem reverter, o livro
     fica com tributo retido e dinheiro creditado de uma operacao que nao existe mais —
     e, sendo append-only, PARA SEMPRE.
     Cada linha de reversao: tipo = 'ajuste', ref_externa da familia `est:` (V3),
     ref_estorno apontando para A LINHA REVERTIDA, qtd_delta e valor SIMETRICOS a ela.
     Tudo por INSERT — nenhum UPDATE (a trigger barraria). O UNIQUE (ref_estorno) WHERE
     NOT NULL do F3 garante que cada linha e revertida no maximo uma vez.
     COBRIR OS TRES CASOS, porque o conjunto varia: estorno ANTES da liquidacao (nao ha
     liq a reverter, e o a_liquidar revertido sai do escopo do job); estorno DEPOIS da
     liquidacao (as duas pernas sao revertidas); resgate SEM IOF (prazo >= 30 dias — nao
     existe iof: a reverter, e o handler nao pode tentar).

  BACKFILL da janela F4->F5, e ele tem DUAS metades: (i) resgates ja gravados sem
  tributo/a_liquidar recebem as linhas faltantes; (ii) estornos ja gravados sem o
  conjunto `est:` recebem a reversao. Tudo por INSERT (append), NUNCA por UPDATE,
  idempotente pela ref_externa de cada derivado.
  TRES CONDICOES DE EXECUCAO, E NENHUMA E OPCIONAL — sem elas o REPARO credita dinheiro de
  operacoes que nao existem mais. O cenario nasce do desenho: um estorno chegado NA JANELA
  F4->F5 gravou `ajuste` sobre o movimento PRINCIPAL e nada mais, porque ir:, iof: e aliq:
  nao existiam.
    (1) A METADE (i) PULA resgates cujo MOVIMENTO PRINCIPAL esteja revertido. Sem isso ela
        cria ir:, iof: e aliq: para um resgate que NAO EXISTE MAIS, e o aliq: nasce JA
        VENCIDO (data passada) e NAO REVERTIDO — e o job de liquidacao, que e de ciclo
        curto e sobe no MESMO DEPLOY, credita caixa:BRL.
    (2) A ORDEM E (i) E DEPOIS (ii), e ela fica escrita: o ref_estorno de cada `est:`
        precisa da linha alvo ja gravada. A janela entre as duas metades e aberta PELO
        DESENHO — e nela que existem aliq: recem-criados ainda sem est:aliq:.
    (3) RODE O BACKFILL COM O JOB DE LIQUIDACAO PARADO, e o filtro do job olha o FATO
        PRINCIPAL (nem a linha aliq: nem o movimento principal revertidos). Sao as duas
        metades da mesma protecao: sem parar o job ele varre a janela do item (2) enquanto
        ela existe; sem o filtro robusto ele volta a errar na primeira reentrega depois de
        religado.
  O PRONTO (h) SOZINHO NAO PEGA NADA DISSO: ele confere idempotencia e "tres consultas de
  guarda devolvendo 0" AO FINAL, e passa verde depois de o dano estar gravado. E as consultas que os encontram
  ("resgate sem linha ir:<tradeId>" e "ajuste sobre resgate sem conjunto est:") viram
  GUARDA PERMANENTE com metrica e alerta — nao script de uma vez: o que elas detectam
  pode reaparecer por bug do handler depois.

  NAO ENTRA: cupom e vencimento (F9), snapshot e o worker da 7.4 (F7), extrato (F8).

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) um resgate produzindo o conjunto **completo** de
  movimentos — venda, `ir_retido` **quando cabe**, `iof` **quando cabe**, `a_liquidar` em D
  e, depois de o job rodar, **as duas pernas** da `liquidacao` em D+1 útil —, *e "quando
  cabe" tem duas condições, não uma: `iof:` só existe com prazo < 30 dias, e **`ir:` e `iof:`
  não existem quando a base é zero ou negativa** (venda com prejuízo, decisão da base). O
  fixture da permissiva fixa o caso de **base positiva e prazo < 30 dias**, que é o conjunto
  máximo; os outros dois são asserções próprias, com contagem menor,* cada um com `ref_externa`
  própria e **nenhuma colisão**; `posicao_corrente` mostrando `caixa:a_liquidar` em D e
  `caixa:BRL` em D+1, **com I4 valendo literalmente nos dois dias** (é essa combinação que
  a decisão da liquidação por job torna verdadeira, e que a versão antecipada tornava
  infalseável). (**Estrita**)
  (a) o `valor_financeiro` do movimento de venda é o **bruto** `Y`, e o líquido só aparece
  **somando as linhas**: `Σ qtd_delta` de `caixa:a_liquidar` naquele fato = `Y − IR − IOF`,
  que é exatamente o valor que o job liquida em D+1. É a prova de que "tributos são
  movimentos próprios" virou dado e não intenção — se o IR estivesse embutido, a soma bateria
  sem as linhas de tributo, e é isso que o teste tem de reprovar. **E os dois invariantes
  independentes de preço da V2, executados:** `Σ qtd_delta` de `caixa:a_liquidar` do fato é
  `Y − IR − IOF` depois de D e **zero** depois da liquidação; e
  `Σ qtd_delta(caixa:a_liquidar) + Σ qtd_delta(caixa:BRL)` é **inalterado** pelas duas
  pernas — transferência pura, logo a liquidação sozinha não muda o patrimônio. *A versão
  anterior deste Pronto exigia "entre D-1 e D o patrimônio cai exatamente por IR + IOF; entre
  D e D+1 é constante" — as duas só valem a preço parado, e uma implementação **correta**
  reprova. A queda por `IR + IOF` continua sendo teste, mas com a hipótese "preço do título
  congelado entre D-1 e D" escrita **no fixture**, nunca no invariante;*
  (b) reprocessamento do mesmo evento não duplica **nenhum** dos derivados (teste por
  movimento, não só pelo principal), e o **job rodado duas vezes** faz zero INSERT na
  segunda;
  (c) D+1 cai em dia útil, provado contra **sexta-feira, sábado e véspera de feriado**, não
  contra uma terça; **e a data fora do horizonte do calendário FALHA ALTO**, com alerta —
  nunca "+1 dia corrido", que é o erro que só aparece meses depois, já gravado. **E as
  fronteiras fiscais — o dia exato da virada de faixa e o dia 30 do IOF — só são
  exercitáveis DEPOIS de a PENDÊNCIA BLOQUEANTE do `prazo` estar decidida:** enquanto ela
  estiver aberta, este critério passa por vacuidade, porque o executor define `prazo`
  enquanto escreve o teste e o teste certifica a definição que ele acabou de escolher. O
  fixture da fronteira nomeia, no próprio nome do teste, **qual** definição de `prazo` está
  em vigor;
  (c2) **o catch-up é retroativo e se declara como tal:** um `a_liquidar` vencido há três
  dias, liquidado agora, grava `data_evento` **na data de liquidação** (passado) e
  **enfileira `recalcular(cliente, instrumento, desde=data_liquidacao)`** — provado por
  asserção sobre a fila de recálculo, não por inspeção visual. Antes do F7 a asserção é
  sobre a chamada; depois dele, sobre o snapshot reversionado. **E a metade que importa é a
  do CRASH, porque é ela que separa "enfileirou" de "chegou":** derrubado o processo
  **entre o commit das duas pernas e o recálculo** (por injeção), a chamada enfileirada é
  perdida — e, **na próxima execução**, a *varredura de defasagem de snapshot* do F7
  reversiona os dias afetados **sem que ninguém reenfileire nada**. *A metade negativa
  isolada não serve: "o snapshot ficou errado depois do crash" é verdade tanto na
  implementação certa quanto na errada, e o que se está provando é a **recuperação**.
  Enquanto o F7 não existir, este critério fica registrado como **pendência de prova** desta
  alínea, e é o F7 que o executa — não se apaga o critério, declara-se onde ele roda.*
  (d) não existe movimento de tributo ou de liquidação **sem** o resgate que o originou
  (varredura do livro);
  (d2) **um resgate sobre posição com `preco_medio` provisório (`quantidade` negativa) grava
  o conjunto completo E emite o sinal** *"resgate tributado sobre preço médio provisório"* —
  as duas metades: a linha de tributo **existe** (não foi recusada nem estacionada) e o
  alerta **disparou**. **Com controle negativo:** o mesmo resgate sobre posição positiva
  **não** dispara o sinal. E o reparo conferido pelo caminho desta fase — estorno do resgate
  revertendo `est:ir:…`/`est:iof:…` —, **nunca** por `UPDATE` na linha de tributo;
  (e) **estorno de um resgate nos três cenários** — antes da liquidação, depois da
  liquidação, e sem IOF: depois do estorno, **AS TRÊS COLUNAS de `posicao_corrente` voltam
  ao estado anterior** para **cada** instrumento tocado (o título, `caixa:BRL` e
  `caixa:a_liquidar`), e **não sobra nenhuma linha derivada sem contrapartida**, provado
  por varredura e não pelo caminho feliz. **O valor afirmado para as linhas de caixa com
  `quantidade = 0` é `preco_medio = 0` e `custo_total = 0`, NÃO `1,000000`** — é a regra de
  fronteira da V1, que **precede** a V4 (escrito lá, e escrito aqui porque é aqui que a
  asserção é redigida). Sem fixar qual dos dois valores o teste afirma, o handler incremental
  desta fase e a reconciliação do F7 podem escolher regras diferentes, e a reconciliação
  passa a alertar em toda operação normal; **O cenário obrigatório é `compra → venda a preço
  DIFERENTE do custo → estorno`**, com números: `compra 10 @ 100`, `venda 4 por 600`,
  estorno → `quantidade 10, custo_total 1000, preco_medio 100`. *A versão anterior deste
  Pronto dizia "Σ por instrumento volta ao estado anterior", e **Σ é só `qtd_delta`**: ele
  fica verde com `preco_medio` em 120 e `custo_total` em 1200, que é exatamente o defeito
  da dobra ingênua do `ajuste` (V1 do F3). Preço igual ao custo também não serve — nesse
  fixture a regra certa e a errada dão o mesmo número;*
  (f) `a_liquidar` **revertido antes de liquidar não é liquidado pelo job** — controle
  positivo: um `a_liquidar` não revertido, vencido no mesmo ciclo, **é**;
  (f2) **a corrida job × handler de estorno, provada por INJEÇÃO nos dois sentidos** — o
  teste sequencial de (f) passa com a corrida intacta, então ele não substitui esta prova:
  com uma barreira entre o `SELECT` e o `INSERT` do job, um estorno **commitado dentro da
  janela** não produz liquidação; e um job que insere dentro da janela do handler resulta
  no conjunto `est:liq:` gravado, não omitido. É o `SELECT … FOR UPDATE` sobre a linha
  `aliq:<fato>` que se está provando, e sem ele os dois casos gravam linha eterna;
  (g) a parada do job por **limite** devolve falha, não sucesso com conjunto parcial;
  (h) o backfill rodado duas vezes com resultado idêntico, **nas duas metades** (resgate
  sem derivados e estorno sem reversão), e as três consultas de guarda devolvendo 0
  pendências. **E as TRÊS condições de execução, cada uma com sua asserção, porque este
  critério sozinho passa verde depois de o dano estar gravado:** (h1) um resgate da janela
  F4→F5 **com o principal revertido** não recebe `ir:`, `iof:` nem `aliq:` da metade (i) —
  **com controle positivo**: um resgate igual, **não** revertido, recebe as três; (h2) a
  ordem (i)→(ii) afirmada por asserção sobre o resultado, não por leitura do código — depois
  da passagem completa, **todo** `aliq:` criado pela metade (i) sobre fato revertido não
  existe, e todo `est:aliq:` da metade (ii) aponta para uma linha que a (i) gravou; (h3) o
  job de liquidação **religado depois do backfill** não credita `caixa:BRL` de nenhum fato
  cujo movimento principal esteja revertido — é a prova do filtro robusto, e ela é
  independente de (h1), porque (h1) prova que a linha não nasceu e (h3) prova que, se
  nascesse, o job não a liquidaria;
  (i) as alíquotas conferidas contra o simulador da TD API nos casos de fronteira —
  **e a conferência é sobre a BASE também, não só sobre a alíquota**: o caso conferido tem
  `valorFinanceiro`, `preco_medio` e `quantidade` tais que `base = valorFinanceiro − pm ×
  quantidade` seja **diferente** de `pm × quantidade`, senão as duas fórmulas dão o mesmo
  número e o teste não distingue a implementação certa da que cobra imposto sobre o custo.
  *Limite declarado, e ele decorre da PENDÊNCIA do `prazo`:* o `SimulacaoInput`
  (`src/TesouroDireto.Domain/Simulador/SimulacaoInput.cs`) modela **uma aplicação** — um
  `DataCompra` e um `ValorInvestido` escalares — e **não sabe responder**
  sobre uma posição de custo médio com N compras — a conferência só é executável com um
  fixture de **compra única**, e é assim que ela tem de estar escrita. Estender a conferência
  a posições de N compras depende de a pendência do `prazo` ter sido decidida, e o critério
  diz isso em vez de fingir cobertura.

- [ ] **F6** — `prices.*` e o bootstrap REST do Hub: a primeira projeção inteiramente
  descartável. **Dependência externa nova: o Hub publicando `prices.*` e respondendo
  `GET /prices/asof`.**

  Handler de `PriceObserved` fazendo upsert em `preco_atual` **e no `historico_precos`**
  (a tabela nasce no F3, com a chave inteira), deduplicado pela chave natural
  `(instrumentoId, dataRef, campo, fonte, revisao)`; client HTTP do Hub para bootstrap via
  `GET {hub}/prices/asof`, com timeout, Polly e conversão de exceção em `Result` na
  Infrastructure; **log destacado de toda `revisao > 0`** (§12: correções são raras e
  merecem visibilidade). Drenagem do `custodia.parked` do motivo
  `tipo_nao_tratado_prices`.

  **Por que depois do livro:** `preco_atual` e o histórico local são projeção descartável
  com **duas** fontes de reconstrução (os eventos e o REST do Hub). Errar aqui custa um
  reprocessamento; errar no livro custa uma linha eterna. É a diferença que põe esta fase
  depois do F4 e do F5, mesmo a §9 item 3 citando `PriceObserved` junto de `TradeRegistered`.

  **Decisões desta fase:**

  - **Histórico local SIM** (a §7.1 chama de recomendado). O recálculo do F7 não pode
    depender de o Hub estar online. *Rejeitado:* consultar sempre `/prices/asof` — poria
    dependência síncrona de outro serviço dentro do caminho de recálculo, e o Fluxo 5
    recalcula **todos** os clientes posicionados. **A DDL é do F3, não desta fase:** a
    decisão é aqui, a tabela nasce lá, porque no F3 não há escritor e aqui já há — que é o
    argumento que o próprio F3 usa a favor de si mesmo.
  - **Monotonicidade de `preco_atual` — a política de fora de ordem da PROJEÇÃO, que o
    rascunho anterior não tinha.** `preco_atual` é, pela §7.1, o **último** preço do
    instrumento, e dedupe por chave natural dá idempotência, **não ordenação**: um
    `PriceObserved` com `dataRef` mais antiga (redelivery, dreno do parking acumulado desde
    o F2, bootstrap rodando junto com o push) passa pelo dedupe — é chave natural diferente
    — e o upsert **regride a projeção para um preço velho**. O mesmo com `revisao`: uma
    revisão 0 reentregue depois da revisão 1 do mesmo dia sobrescreveria a correção. Regra:
    `preco_atual` só é sobrescrito se `(dataRef, revisao)` do evento for **≥** o armazenado
    **para aquele instrumento** — e é **por instrumento**, não por `(instrumento, campo)`,
    porque a PK da §7.1 é `instrumento_id` e a linha guarda **só o `campoPosicao`** (decidido
    na fase do schema, F3). Evento de campo que **não** é o `campoPosicao` do item não toca
    `preco_atual`: vai só para o histórico. O **`historico_precos` recebe TODAS as
    revisões e TODOS os campos**, sempre. Escrito nesta fase porque é aqui que se drena um parking com **semanas** de
    mensagens fora de ordem — o pior cenário possível para essa lacuna, e ele acontece no
    primeiro boot da fase. *Rejeitado:* confiar na ordem de entrega do broker — a §5 diz
    que "redelivery é esperada e **inócua** (dedupe por chave natural)", e isso é verdade
    para o **livro**, não para uma projeção de "último".
  - **O campo que marca posição vem MARCADO no dado: `campoPosicao`** (§4.5). O consumidor
    faz `item.campos[item.campoPosicao]` e **não** duplica a tabela de classes nem
    hard-codeia `pu_venda`. É a §10.32 aplicada: a regra "no TD o que marca posição é
    `pu_venda`, nunca `pu_compra`" mora no Hub, e a §4.5 explica por que um `campo=` com
    default seria pior ("quando `acao:PETR4` chegar, o default ou mente ou muda em silêncio
    para quem omitiu o parâmetro"). *Rejeitado:* gravar `campo = 'pu_venda'` fixo — funciona
    hoje e apodrece na fase 2, em silêncio, exatamente como a §10.32 descreve.
  - **GET condicional contra o Hub: SIM**, ao contrário do `operacoes`. A §10.30 registra a
    ausência lá porque a URL carregava o **termo digitado** e cada termo novo era um miss
    garantido. Aqui o bootstrap sonda **URL fixa em ciclo**, que é o caso do
    `../hub-precos/src/Hub.Infrastructure/TdApi/TdApiClient.cs` — o molde volta a valer. E a
    própria §10.30 diz o que fazer então: o store de ETag nasce **com prazo, teto e
    evicção**, as três coisas que a §10.16 cobra e cuja ausência deixou o hub em 304
    permanente depois de um banco zerado por fora. *Rejeitado:* portar o
    `ConditionalGetStore` como está no molde, sem prazo.
  - **Sem last-known-good**, e a ausência é decidida por motivo **diferente** do
    `operacoes`: lá o cache servia também a validação de uma escrita (§10.29); aqui não há
    escrita de negócio nenhuma. O que proíbe o LKG aqui é outra coisa — servir preço velho
    como se fosse fresco alimentaria snapshot no F7, e snapshot é **documento que o cliente
    vê** (P2). Falhar e reprocessar é honesto; preencher com o último conhecido é
    forward-fill materializado como observação, que a §9 dos padrões proíbe nominalmente.
    Registrar a ausência com o motivo, para ninguém "consertá-la" portando o cache do
    `operacoes` por fidelidade.
  - **Negação explícita do onboarding:** a resolução nome→id da §7.2 acontece **na borda,
    em Operações** (ADR-10). `Hub__*` serve **bootstrap da projeção de preços e o `asof` do
    worker**, e mais nada. Não existe `HubCatalogoClient` de validação nesta casa.
  - **`revisao > 0` MARCA o fato no dado** (a coluna `revisao` no histórico) para o worker
    do F7 consumir. *Rejeitado:* o F7 comparar valores para descobrir sozinho que houve
    correção — §10.32: quem sabe a diferença marca no dado, e toda re-derivação apodrece em
    silêncio no primeiro caso novo.

  **O BOOTSTRAP LÊ `dataRef` POR CAMPO, NUNCA `item.dataRef` — e esta é a linha mais fácil
  de errar da fase inteira.** A §4.5 é explícita: *"`dataRef` por **CAMPO**, não por item …
  basta um dia em que a fonte não publique `taxa_compra` para os campos dessincronizarem.
  Resolver a data uma vez por item e devolver só os campos daquela data faria um campo
  **sumir em silêncio**"*; e o `dataRef` **do item** é, por definição, o **maior** entre os
  campos — resumo de frescura, não a data de nada. Logo: cada linha de `historico_precos`
  usa `campos[<campo>].dataRef`, e `preco_atual` usa
  `campos[campoPosicao].dataRef`. Usar `item.dataRef` para todos grava **a data errada**
  para os campos atrasados, em silêncio, numa tabela que alimenta snapshot — que é documento
  que o cliente vê (P2). É literalmente o erro que o `LEIA-ME-KIT` registra em **"Escrever
  contrato assumindo que os campos andam juntos"**, escrito **sobre este payload**.

  **O BOOTSTRAP: o `/prices/asof` NÃO é paginado, e o laço que existe é outro.**

  A §4.5 define `GET /prices/asof?date=D[&instruments=a,b,c]` devolvendo `items[]`. **Não
  há `page`, não há `pageSize`, não há `X-Total-Count`.** O rascunho anterior mandava
  testar boundary de `pageSize` e validar `X-Total-Count` contra este contrato: Pronto
  inexecutável, e um empurrão para o executor **inventar paginação em contrato de
  terceiro**. Pior, a §10.31 ficava aplicada ao laço errado, e o laço real ficava sem
  classificação nenhuma. Os laços reais são dois, e cada condição de parada é classificada:

  **Escopo — "quais instrumentos pedir" — decidido aqui, porque sem isso não há laço.** O
  conjunto é derivado do **LIVRO**: os `instrumento_id` distintos de `movimentos`,
  **excluído o namespace `caixa:`** (V4 do F3 — preço 1 por definição, não existe no Hub, e
  pedi-lo devolveria `motivo: instrumento_desconhecido`, que é sinal de defeito e não deve
  ser fabricado por nós). *Rejeitado:* chamar `/prices/asof?date=D` **sem** `instruments` —
  a resposta seria o universo inteiro do Hub (~400 hoje, **aberto** na fase 2), que é
  "resposta de coleção sem limite superior", antipadrão nominal da §9 dos padrões; e a
  Custódia não tem catálogo contra o qual conferir a completude dela. *Rejeitado:* manter
  catálogo local de instrumentos — é a terceira identidade que a ADR-4 proíbe, e a §7.2 põe
  a resolução na borda, em Operações.

  | Laço | Condição de parada | Classificação | Desfecho |
  |---|---|---|---|
  | **A · janela de datas** `[desde, hoje]`, uma chamada por data | percorri a janela inteira | **completude** | sucesso |
  | | teto de dias por execução | **limite** | **falha**, `Hub.ColetaIncompleta` |
  | | erro de transporte / 5xx / timeout numa data | **limite** | **falha**, `Hub.Indisponivel` |
  | **B · fatias de `instruments`** | consumi todas as fatias do conjunto derivado do livro | **completude** | sucesso |
  | | teto de fatias | **limite** | **falha**, `Hub.ColetaIncompleta` |
  | | a resposta **não traz todos** os ids que a fatia pediu | **limite** | **falha**, `Hub.ColetaIncompleta` |

  **A última linha é a que substitui o boundary de `pageSize`, e ela é mais forte que o
  guard de `X-Total-Count` da §10.31** — porque aqui o contrato **garante** a contagem: a
  §4.5 diz, com todas as letras, que "instrumento sem preço ≤ D **aparece, nunca é
  omitido**", e explica por quê ("omitir converte 'não sei o preço' em 'essa posição não
  existe': quem calcula Σ qtd × preço soma menos parcelas e produz um número plausível e
  errado, sem sinal nenhum"). Logo, **igualdade de conjuntos** entre os `instruments`
  pedidos e os `items` devolvidos é a completude da fatia, e qualquer id faltando é
  violação de contrato, nunca "não tem dado". O boundary a testar é a **fatia cheia** (o
  tamanho exato do lote) e a resposta a que **falta exatamente um** id, que tem de reprovar.

  **A marcação do produtor, o que ela cobre e o que ela NÃO cobre — e este parágrafo é a
  correção de um buraco que atravessava F6→F7.** A §4.5 **já** marca o caso na resposta do
  `asof`: `dataRef: null`, `campos: null`, `"motivo": "sem_preco_ate_a_data"`. Isso é
  **ausência marcada pela fonte**, e a Custódia a trata como tal **no ponto onde ela
  chega**: **não** vira linha em `preco_atual` nem em `historico_precos` (não há valor a
  gravar, e gravar zero seria fabricar observação), e **alimenta a métrica** de "instrumento
  posicionado sem preço", no bootstrap e nas chamadas do worker. O **outro** `motivo` da
  §4.5, `instrumento_desconhecido`, é caso **diferente** (um instrumento do nosso livro que
  o Hub não conhece) e tem desfecho **próprio e nomeado**, com alerta: nunca se misturam.
  Confundir os dois é exatamente a re-derivação que a §10.32 diz que apodrece no primeiro
  caso novo — **e é aqui, entre os dois `motivo`, que a §10.32 morde de verdade nesta fase.**

  **O que a marcação NÃO cobre, escrito porque a versão anterior deste arquivo mandava o F7
  "ler a marcação" e ela não existe para ele.** São dois fatos, e os dois são de contrato:
  (i) `sem_preco_ate_a_data` é campo da **resposta REST** do `/prices/asof` — o
  `PriceObserved` da §5.1 **não tem campo `motivo`**, e ausência de preço no caminho de
  push simplesmente **não gera evento nenhum**; (ii) o caminho diário do F7 é o handler de
  `eod.ready(D)`, que **não chama o `asof`** — ele lê `preco_atual` e `historico_precos`.
  Somando: **não existe marcação persistida para o F7 ler**, e não existiria mesmo que esta
  fase gravasse uma, porque o produtor da marcação só fala pelo REST. A consequência está
  escrita na decisão "Dia sem preço ≤ D" do F7, rotulada lá como **desvio da §10.32**: o F7
  deriva a ausência consultando as próprias tabelas — "não existe linha de preço com
  `data_ref ≤ D` para o instrumento X" —, que é uma **consulta definida**, não uma
  comparação de valores. *Rejeitado, e era a outra saída possível:* gravar a ausência **como
  dado** (linha em `historico_precos` com `valor NULL` mais uma coluna `motivo`, ou tabela
  `precos_ausentes(instrumento_id, data_ref, motivo)`). Custaria schema, portanto seria
  decisão do **F3** e não desta fase, e ainda assim **não resolveria**: a tabela seria
  alimentada só pelo caminho REST (bootstrap e worker), ficando **vazia justamente nos dias
  cobertos só por push** — uma marcação que existe em alguns dias e falta em outros é pior
  que uma derivação uniforme, porque o F7 teria de tratar "sem linha na tabela de ausências"
  como inconclusivo e cair na derivação de qualquer jeito, agora com dois caminhos para
  divergir.

  **NÃO ENTRA:** recálculo (F7) — nesta fase `revisao > 0` apenas corrige o histórico,
  marca e registra; não há snapshot para reversionar ainda, e quando o F7 chegar ele calcula
  já a partir do histórico corrigido. Resposta explícita a "tem conserto?": **sim, e por
  construção.** Também não entra `CorporateActionObserved` (F9): essas mensagens continuam
  indo para o `custodia.parked`, com a política já decidida no F4.

  **Configuração: as CINCO listas mudam juntas**, mais o `.env.example`.
  `Hub__{BaseUrl,ApiKey}` entram (1) nos dois composes (com `:?`, sem default em
  Production), (2) no `envs:` do ssh-action, (3) **no bloco `env:` do mesmo step** — sem
  ele a variável chega vazia e a guarda `-z` acusa "secret vazio" com o secret cadastrado
  —, (4) no `printf` do `.env` e (5) nas dummies do config gate. Guarda de boot
  pela §6 do `PADROES`: fora dos ambientes isentos, segredo ausente **derruba o boot**, e a
  guarda de API key checa **comprimento mínimo E placeholder**, não só vazio — molde
  `../operacoes/src/Operacoes.API/Extensions/HubConfigGuard.cs` e `KeyStrengthGuard.cs`. O
  bloco do `.env.example` reescrito no F1 vira aqui o texto definitivo. Localmente o alias é
  `hub-precos-app`, nunca `app` (§10.1). **Avise no PR: mudança quebrante** para quem já tem
  `.env` local, e rode `config -q` contra o `.env` real.

  **Âncoras:** `ARQUITETURA` §7.1 (`preco_atual` e histórico local recomendado), §7.3 (ramo
  PriceObserved), §5.1 (chave de dedupe do consumidor), §5 (bootstrap de consumidor novo é
  REST do Hub, **nunca** replay do broker — a razão pela qual perder preço é barato e perder
  trade não é), §4 (`/prices/asof`), §12, ADR-9, ADR-12; `PADROES` §4, §10.16, §10.29,
  §10.30, §10.31, §10.32; `LEIA-ME-KIT` **"Escrever contrato assumindo que os campos andam
  juntos"** (é sobre este payload, e é a âncora do `dataRef` por campo) e "Porta publicada
  no compose local não existe em produção".

  **Prompt:**
  ```
  Implemente o handler de PriceObserved e o bootstrap REST da projecao de precos
  (ARQUITETURA 7.3 ramo PriceObserved, 7.1, 5.1, Fluxo 1 da 8.2).

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  ENTRA:
  - handler de PriceObserved: upsert em preco_atual E no historico_precos (a tabela ja
    existe desde o F3), dedupe pela chave natural
    (instrumentoId, dataRef, campo, fonte, revisao);
  - MONOTONICIDADE de preco_atual: ela e o ULTIMO preco (7.1), e dedupe por chave
    natural da idempotencia, NAO ordenacao. So sobrescreva se (dataRef, revisao) do
    evento for >= o armazenado PARA AQUELE INSTRUMENTO — e por INSTRUMENTO, nao por
    (instrumento, campo), porque a PK da 7.1 e instrumento_id e a linha guarda SO o
    campoPosicao (decidido no F3, item 5c). Evento de campo que NAO e o campoPosicao nao
    toca preco_atual: vai so para o historico. O historico_precos recebe TODAS as
    revisoes e TODOS os campos, sempre. Sem isso, um PriceObserved com dataRef mais antiga
    (redelivery, dreno do parking acumulado desde o F2, bootstrap rodando junto com o
    push) passa pelo dedupe — e chave natural diferente — e REGRIDE a projecao para um
    preco velho; e uma revisao 0 reentregue depois da revisao 1 do mesmo dia
    SOBRESCREVERIA a correcao. Esta fase e justamente a que drena um parking com SEMANAS
    de mensagens fora de ordem, no primeiro boot. A 5 diz que "redelivery e esperada e
    INOCUA (dedupe por chave natural)" — isso vale para o LIVRO, nao para uma projecao
    de "ultimo".
  - client HTTP do Hub para GET {hub}/prices/asof, com timeout, Polly e conversao de
    excecao em Result na Infrastructure. Molde:
    ../hub-precos/src/Hub.Infrastructure/TdApi/TdApiClient.cs e
    ../operacoes/src/Operacoes.Infrastructure/Catalogo/ (para o padrao de porta/Result);
  - comando/hosted service de bootstrap IDEMPOTENTE que preenche o historico e o
    preco_atual;
  - LOG DESTACADO de toda revisao > 0 (ARQUITETURA 12: correcoes sao raras e merecem
    visibilidade);
  - drenagem do custodia.parked do motivo `tipo_nao_tratado_prices`, pelo drenador e pela
    mecanica decididos no F4 (le messages_ready antes, consome no maximo N, republica o
    resto, parada classificada). NAO drene por routing key.

  DECISOES JA TOMADAS:
  1. HISTORICO LOCAL SIM, nao so preco_atual: o recalculo do F7 nao pode depender de o
     Hub estar online. A DDL de historico_precos JA EXISTE (F3) — nao a crie aqui.
  2. GET CONDICIONAL (If-None-Match) SIM, ao contrario do operacoes — aqui a URL e FIXA
     e sondada em ciclo, que e o caso do TdApiClient do molde e nao o da busca por termo
     (PADROES 10.30). MAS o store de ETag nasce COM PRAZO, TETO E EVICCAO (PADROES
     10.16): sem isso o hub ficou em 304 permanente depois de um banco zerado por fora.
     NAO porte o ConditionalGetStore do molde como esta.
  3. SEM last-known-good. Servir preco velho como fresco alimentaria snapshot no F7, e
     snapshot e documento que o cliente ve (P2) — e forward-fill materializado como
     observacao, proibido nominalmente pela secao 9 do PADROES. Registre a AUSENCIA com
     o motivo, para ninguem "consertar" portando o cache do operacoes.
  4. NAO EXISTE onboarding nem validacao de instrumento aqui: a resolucao nome->id da
     7.2 acontece na borda, em Operacoes (ADR-10). Hub__* serve bootstrap da projecao e
     o asof do worker, e nada mais. Nao crie HubCatalogoClient.
  5. revisao > 0 MARCA o fato no dado (coluna revisao no historico) para o worker do F7.
     O worker NAO vai re-derivar "houve revisao?" comparando valores (PADROES 10.32).

  NAO ENTRA: recalculo (F7 — nesta fase revisao > 0 so corrige, marca e registra),
  snapshot (ADR-9: preco individual NUNCA dispara a valoracao DIARIA; o gatilho 2 da 7.4,
  que a revisao > 0 habilita, e do F7 e REVERSIONA O PASSADO — nunca materializa o dia
  corrente), corpactions (F9 — seguem indo para o custodia.parked).

  COLETA (PADROES 10.31 — este defeito foi achado TRES vezes por tres portas diferentes
  no F5 do operacoes, sempre com a suite verde, e aqui a consequencia e pior que lista
  truncada: preco faltando em silencio vira snapshot com valor errado, gravado e
  versionado):

  ATENCAO: /prices/asof NAO E PAGINADO. A secao 4.5 o define como
  `?date=D[&instruments=a,b,c]` devolvendo `items[]` — NAO existe `page`, NAO existe
  `pageSize`, NAO existe `X-Total-Count`. Nao invente paginacao em contrato de terceiro,
  e nao teste boundary de pageSize: nao ha o que testar. Os lacos que EXISTEM sao dois.

  ESCOPO — quais instrumentos pedir. E derivado do LIVRO: os instrumento_id distintos de
  `movimentos`, EXCLUIDO o namespace `caixa:` (V4 do F3 — preco 1 por definicao, nao
  existe no Hub, e pedi-lo devolveria motivo=instrumento_desconhecido, que e sinal de
  defeito e nao deve ser fabricado por nos). NAO chame /prices/asof?date=D SEM
  `instruments`: a resposta seria o universo inteiro do Hub (~400 hoje, ABERTO na fase
  2), que e "colecao sem limite superior" — antipadrao nominal da secao 9 do PADROES — e
  a Custodia nao tem catalogo contra o qual conferir a completude dela. E NAO crie
  catalogo local de instrumentos: e a terceira identidade que a ADR-4 proibe.

  LACO A — janela de datas [desde, hoje], UMA CHAMADA POR DATA:
    percorri a janela inteira ................ COMPLETUDE -> sucesso
    teto de dias por execucao ................ LIMITE     -> FALHA, Hub.ColetaIncompleta
    erro de transporte/5xx/timeout numa data . LIMITE     -> FALHA, Hub.Indisponivel
  LACO B — fatias de `instruments`:
    consumi todas as fatias do conjunto ...... COMPLETUDE -> sucesso
    teto de fatias ........................... LIMITE     -> FALHA, Hub.ColetaIncompleta
    a resposta NAO traz todos os ids da fatia  LIMITE     -> FALHA, Hub.ColetaIncompleta
  Parada por LIMITE devolve FALHA, nunca Success parcial.

  A ULTIMA LINHA SUBSTITUI O BOUNDARY DE pageSize, e e MAIS FORTE que o guard de
  X-Total-Count da 10.31, porque aqui o contrato GARANTE a contagem: a 4.5 diz que
  "instrumento sem preco <= D APARECE, NUNCA E OMITIDO", e explica por que ("omitir
  converte 'nao sei o preco' em 'essa posicao nao existe': quem calcula Σ qtd x preco
  soma menos parcelas e produz um numero plausivel e errado, sem sinal nenhum"). Logo a
  completude da fatia e IGUALDADE DE CONJUNTOS entre os `instruments` pedidos e os
  `items` devolvidos, e id faltando e VIOLACAO DE CONTRATO, nunca "nao tem dado". Teste
  com a fatia CHEIA (o tamanho exato do lote) e com a resposta a que falta EXATAMENTE UM
  id, que tem que REPROVAR.

  A MARCACAO DO PRODUTOR — O QUE ELA COBRE E O QUE ELA NAO COBRE. A 4.5 marca o caso na
  RESPOSTA DO asof com dataRef: null, campos: null e "motivo": "sem_preco_ate_a_data".
  Trate como AUSENCIA MARCADA no ponto onde ela chega: NAO vira linha em preco_atual nem
  em historico_precos (nao ha valor a gravar, e gravar zero seria fabricar observacao), e
  ALIMENTA A METRICA de "instrumento posicionado sem preco". O OUTRO motivo da 4.5,
  `instrumento_desconhecido`, e caso DIFERENTE (instrumento do nosso livro que o Hub nao
  conhece) e tem desfecho PROPRIO E NOMEADO, com alerta. NUNCA misture os dois — e e AQUI,
  entre os dois motivos, que a PADROES 10.32 morde nesta fase.
  O QUE ELA NAO COBRE, e nao tente resolver: NAO grave "ausencia de preco" como linha para
  o F7 ler depois. Dois fatos de contrato: (i) sem_preco_ate_a_data e campo da RESPOSTA
  REST do /prices/asof — o PriceObserved da 5.1 NAO TEM campo `motivo`, e ausencia de
  preco no caminho de push nao gera evento nenhum; (ii) o caminho diario do F7 e o handler
  de eod.ready(D), que NAO CHAMA O asof — ele le preco_atual e historico_precos. Logo uma
  tabela de ausencias so seria alimentada pelo caminho REST e ficaria VAZIA justamente nos
  dias cobertos so por push: marcacao que existe em alguns dias e falta em outros e PIOR
  que derivacao uniforme. O F7 deriva a ausencia consultando as proprias tabelas ("nao
  existe linha com data_ref <= D para o instrumento X"), e isso esta ROTULADO LA como
  desvio da 10.32. Nao invente schema aqui para sustentar aquela leitura.

  E use `campoPosicao` do proprio item (4.5): faca item.campos[item.campoPosicao]. NAO
  hard-codeie `pu_venda` nem duplique a tabela de classes — a regra mora no Hub, e a 4.5
  explica que um default mentiria ou mudaria em silencio quando acao:PETR4 chegar.

  dataRef E POR CAMPO, NUNCA item.dataRef — esta e a linha mais facil de errar da fase.
  A 4.5: "dataRef por CAMPO, nao por item ... basta um dia em que a fonte nao publique
  taxa_compra para os campos dessincronizarem. Resolver a data uma vez por item e
  devolver so os campos daquela data faria um campo SUMIR EM SILENCIO". O dataRef DO
  ITEM e, por definicao, o MAIOR entre os campos: e resumo de frescura, nao a data de
  nada. Entao cada linha de historico_precos usa campos[<campo>].dataRef e o preco_atual
  usa campos[campoPosicao].dataRef. Usar item.dataRef para todos grava A DATA ERRADA para
  os campos atrasados, EM SILENCIO, numa tabela que alimenta snapshot — documento que o
  cliente ve (P2). Leia o LEIA-ME-KIT, secao "Escrever contrato assumindo que os campos
  andam juntos": ela foi escrita sobre ESTE payload.

  `code` SEPARADOS: Hub.Indisponivel (transitorio) x Hub.ColetaIncompleta (estrutural).
  Os dois podem ser 503, mas o code manda o operador para o lado certo.

  Configuracao: AS CINCO LISTAS mudam juntas com Hub__{BaseUrl,ApiKey} — (1) os dois
  composes (`:?`, sem default em Production), (2) `envs:` do ssh-action, (3) O BLOCO
  `env:` DO MESMO STEP (sem ele a variavel chega VAZIA e a guarda `-z` acusa "secret
  vazio" com o secret cadastrado), (4) printf do .env, (5) dummies do config gate.
  Guarda de boot pela secao 6 do PADROES: segredo ausente DERRUBA O BOOT, e
  a guarda de API key checa comprimento minimo E placeholder, nao so vazio — molde
  ../operacoes/src/Operacoes.API/Extensions/HubConfigGuard.cs e KeyStrengthGuard.cs.
  Reescreva o bloco do .env.example com o papel VERDADEIRO do Hub aqui. Localmente o
  alias e hub-precos-app, NUNCA app (PADROES 10.1). AVISE NO PR que e mudanca quebrante
  e rode `docker compose config -q` contra o .env REAL.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) preço do dia chegando **por push** do Hub e aparecendo
  em `preco_atual`; **a passagem completa do drenador do motivo `tipo_nao_tratado_prices`
  devolvendo COMPLETUDE e residual 0 para esse motivo, com `n_motivo ≥ 1`** — o teste
  **planta** a mensagem antes de rodar, porque com `n_motivo = 0` o desfecho é
  **VAZIO_DO_MOTIVO** (INCONCLUSIVO), e uma implementação que o classificasse como
  COMPLETUDE fecharia a fase **por vacuidade**, o mesmo defeito do gauge que zera no
  deploy; é a varredura que sabe o estoque,
  não um gauge (que zeraria no deploy seguinte) e não inspeção de routing key no broker (que
  conta mensagens, não tipos); uma `revisao > 0`
  reprocessada substituindo o valor em `preco_atual`, deixando **as duas** revisões no
  `historico_precos` e produzindo o log destacado. (**Estrita**)
  (a) `preco_atual` e o `historico_precos` **derrubados com `TRUNCATE`** e reconstruídos
  inteiramente pelo bootstrap REST, sem intervenção manual, com `diff` vazio contra o
  estado anterior — essa é a **prova** de que são descartáveis, não a afirmação de que são;
  (b) reentrega do mesmo `PriceObserved` não duplica linha nem muda nada;
  (c) **monotonicidade nas duas direções:** entregar `dataRef` D-3 **depois** de D-1 **não
  muda** `preco_atual`, e `revisao 0` depois de `revisao 1` do mesmo dia também não — com
  **controle positivo**: D **posterior** ao armazenado muda, e `revisao 1` depois de
  `revisao 0` muda. E o `historico_precos` fica com **todas** as entregas nos dois casos;
  (d) **a fatia de `instruments`, no lugar do boundary de página que não existe:** uma
  fatia **cheia** (o tamanho exato do lote) coletada com sucesso, e uma resposta a que
  falta **exatamente um** id pedido **reprovando** com `Hub.ColetaIncompleta` — é a
  completude por igualdade de conjuntos, garantida pela §4.5 ("instrumento sem preço ≤ D
  aparece, nunca é omitido"). Mais: parada por **limite** no laço de datas e no de fatias
  devolvendo **falha**, com `Hub.Indisponivel` e `Hub.ColetaIncompleta` como `code`
  separados;
  (e) um item com `motivo: "sem_preco_ate_a_data"` e `dataRef: null` **não** cria linha em
  `preco_atual` nem em `historico_precos`, e **incrementa** a métrica de instrumento
  posicionado sem preço; um com `motivo: "instrumento_desconhecido"` cai num desfecho
  **diferente e nomeado**, com alerta — os dois no mesmo teste, para que confundir um com o
  outro reprove;
  (f) `campoPosicao` respeitado: um item cujo `campoPosicao` **não** é `pu_venda` grava o
  campo certo — o teste que só usa `pu_venda` não distingue a implementação correta da que
  hard-codeia;
  (f2) **`dataRef` por CAMPO, e é o Pronto que faltava:** uma resposta do `asof` com
  `pu_venda` em **D** e `taxa_venda` em **D-3** (o `item.dataRef` sendo **D**, o maior)
  produz **duas linhas de `historico_precos` com `data_ref` DIFERENTES**, e `preco_atual`
  com a data do `campoPosicao`. **O teste reprova se as duas linhas saírem com a mesma
  data** — é a única forma de distinguir a implementação correta da que usa `item.dataRef`
  para tudo, e nenhuma outra asserção desta fase a pega;
  (g) **nenhum caminho de código escreve em `snapshots_posicao`**, provado por varredura, e
  a tabela permanece vazia ao fim da suíte. *A asserção anterior ("N eventos `PriceObserved`
  não criam linha em `snapshots_posicao`") era verdadeira por **ausência de código** — nesta
  fase não existe escritor de snapshot nenhum, o handler de `eod.ready` e o worker só nascem
  no F7 —, então passaria também com a ADR-9 violada, e o "controle positivo" oferecido
  (contador de `preco_atual`) provava que o evento chegou, não que existe mecanismo de
  valoração contido. A prova comportamental da ADR-9 fica onde ela morde: no F7, com o
  `eod.ready` como controle positivo.*
  Bootstrap provado **pelo caminho real**:
  `docker exec` de dentro de um container na rede `plataforma` batendo no alias
  `hub-precos-app`, nunca em `127.0.0.1:porta-local` — o compose de produção do Hub não
  publica porta nenhuma, de propósito.
  E (h) **I12 reconferida agora que `Hub__BaseUrl` existe:** a varredura de
  `ConnectionStrings*` do F1 continua devolvendo **exatamente uma** connection string, para
  o database `custodia`. É esta a fase em que a tentação aparece — "o bootstrap seria mais
  simples lendo o banco do Hub" —, e a ADR-12 a proíbe; o que entrou aqui é uma **URL HTTP**,
  não uma conexão.

- [ ] **F7** — `eod.ready`, snapshots e o worker de recálculo: a engrenagem central.
  **Dependência externa nova: o Hub publicando `eod.ready`.**

  Handler de `EodPricesReady(D)` materializando o snapshot de **todos** os clientes num
  batch, sobre o intervalo `[U_anterior + 1, D]` percorrido em **ordem crescente** e gravando
  **`eod_processado(d)` para cada dia `d`, na mesma transação que fecha aquele dia** — a
  **DDL nova desta fase**, uma linha por data materializada, com `data_ref date` e
  `processado_em timestamptz`, e não é projeção do livro; `snapshots_posicao` versionado (marca a versão anterior `vigente = false` e
  **INSERE** a nova com `calculado_em`); o worker `recalcular(cliente?, instrumento, desde)`
  da §7.4 com os gatilhos **1** (movimento com `data_evento` no passado — ligando o que o
  F4 deixou só registrado) e **2** (`PriceObserved` com `revisao > 0`, ligando o que o F6
  deixou marcado); e o job de reconciliação livro ↔ `posicao_corrente` que **ALERTA e não
  corrige** (a outra metade da decisão B), comparando **as três colunas** pela **dobra da V1
  com corte `D = ∞`** — e que alerta também nas **duas** direções de `U` contra a projeção
  (`MAX(snapshots_posicao.data) > U` e o simétrico `< U` com `U` além do último `D`
  conhecido). A **varredura de defasagem de snapshot**, que é o que torna os gatilhos 1 e 2
  duráveis sob crash. Comando administrativo `materializar --desde --ate` dentro do container
  (sem endpoint, ADR-10): é o caminho de bootstrap **fora** do handler de evento, **com guarda
  no `--ate`**. Drenagem do `custodia.parked` de **DOIS** motivos — `tipo_nao_tratado_eod`
  (o backlog acumulado desde o F2) e **`intervalo_acima_do_teto`** (o motivo que esta fase
  cria, e cuja passagem é metade do Pronto (f2)) —, pelo drenador do F4. Alerta de "dia útil
  sem `eod.ready` até 12:00" (§12), lido de `eod_processado.processado_em`.

  **Por que a DDL desta fase nasce aqui e não no F3, que se declarou "a última fase barata
  para acertar o schema".** O argumento do F3 é a §10.21, e ele vale para **tabela
  append-only com trigger**: sair de constraint estrita é `DROP CONSTRAINT`, sair de
  constraint que faltou exige `DISABLE TRIGGER` e perícia manual. `eod_processado` não é
  nenhuma das duas coisas — é **tabela de controle de consumo, mutável, sem trigger de
  imutabilidade e fora do alcance da §10.21**, não está na §7.1, e a decisão sobre a **forma**
  dela (as duas colunas, a chave, e o que `U` nulo significa) **é desta fase**: ela não
  existia no F3 para ser decidida lá. O escopo do F3 está escrito lá com essas palavras —
  vale para o livro e para as quatro projeções da §7.1.
  **`eod_processado` não é "a única tabela fora da §7.1 deste roadmap" — são DUAS, e a outra
  vem antes.** O **calendário de dias úteis** nasce no **F5**: tabela de **configuração**
  própria deste banco, populada por migration/seed, que não se reconstrói do livro — e ela
  serve esta fase também ("dia útil" do snapshot sai dela). *Escrever "a única tabela nova
  nasce aqui", como dizia a versão anterior desta frase, era inventário incompleto: o escopo
  "as cinco tabelas são da §7.1" continua verdadeiro, mas a enumeração das exceções que veio
  com ele não estava.* O que é único desta fase é o que está escrito acima: **a DDL nova
  DESTA fase**.

  **O vocabulário é o do F3, literalmente** — e a peça desta fase é a **V4**: as linhas
  `caixa:BRL` e `caixa:a_liquidar` **entram no snapshot**, com `preco = 1,000000` por
  definição. Sem isso, `snapshots_posicao.preco` sendo NOT NULL, não nasceria linha de
  caixa nenhuma: o F5 escrituraria o limbo D→D+1 no livro e esta fase o apagaria do
  documento, e o "soma dos instrumentos + caixa = patrimônio diário" da §7.5 ficaria sem a
  parcela de caixa — derrubando o Pronto do F8. *O que esta fase precisa é só isto: **a
  linha de caixa existe no snapshot**. A **igualdade** "soma dos instrumentos + caixa =
  patrimônio" é o que a PENDÊNCIA BLOQUEANTE do `caixa:BRL` (F3, V2) deixa em aberto, e o F8
  Pronto (g) já deixou de afirmá-la — não a reintroduza aqui como critério.*

  **ESTA É A FASE QUE MUDA O PERFIL DE RECURSO — MEDIR DE NOVO.** O teto de **192m** foi
  medido na VPS em 2026-09-07 para um perfil de API pequena **sem worker** (o próprio
  `docker-compose.prod.yml` registra o worker da §7.4 como o candidato óbvio a mudá-lo). Um
  recálculo do Fluxo 5 percorre **todos** os clientes posicionados num instrumento, **todos**
  os dias úteis do intervalo, num host de **1 núcleo** — e o handler de `eod.ready` **depois
  de um salto de datas** percorre (dias pulados × clientes × instrumentos) de uma vez.
  Medir com `docker stats` **durante um recálculo largo e durante um `eod.ready` com
  salto**, não em regime, e **olhar o que sobra para os vizinhos antes de subir o próprio
  teto** (§10.14): o `tesouro-direto-alloy` já estava em 169 MiB de 192 (88%), e o
  `hub-precos-app` roda **sem limite nenhum**, enxergando os 1,9 GB do host em vez do cgroup
  — a lacuna da §10.12 já aberta hoje na máquina.

  **Decisões desta fase:**

  - **ADR-9 travada por teste, não por disciplina — E COM O QUALIFICADOR INTEIRO.** A §7.3
    diz que `eod.ready` é o único gatilho **da valoração diária**, e o qualificador é metade
    da regra: a **exceção nomeada** é o **gatilho 2 da §7.4**, que esta fase implementa e
    que **trabalha sobre o passado** (`recalcular(TODOS os posicionados, desde=dataRef)`) —
    versionando o dia que mudou **e criando o dia que faltava** — sem nunca materializar dia
    que o `eod.ready` ainda não fechou. Ler "único gatilho de valoração" sem o "diária"
    proíbe o gatilho 2 — e sem o gatilho 2 **o Fluxo 5 não existe**, que é metade do critério
    de pronto do item 4 da §9. As duas coisas convivem porque fazem coisas diferentes:
    `eod.ready` **materializa os dias que ele fecha** — o `D` que ele anuncia e, pela decisão
    1c abaixo, os dias que o salto de datas pulou; o worker **trabalha sobre o que já foi
    materializado**. *A frase estava no singular numa versão anterior deste bloco e
    contradizia a 1c oitenta linhas abaixo, na mesma fase.*
    *Rejeitado:* materializar snapshot **do dia corrente** ao receber `prices.td` "para ficar
    mais fresco" — são ~400 pings/dia e o resultado é o mesmo; a regressão que reintroduziria
    isso é barata de escrever e cara de perceber.
  - **OS DOIS FATOS MEDIDOS SOBRE O `eod.ready`, e eles decidem o recorte. Estão aqui com
    arquivo e linha para a próxima sessão NÃO ter que medir de novo** (§10.9: verifique com
    o comando literal; `LEIA-ME-KIT`, "Especular em vez de medir"). O produtor é o
    `../hub-precos`:

    1. **O `D` do `eod.ready(D)` NÃO é uma data de relógio, e na TD ele vem defasado.** Em
       `../hub-precos/src/Hub.Infrastructure/Persistence/Repositories/IngestaoReadRepository.cs`,
       linhas **37-47**, o `SqlDataEodFechado` é
       `SELECT MIN(wm) FROM (SELECT MAX(p.data_ref) AS wm … GROUP BY i.id) t` sobre os
       instrumentos **ativos** da classe TD. `D` é, então, **o MENOR dos últimos preços**:
       para cada instrumento ativo da classe TD **que tem algum preço**, o `MAX(data_ref)`
       dele; `D` é o **menor** desses máximos — derivado do **dado**, nunca do relógio. Como a
       TD publica o fechamento do dia anterior por volta das
       06:00 BRT, `D` é tipicamente **`hoje−1`**, e mais antigo em segunda-feira, em feriado,
       ou quando um instrumento está atrasado.
       **TRÊS qualificadores, e os três existem porque a formulação natural deste fato é
       mais forte do que o SQL. O primeiro é sobre o que `MIN` de máximos NÃO garante:** *"a data
       mais recente em que todos os instrumentos ativos que têm algum preço têm preço"* sai
       errada. `MIN` de máximos garante que **nenhum** instrumento tem o último preço
       **antes** de `D`; não garante que cada um tenha preço **em** `D`. *Contraexemplo: A com
       preços em `D−1` e `D+1`, B com preço em `D` → `MIN(MAX) = D`, e A não tem preço em
       `D`.* Para esta fase é inofensivo, porque a valoração é `preco(último ≤ D)` e não
       `preco(em D)` — mas quem escrever a asserção como "todo instrumento tem preço em `D`"
       escreve um teste que falha por acaso de dado, e o instrumento cujo único preço é
       **posterior** a `D` cai no Pronto (k), que é o caminho certo para ele.
       **O segundo qualificador é sobre quem o `MIN` nem enxerga**, e é a premissa que o
       executor mais facilmente tomaria por garantia forte demais: aquele SQL usa
       `JOIN precos`, **não**
       `LEFT JOIN` (linha 43), então **instrumento ativo sem nenhum preço é EXCLUÍDO do
       `MIN` em vez de anulá-lo**. O Hub cobre esse caso com `SqlDataEodAtivosSemPreco`
       (linhas 54-63 do mesmo arquivo), que só produz **warning** e **não bloqueia** a
       emissão (`IngerirPrecosTdCommandHandler.cs:339-345`). Consequência direta para esta
       fase: **`eod.ready(D)` PODE chegar com instrumento ativo sem preço algum**, que é
       exatamente o caso do Pronto (k) — sinal, e nunca preço fabricado.
       **O terceiro qualificador é sobre o CONJUNTO de que o `MIN` é tomado, e ele existe
       porque "DOIS" era, ele mesmo, uma afirmação de completude errada.** O `WHERE`/`JOIN`
       daquele SQL (linhas 42 e 44) restringe o conjunto por `instrumento_fontes.fonte =
       @fonte` **e** por `ativo_ate >= @hoje` — um parâmetro de **relógio**. Isso **não** torna
       `D` uma data de relógio (a asserção principal sobrevive inteira), mas o conjunto sobre
       o qual o mínimo é tomado **depende de `hoje` e da fonte**; e um instrumento ativo **sem
       linha em `instrumento_fontes` para aquela fonte** some do `MIN` **e** do
       `SqlDataEodAtivosSemPreco`, isto é, nem entra na conta nem gera o warning. Para esta
       fase a consequência é a mesma do segundo qualificador — o Pronto (k) —, e o que muda é
       não afirmar que a lista de ressalvas está fechada em dois.
    2. **`eod.ready(D)` é emitido UMA VEZ POR DATA, para sempre.** Em
       `../hub-precos/src/Hub.Application/Ingestao/IngerirPrecosTdCommandHandler.cs`
       linha **337**, ele só emite se `fechado > eod.UltimoEmitido`; e a migration
       `../hub-precos/src/Hub.Infrastructure/Persistence/Migrations/20260822231916_CriaIndiceUnicoOutboxEod.cs`
       linha **15** cria
       `CREATE UNIQUE INDEX ux_outbox_eod_data ON outbox ((payload->>'data')) WHERE tipo = 'EodPricesReady'`.
       **Não existe reentrega do `eod.ready` para a mesma data**, e o `D` pode **pular**
       datas (se `fechado` salta de 08-10 para 08-14, os dias 11, 12 e 13 nunca são
       anunciados).

    **Duas frases da versão anterior deste arquivo morreram com essa medição, e é bom que
    fique escrito qual erro elas eram:** *"o `eod.ready(hoje)` o materializará"* é **falsa**
    pelo fato 1 (o `eod.ready` quase nunca carrega `hoje`, e nunca repete uma data); e
    *"está correto … não 'conserte'"*, aplicada ao intervalo vazio, era **perigosa** pelo
    fato 2 — se o recorte do worker excluir um dia, **nada mais o cria**.
  - **O MECANISMO que faz as duas conviverem é RECORTE DE INTERVALO, e ele é um DESVIO
    PARCIAL da §7.4 — rotulado, na mesma convenção dos outros desvios deste arquivo.** O
    worker percorre **`[desde, U]`** e **INSERE quando o valor difere do vigente OU
    QUANDO NÃO EXISTE LINHA**, com **`U` = o último dia JÁ MATERIALIZADO por esta casa**,
    e não uma data de relógio. A §7.4 escreve `para cada dia útil D em [desde, hoje]` e
    `se difere do vigente (ou NÃO EXISTE LINHA)`: **o que muda é só o limite superior**, que
    deixa de ser `hoje` e passa a ser `U`; a cláusula "não existe linha" fica **literal**,
    como está lá. **Por que muda:** materializar dia é do `eod.ready` (ADR-9), e pelo fato 1
    o dia que ele materializa **não é uma data de relógio** — escrever `hoje` ou `hoje−1` no algoritmo é
    escrever um palpite sobre o relógio de outro serviço. *Sem o rótulo, o executor que ler
    a §7.4 — como o próprio prompt manda — encontra a contradição sem explicação e tende a
    seguir a `ARQUITETURA`.*
    **`U` é `MAX(data_ref)` da tabela `eod_processado`**, **uma linha por DIA
    materializado** — não uma por `D` anunciado —, gravada na **mesma transação que fecha
    aquele dia inteiro**, pelo handler ou pelo comando administrativo, que aqui fazem a mesma
    coisa. *Um `eod.ready(D)` que materializa `[D−4, D]` deixa **cinco** linhas, e a de `D`
    exatamente uma. A formulação anterior — "uma linha por `D` cujo batch fechou em
    COMPLETUDE, gravada ao fim do batch" — descrevia outra tabela: ela contradizia o resumo
    desta fase ("uma linha por data materializada"), contradizia o comando administrativo, e
    fazia o alerta da condição (iii) disparar em toda operação normal.*

    **A tabela tem DUAS colunas com papéis separados, e confundi-las torna o alerta da §12
    inimplementável:** `data_ref date` — a data **do dado**, o `D` do evento, e a única de
    que sai `U` — e `processado_em timestamptz` — a hora **de relógio** em que este serviço
    fechou aquele batch, e a única de que sai o alerta.
    **A DDL inteira, decidida aqui e não deixada para o executor:**
    `eod_processado (data_ref date PRIMARY KEY, processado_em timestamptz NOT NULL)`, na
    migration desta fase, snake_case como o resto do schema (`PADROES` §3; molde
    `../operacoes/src/Operacoes.Infrastructure/Persistence/`, o mesmo que o F3 usa).
    **A `PRIMARY KEY (data_ref)` não é zelo — é o que o Pronto (c) afirma.** "A reentrega do
    mesmo `eod.ready(D)` deixa `eod_processado(D)` com **uma** linha" ou é constraint ou é
    disciplina de handler: sem a chave, duas reentregas concorrentes gravam duas linhas e nada
    reclama, e o teste do Pronto vira a única guarda de um invariante que pertence ao banco.
    A gravação é **`INSERT … ON CONFLICT (data_ref) DO NOTHING`**, e o `DO NOTHING` é escolha
    e não descuido: `processado_em` é a hora em que aquele `D` foi materializado **pela
    primeira vez**, que é o que o alerta das 12:00 quer saber — um `DO UPDATE SET
    processado_em = now()` faria a reentrega **do broker** adiar o alerta sem que nenhum dado
    novo tivesse chegado, que é o mesmo erro de medir chegada por um campo que não a mede.
    **Os DOIS índices, porque são DOIS padrões de acesso novos e a §3 é literal** (*"todo
    padrão de acesso novo exige índice correspondente na mesma migration"*): `MAX(data_ref)`
    é servido pela **`PRIMARY KEY`**, e `MAX(processado_em)` — a leitura do alerta das 12:00
    — ganha **`ix_eod_processado_processado_em`** na **mesma** migration, com o nome no padrão
    `ix_tabela_colunas` da §3. *Tentei escrever a ausência dele como decisão ("roda uma vez
    por dia sobre uma tabela minúscula") e ela não sobrevive à §3, que não abre exceção por
    tamanho de tabela — e um desvio de padrão para economizar um índice numa tabela de uma
    linha por dia útil seria pagar caro por nada.*
    **O alerta "dia útil sem `eod.ready`
    até 12:00" (§12) é sobre `processado_em`, NUNCA sobre `data_ref`**, e a razão está no
    FATO 1 desta mesma fase: `data_ref` **não responde à pergunta que a §12 faz**. A §12
    pergunta *"chegou evento hoje?"* — chegada —, e `data_ref` é propriedade do **dado da
    fonte**: na TD ela vem defasada (tipicamente `hoje−1`, mais antiga em segunda-feira,
    feriado ou com instrumento atrasado), então `MAX(data_ref) < hoje` é verdade em todo dia
    útil normal e o alerta dispara **todo dia, para sempre**. A regra fica:
    `MAX(processado_em) < hoje 12:00` num dia útil.
    *Contraexemplos tentados, e os dois falham pela `data_ref`, em direções opostas:* (i) um
    `eod.ready` que chega **hoje** trazendo `D` de três dias atrás — o caso da segunda-feira:
    pelo `processado_em` não há alerta, que é o certo (o Hub falou hoje), e pela `data_ref`
    haveria; (ii) uma fonte futura que publique preço do **próprio dia** — `data_ref` passaria
    a ser `hoje` e o alerta **nunca** dispararia, mesmo com o Hub mudo desde ontem. O mesmo
    campo, o mesmo alerta, os dois sinais errados: a `data_ref` simplesmente não é a medida.

    **DESVIO da ADR-5 e do antipadrão nominal da §9 do `PADROES` — rotulado aqui, e entra na
    lista canônica de desvios deste arquivo.** A ADR-5 (`ARQUITETURA`) decide *watermark
    derivado, `MAX(data_ref)` via LEFT JOIN, nunca coluna/flag de controle*, e a §9 do
    `PADROES` proíbe nominalmente *"estado de controle duplicando dados (flags de bootstrap)
    — derive dos dados (padrão watermark/reconciliação)"*. `eod_processado` **é** uma tabela
    de controle guardando um watermark: está dentro do que as duas proíbem, e passar sem o
    rótulo era o defeito. **É o único desvio deste roadmap que viola um antipadrão NOMINAL da
    §9, e por isso ele carrega a cláusula inteira, não meia: rotulado aqui, na lista canônica
    do F5, APROVADO PELO `advisor` com as quatro condições escritas abaixo, e A GRAVAR NA
    MEMÓRIA ao fechar a fase** — a mesma exigência do desvio (8), e a regra de ouro do
    `CLAUDE.md` (*"desvio de padrão só com justificativa explícita, aprovada pelo `advisor` e
    gravada na memória"*). *Um desvio que viola antipadrão nominal carregando menos cláusula
    que os outros era assimetria na direção errada.*
    **O argumento que sustenta o desvio é o I6 — e os outros dois que estavam escritos aqui
    não sustentavam, e saíram (os dois *Rejeitado* logo abaixo):** *só existe linha de snapshot em
    dia útil com posição ≠ 0*. Um dia materializado em que **nenhum** cliente tinha posição
    **não deixa linha nenhuma** em `snapshots_posicao` — então `MAX(data)` daquela tabela não
    distingue *"materializado e vazio"* de *"não materializado"*. O derivado **trava** naquele
    dia, e o próximo `eod.ready` re-materializa um intervalo que só cresce.
    **A premissa da ADR-5 é *"o derivado não pode mentir"*, e aqui ela é falsa:** o derivado
    mente sobre o dia vazio, e mente do jeito que não se percebe — devolvendo uma data
    plausível. *É por isso que este é um DESVIO e não uma aplicação da ADR:* as duas
    condições de migração que a ADR-5 **lista** (a opção (b) incomodar, ou o `MAX` pesar) não
    são esta; o que cai é a premissa, não uma das condições listadas.
    *Rejeitado, e o motivo anterior não sustentava:* recusar `MAX(data)` de
    `snapshots_posicao` porque *"um `TRUNCATE` + reconstrução mudaria o valor de `U`"* — a
    ADR-5 **antecipa e aceita** esse efeito com todas as letras (*"apagar preços de um
    instrumento força re-backfill dele — é o botão de reparo, documentar"*). Rejeitar o
    derivado porque o reparo funciona é rejeitar a ADR pelo motivo que ela declara aceitável.
    *E a §10.32 NÃO apoia esta decisão, apesar de ter sido citada aqui:* ela trata de
    **produtor marcando distinção para o consumidor**; aqui é o consumidor registrando o
    **próprio** estado, que é o objeto da §9. A citação saiu.

    **Perdê-la degrada, não corrompe — mas isso só é verdade com o TETO abaixo, e só porque
    `U` nulo tem significado DEFINIDO. E a definição são DUAS metades, uma por consumidor da
    tabela: escrever uma só deixa a outra sem regra, que foi o que uma versão anterior deste
    bloco fez.**
    - **HANDLER de `eod.ready(D)` — aqui `U_anterior` nulo é o limite INFERIOR:** o intervalo
      começa em **`MIN(data_evento)` do livro**, não em `D`. Com isso, "perdi a tabela" e
      "primeira execução de todas" produzem o **mesmo** comportamento correto e o handler
      **não precisa distingui-las**: ele re-percorre o intervalo, e o "só versiona se difere
      do vigente" faz cada dia já correto custar **zero INSERT**.
    - **WORKER `recalcular` — aqui `U` nulo é o limite SUPERIOR, e a regra é outra:** o worker
      percorre `[desde, U]`; com `U` nulo o intervalo é **VAZIO** e o worker **não faz nada**
      até o próximo `eod.ready` regravar a linha. *E isso não perde correção, pela razão que
      fica escrita: o `eod.ready` seguinte re-percorre `[MIN(data_evento), D]` com **a mesma
      função** e cria ou versiona exatamente os dias que o worker teria tocado. O que a perda
      custa ao gatilho 2 é **latência** — a revisão de preço espera o próximo `eod.ready` —,
      nunca conteúdo.*
    *A versão anterior deste bloco escreveu só a primeira metade e **apagou** a segunda no
    mesmo movimento:* "`U` nulo significa materializar a partir de `MIN(data_evento)`" é
    limite **inferior** e fala do handler; o worker ficou com `[desde, ???]` e sem regra — ou
    não fazendo nada por convenção não escrita, ou percorrendo até `hoje`, que é justamente o
    desvio da §7.4 que esta fase existe para evitar. **É por isso que I5 também traz o caso
    nulo.**
    *Contraexemplo tentado, e é ele que mata a versão anterior desta frase:* com `U = D−4`
    gravado, tabela perdida, `U` nulo e a regra antiga ("`U` nulo → intervalo é só `D`"), o
    handler materializaria **só `D`** e os dias `D−3..D−1` ficariam sem linha **para sempre**
    — pelo FATO 2 nenhum `eod.ready` futuro os anuncia. Isso é **corromper**, não degradar, e
    é o buraco permanente que a decisão 1c existe para fechar, reaparecendo pela porta ao
    lado. Com `U_anterior` nulo ⇒ início em `MIN(data_evento)` do livro (a metade do
    handler), o contraexemplo não se monta.
    *Rejeitado:* tornar `eod_processado` não-opcional com um **registro de bootstrap
    explícito** para distinguir "tabela vazia" de "primeira execução" — é a *flag de
    bootstrap* que a §9 proíbe **pelo nome**, dentro da mesma decisão que já está desviando
    da ADR-5; seria pagar o desvio duas vezes. *Rejeitado:* só somar ao Pronto (f) o caso de
    perda total — teste não torna frase falsa verdadeira, ele apenas documenta o buraco; o
    caso **entra** no Pronto, mas como consequência da definição acima, não no lugar dela.
    **Limite declarado:** com o livro vazio, `MIN(data_evento)` é nulo e o intervalo é só
    `D` — que é o certo, porque sem movimento nenhum não há posição ≠ 0 para materializar.

    **O TETO DO INTERVALO DO HANDLER, e é ele que faz o "degrada, não corrompe" ser
    verdade.** *Contraexemplo que derruba a frase sem o teto, e ele não é hipotético:* o F4
    entra em produção antes do F7 e acumula livro; com `eod_processado` perdida,
    `MIN(data_evento)` pode ser de anos atrás e o batch vira (anos de dias úteis × clientes ×
    instrumentos) **dentro de um handler que só dá ack na completude** (decisão 2). O
    `consumer_timeout` do RabbitMQ fecha o canal, a mensagem volta, o handler recomeça **o
    mesmo** batch ilimitado — laço de reentrega que nunca fecha, `eod_processado(D)` nunca
    gravado, e pelo FATO 2 nenhum `eod.ready` futuro reanuncia aquele `D`. Isso é
    **corromper**, pela mesma aritmética que este bloco usou para matar a regra antiga, agora
    entrando do outro lado. **Regra:** o handler conta os **dias úteis** de `[início, D]`
    **antes** de começar e, passando do teto, **não processa** — devolve **LIMITE** (§10.31),
    alerta, e a mensagem vai para a `custodia.parked` pelo `custodia.parking` com o motivo
    **`intervalo_acima_do_teto`**; nunca `nack` em laço, nunca ack-e-descarta. **O teto é
    `IConfiguration`, com default de 90 dias úteis**, e o número tem razão escrita: um
    `eod.ready` normal traz **um** dia e um salto operacional plausível (ingestor fora do ar
    por uma semana, instrumento atrasado) traz menos de dez — 90 é uma ordem de grandeza
    acima disso e ainda é um batch que cabe. **A medição desta fase confirma ou corrige o
    número** (`docker stats` durante um `eod.ready` com salto, que esta fase já exige); o que
    **não** é configurável é a existência do teto. *Isso não mexe em nenhuma das cinco listas
    de configuração: é `appsettings` com default no código, não segredo.*
    **E o caminho de reparo, porque "parar" não pode virar "perder":** o bootstrap de um
    livro grande se faz **fora do handler de evento**, por comando administrativo dentro do
    container — `materializar --desde --ate`, da mesma família do comando de reconstrução de
    `posicao_corrente` (Decisão B do F4) e do drenador do parking, e **sem endpoint**
    (ADR-10). **O comando NÃO está sujeito ao teto** — ele é explícito, tem operador olhando
    e é exatamente o caminho para intervalo grande — **e grava `eod_processado` para cada dia
    que materializa**, pela mesma regra do handler (a linha entra na transação que fecha
    aquele dia inteiro, nunca antes; percurso em ordem crescente). Rodado o comando, `U`
    avança, e a passagem do drenador do motivo `intervalo_acima_do_teto` reentrega o
    `eod.ready(D)` estacionado, que agora cabe no teto. O ciclo fecha com peças que já
    existem, e a mensagem nunca é descartada — pelo FATO 2, descartá-la perderia aquele `D`
    para sempre.

    **O `--ate` TEM GUARDA, e ela é obrigatória porque este comando é o ÚNICO escritor capaz
    de ADIANTAR `U`.** Sem validação, `materializar --desde 2026-01-01 --ate 2026-12-31` — uso
    plausível, já que o comando existe justamente para intervalo grande — grava
    `eod_processado` até dezembro, e a partir daí: (1) o handler passa a calcular
    `[U_anterior + 1, D]` com `U_anterior > D`, isto é, **intervalo vazio ou invertido**, o
    batch "fecha" por vacuidade e nenhum `eod.ready` materializa mais nada, para sempre; (2) o
    worker percorre `[desde, U]` até uma data **futura**, materializando dias que nenhum
    `eod.ready` fechou — que é literalmente o que o recorte existe para impedir (ADR-9). **É a
    corrupção que a propriedade "`U` atrasa, nunca adianta" declara impossível, produzida pelo
    comando que a mesma decisão introduz.**
    **Regra: o comando RECUSA `--ate` maior que o último `D` ANUNCIADO** — o maior entre
    `MAX(data_ref)` de `eod_processado` e o `D` da mensagem estacionada com
    `intervalo_acima_do_teto`, quando houver —, com **falha alta**, mensagem dizendo qual é o
    limite e por quê, e **nada gravado**. E o detector fica escrito junto, porque validação de
    argumento não cobre `UPDATE` manual nem versão antiga do binário: é a **alínea (iv)** da
    reconciliação, acima (`MAX(snapshots_posicao.data) < U` com `U` além do último `D`
    conhecido).
    **A parada do comando é CLASSIFICADA, como a de todo laço deste arquivo (§10.31), e ele
    está em I15:** **COMPLETUDE** = o intervalo inteiro pedido foi materializado, dia a dia,
    em ordem crescente; **LIMITE** = interrupção do operador, falha no meio, ou recusa do
    `--ate` — e **limite devolve falha com código de saída não-zero**, nunca "materializei o
    que deu". *Isto NÃO acrescenta valor à lista de `x-custodia-motivo`: aquela lista é de
    mensagem estacionada no broker, e este comando não estaciona mensagem nenhuma — o desfecho
    dele é código de saída e log, e dizer isso por escrito é o que impede a fase seguinte de
    "completar" a lista com um motivo que ninguém emite.* **A interrupção no meio é segura por
    construção**, e é a mesma propriedade do handler: como cada dia commita inteiro e em ordem
    crescente, `U` fica no último dia **inteiro** — atrasado, nunca adiantado — e a próxima
    execução recomeça de onde parou. *Contraexemplo tentado contra o "por construção", e ele
    não abre:* os pontos de interrupção possíveis são **dois** — entre o commit do dia `d` e o
    do dia `d+1`, que deixa `U = d`; e **dentro** do dia `d`, que não commita nada daquele dia
    e deixa `U = d−1`. Não há um terceiro, porque **não existe commit parcial de dia** — é
    exatamente essa a regra, e é ela que o Pronto (f) exercita.
    *Rejeitado:* deixar o handler percorrer intervalo ilimitado confiando que o
    `consumer_timeout` seja generoso — é dimensionar sem medir, num host de **um** núcleo, e
    o modo de falha é o pior que existe: laço quente que nunca converge e que não se
    distingue de lentidão. *Rejeitado:* **dispensar o teto** porque o commit por dia faz a
    reentrega progredir. **A premissa agora é verdadeira** — o commit por dia é a unidade de
    completude desta fase, decidida na propriedade abaixo —, mas ela **não conclui**, e o
    argumento anterior deste *Rejeitado* ("a reentrega não progride, porque
    `eod_processado(D)` só entra no último commit") caiu junto com aquela regra, além de ser
    **circular**: rejeitava a alternativa citando a regra que a alternativa propunha mudar.
    **Progredir não é caber**, por três razões independentes: (i) o ack só sai na completude
    do intervalo, e `custodia.prices` é a **mesma fila** que carrega `TradeRegistered` (§7.3,
    `ARQUITETURA.md:558`) — um batch de anos avançando um dia por reentrega mantém a fila
    inteira parada atrás dele, e o registro de operações para junto; (ii) num host de **1
    núcleo** e sob o teto de memória desta fase, isso é um laço quente de reentregas
    competindo com os vizinhos (§10.14) por horas, **indistinguível de lentidão** para quem
    olha de fora; (iii) §10.31: um laço cuja única parada é a exaustão do intervalo **não tem
    parada por LIMITE nenhuma** — o teto é o que dá nome à condição, e o nome
    (`intervalo_acima_do_teto`, com alerta e parking) é o que transforma "travado em silêncio"
    em "estacionado e visível". **O commit por dia é adotado; ele apenas não substitui o
    teto.**

    **A propriedade da qual tudo isto é consequência, e ela vale escrita sozinha: `U` pode
    ATRASAR, nunca ADIANTAR.** `U` atrás da realidade é **auto-curável** — o re-percurso
    custa zero INSERT pela regra "só versiona se difere do vigente". `U` **à frente** é a
    única corrupção que esta tabela permite: ele tira do alcance do worker dias que ninguém
    materializou, e pelo FATO 2 nenhum `eod.ready` os reanuncia.
    **A UNIDADE DE COMPLETUDE É O DIA, NÃO O BATCH — e é isto que decide o que "na mesma
    transação do fim do batch" deixava em aberto.** O handler percorre `[início, D]` em
    **ORDEM CRESCENTE** e, para cada dia `d`, a linha `eod_processado(d)` entra na **MESMA
    transação** que fecha `d` **inteiro** (todos os clientes × todos os instrumentos daquele
    dia): nunca antes dela, nunca num commit que fechou `d` pela metade. O batch é, então,
    **N transações — uma por dia** — e não uma só.
    **A ordem crescente é parte da regra, não detalhe de implementação:** como
    `U = MAX(data_ref)`, materializar fora de ordem faria `U` saltar por cima de dias não
    materializados, que é exatamente a corrupção proibida. Interrupção no meio do intervalo
    deixa `U` no último dia **inteiro** — atrasado, nunca adiantado —, e a reentrega recomeça
    em `U + 1`.
    **Ack e watermark são coisas separadas, e agora sem contradição:** o ack da mensagem
    continua saindo só na **COMPLETUDE do intervalo inteiro** (§10.31); `eod_processado` é
    sobre o **dia**. *A formulação anterior — "a linha entra só no commit que fecha a ÚLTIMA
    unidade do batch, jamais num intermediário" — foi **revogada**: ela tratava o batch como
    unidade de completude, contradizia a condição (iii) abaixo em operação normal (todo dia já
    commitado ficava `> U` enquanto o batch corria) e contradizia o próprio comando
    administrativo, que sempre gravou por dia.*

    **A DIREÇÃO INVERSA, que este arquivo não cobria: `snapshots_posicao` truncada com
    `eod_processado` INTACTA.** Tudo acima é sobre perder a tabela de controle; o caso
    simétrico é pior porque é silencioso. Truncar ou reconstruir `snapshots_posicao` deixando
    `eod_processado` de pé mantém `U` em `D`; o worker nunca volta àqueles dias (`[desde, U]`
    só os alcança se algum gatilho os alcançar, e nada mudou no livro) e, pelo FATO 2, nenhum
    `eod.ready` novo os anuncia — **buraco permanente e silencioso**. É exatamente o que o
    watermark derivado curava por construção, e o que a ADR-5 chama de "botão de reparo": ao
    trocar o derivado pela tabela, esta fase **assume** essa cura, e assumi-la é escrevê-la.
    Três regras, e são desta fase:
    **(i)** truncar `snapshots_posicao` **EXIGE** truncar `eod_processado` na mesma operação
    — a projeção e o watermark dela são um **par**, e separá-los é o modo de falha; isso entra
    no texto do procedimento de reparo, não na cabeça de quem o executa;
    **(ii)** o Pronto desta fase prova a reconstrução **com os dois truncados** e registra,
    como metade negativa, que truncar **só a projeção** NÃO se auto-cura;
    **(iii)** o job de reconciliação que esta fase já tem **alerta quando
    `MAX(snapshots_posicao.data) > U`** — e, com a **unidade de completude sendo o dia**
    (propriedade acima), isto é um **invariante literal a qualquer instante, inclusive com um
    batch em curso**: snapshot de `d` e `eod_processado(d)` são gravados na **mesma
    transação**, então nenhum leitor jamais vê a projeção sem o watermark dela. É por isso que
    o alerta pode ser **incondicional** — ele não precisa saber se há handler rodando, e **não
    deve** precisar: exigir esse conhecimento seria criar um segundo estado de controle, que é
    o antipadrão que esta fase já paga uma vez. O que ele pega é só o que deve pegar: snapshot
    escrito **por fora** do handler e do comando administrativo, ou as duas tabelas mexidas em
    ordens diferentes. *Contraexemplo tentado, e é ele que derrubava a formulação anterior:*
    **batch em curso** — com a unidade sendo o batch, todo dia já commitado ficava `> U`
    enquanto o intervalo corria, e o alerta disparava em operação normal; com a unidade sendo
    o dia, o estado não existe nem por um instante, porque as duas linhas são a **mesma
    transação**. *Segundo contraexemplo tentado:* falha no meio do intervalo — deixa `U` no
    último dia inteiro e `MAX(snapshots.data) = U`, não `> U`. *(iii) pega a direção "`U` atrasado em relação à projeção"; a
    direção deste parágrafo — projeção vazia com `U` intacto — é **prevenida** por (i) e
    **conferida** por (ii), e é por isso que as três andam juntas em vez de uma bastar.*
    **(iv)** a alínea **simétrica**, e ela é nova porque o comando administrativo abriu o
    caminho que ela vigia: o job **alerta também quando `MAX(snapshots_posicao.data) < U` com
    `U` maior que o último `D` conhecido** (o `MAX(data_ref)` anunciado por um `eod.ready`, ou
    o `D` da mensagem estacionada). Esse é o estado "`U` adiantado", que a regra (iii) **não
    vê** — ela vigia `>`, e aqui o sintoma é `<` —, e é o que um `materializar --ate` no futuro
    produziria. *Sem esta alínea, a única corrupção que a tabela permite é a única que nenhum
    detector cobre.*

    **Escopo do desvio, estreito de propósito:** o que está autorizado aqui é **este**
    watermark — `eod_processado`, para o recorte da valoração diária — e só ele. A ADR-5
    continua valendo para qualquer outro watermark da Custódia, e o bootstrap/backfill de
    preço do F6 permanece **derivado**, sem tabela de controle. *Sem esta frase o desvio vira
    precedente genérico, e "a Custódia já tem uma tabela de controle" passa a justificar a
    próxima.*
    *Rejeitado, e fica registrado porque de longe parece boa:* criar um **inbox** de eventos
    consumidos e derivar `U` dele — não há inbox nesta casa (este roadmap registra a
    ausência), e um inbox registraria **CHEGADA**, não **COMPLETUDE**: é a mesma tabela de
    controle com semântica pior, porque `U` passaria a avançar sobre dias cujo batch não
    fechou, que é a única corrupção que a propriedade acima proíbe. *Rejeitado:* tornar o I6
    falso de propósito, gravando linha-sentinela para o dia vazio — exigiria
    cliente/instrumento **fabricados dentro do documento que o cliente lê** (P2), mais filtro
    em toda leitura do F8.
    **A formulação anterior — "reversiona dias já materializados e nunca cria linha" — não
    era um recorte: era uma regra a mais, e ela QUEBRAVA O GATILHO 1 no caso mais comum.**
    Uma compra registrada retroativamente para um período em que o cliente tinha posição
    zero não tem snapshot para "reversionar" (§7.1, convenção 2: só há linha em dia com
    posição ≠ 0) — o worker tem de **criar** as linhas, e aquela regra o proibia. É
    exatamente o cenário que o F4 nomeia como típico: *"para registro manual posição
    negativa costuma significar 'falta lançar a compra antiga'"*. Com **um só código** para
    os três gatilhos, proibir a criação quebraria o gatilho 1 sem tocar no 2.
    *Rejeitado:* manter o `[desde, hoje]` literal da §7.4 e confiar em disciplina para não
    criar dia ainda não fechado — é a regra que a ADR-9 quer no schema do algoritmo, não na
    cabeça de quem implementa, e o `eod.ready` que viesse fechar aquele dia encontraria a
    linha já criada pelo worker, com `calculado_em` mentindo sobre quem a criou.
  - **O CASO QUE O RECORTE TEM DE FECHAR, e com o `eod.ready` one-shot ele é o NORMAL, não a
    exceção: movimento retroativo ou `revisao > 0` chegando DEPOIS de o dia já ter sido
    materializado.** Com `U`, a dicotomia é **exaustiva sobre os dias que o Hub anuncia** (a
    ressalva está escrita logo abaixo) e o intervalo vazio deixa de ser um buraco:
    - **`data_evento ≤ U`** — o dia **está dentro** de `[desde, U]`: o worker o **versiona**
      (mudou) ou o **cria** (não existia). É este ramo que a versão anterior deixava de fora
      quando o dia já materializado era ontem e o recorte parava em `hoje−1` por acaso.
    - **`data_evento > U`** — aquele dia **ainda não foi materializado por ninguém**, porque
      `U` é, por definição, o último que foi. O `eod.ready` que vier a fechá-lo lerá o livro
      **já com o movimento**, e o snapshot nasce certo de primeira. Aqui, e só aqui,
      "intervalo vazio, worker não faz nada" **está correto** — e agora com prova, não com
      uma frase pedindo para não consertar.
    **Nenhum dia fica sem dono — e as fronteiras dessa afirmação estão escritas, porque
    agora são DUAS.** *A versão anterior deste parágrafo dizia "ela tem uma", no singular, e o
    teto — decidido na mesma fase — abriu a segunda sem que a frase acompanhasse.*
    **Fronteira 1: dia que o Hub nunca anuncia.** A dicotomia cobre os dias **até o `D` que o
    Hub anunciar**; ela não promete que o Hub anuncie. *Contraexemplo tentado, e ele
    sobrevive:* se um instrumento ativo parar de receber preço para sempre, o `fechado` do Hub
    trava (FATO 1) e **nenhum** `eod.ready` novo chega — os dias depois de `U` ficam sem
    materializar, e não há nada nesta casa que os crie, por desenho (ADR-9: materializar dia é
    do `eod.ready`). **O detector é o alerta das 12:00 sobre `processado_em`**, e é essa a
    razão de ele existir.
    **Fronteira 2: dia ANUNCIADO cujo intervalo estourou o TETO.** Aqui o `eod.ready(D)` foi
    emitido, o `D` **é** anunciado, e mesmo assim ele fica sem materializar até um operador
    rodar `materializar --desde --ate` e o drenador reentregar a mensagem — a dicotomia `≤ U`
    / `> U` deixa de ser automática **nesse ramo**, e passa a depender de intervenção humana.
    É o desvio (10) da lista canônica, e é ele que torna a afirmação condicional. **O detector
    é o MESMO alerta das 12:00**, e por um motivo que vale escrever: com a mensagem
    estacionada, `processado_em` **não avança**, então o alerta dispara pelo caminho normal —
    não foi preciso inventar sinal novo para a fronteira nova. *Sem estas duas fronteiras
    escritas, "nenhum dia fica sem dono" seria cobertura afirmada sobre dois casos reais e não
    cobertos.* O que a dicotomia garante é, então: **nenhum dia ANUNCIADO E DENTRO DO TETO
    fica sem dono**, que era exatamente o que o `[desde, hoje−1]` não garantia.
  - **O `eod.ready(D)` materializa `[U_anterior + 1, D]`, não só `D` — e isto é a segunda
    metade do fato 2. É um DESVIO da §7.3, rotulado aqui e na lista canônica do F5.** A §7.3
    escreve o ramo no **singular** (`EodPricesReady(D): para cada (cliente, instrumento) com
    posição ≠ 0: snapshot(D) = …`); esta fase põe o handler percorrendo um **intervalo**.
    *Sem o rótulo, o executor que ler a §7.3 — como o próprio prompt manda — encontra a
    contradição sem explicação e tende a seguir a `ARQUITETURA`, que é a mesma regra que
    vale para os outros nove desvios deste arquivo.*
    **Por que o desvio:** como o Hub só emite quando `fechado > ultimoEmitido`, `D` **pula**
    datas (ingestor fora do ar, instrumento atrasado): os dias pulados são dias úteis com
    posição ≠ 0 e **nenhum** evento futuro os anuncia. O handler percorre o intervalo com
    **a mesma função** do worker (§7.4, "um só código"), grava `eod_processado(D)` e o ack
    sai só na COMPLETUDE do conjunto. **Com `U_anterior` nulo** — tabela vazia, perdida ou
    truncada — o intervalo começa em `MIN(data_evento)` do livro, pela definição escrita na
    decisão do `U` acima, **e com o TETO declarado ali** — sem o teto, "começa em
    `MIN(data_evento)`" é o intervalo ilimitado que a mesma decisão nomeia como corrupção;
    **não** é "só `D`". *A formulação anterior ("`U_anterior` nulo → o
    intervalo é só `D`, senão o primeiro `eod.ready` materializaria desde o começo dos
    tempos") tinha um buraco: ela também se aplicava à **perda** da tabela, e aí os dias
    entre o `U` perdido e `D` ficavam sem linha para sempre. `MIN(data_evento)` do livro não
    é "o começo dos tempos" — é o primeiro dia em que existe fato para valorar, e antes dele
    não há posição ≠ 0 para materializar.* *Rejeitado:* materializar só `D` e contar com o worker para os
    pulados — o worker só passa por um dia se **algum** gatilho o alcançar, e um dia pulado
    de um cliente sem movimento retroativo **nunca** ganharia linha: buraco permanente e
    silencioso no extrato de posição do F8, com o `eod.ready` daquela data já queimado.
    *Rejeitado:* um job de varredura de buracos — é uma terceira engrenagem para descobrir o
    que o handler já sabe no momento em que recebe o evento, e a §7.4 diz que a engrenagem é
    uma só. **Consequência de recurso, e ela é real:** o batch passa a ser
    (dias do intervalo × clientes × instrumentos), então a medição com `docker stats` desta
    fase tem de incluir **um `eod.ready` depois de um salto de datas**, não só o dia a dia —
    **e é essa medição que fixa o teto** da decisão do `U`: o primeiro fator do produto é o
    único que pode crescer sem limite, e o teto é o que o limita.
  - **O campo `classes` do `EodPricesReady` (§5.1) é ignorado nesta fase, e a ausência é
    decidida.** Em fase 1 existe **só** `td`, e a ADR-9 adia explicitamente a política de
    classe atrasada ("Fase 2: definir política para classe atrasada — publicar por classe e
    valorar parcial, ou timeout com último ≤ D"). Regra: **não invente valoração parcial** e
    não filtre instrumento por classe a partir desse campo. *Rejeitado:* valorar só os
    instrumentos das classes listadas — inventaria, agora, a política que a ADR-9 mandou
    decidir depois, e o modo de falha seria snapshot faltando em silêncio.
  - **Ack do `eod.ready` só na COMPLETUDE do batch — e o batch agora é (dias do intervalo ×
    clientes × instrumentos).** É mais um laço cuja condição de parada precisa dizer se
    parou por completude ou por limite
    (§10.31, a terceira armadilha do `CLAUDE.md` com outro nome), **e os dois rótulos estão
    escritos: COMPLETUDE = o intervalo inteiro materializado, ack; LIMITE = os dias úteis de
    `[início, D]` passaram do teto, e então o handler nem começa — parking com
    `intervalo_acima_do_teto` e alerta, nunca retentativa.** *Um laço cujo limite superior é
    `MIN(data_evento)` de um livro de anos não tem parada por limite nenhuma, e foi assim que
    "perdê-la degrada" ficou falso até o teto entrar.* **ACK e WATERMARK são coisas separadas,
    e é aqui que a distinção morde:** o **ack** sai só na COMPLETUDE do **intervalo inteiro**;
    a linha **`eod_processado(d)` é por DIA**, e entra na **mesma transação que fecha o dia
    `d` inteiro** (todos os clientes × todos os instrumentos daquele dia), com o percurso em
    **ordem crescente**. O batch é, portanto, **N transações — uma por dia**. Gravar
    `eod_processado(d)` com **o dia** pela metade é que faria `U` avançar sobre o que ninguém
    materializou; gravá-lo com **o intervalo** pela metade é correto e desejável, porque deixa
    `U` no último dia inteiro e a reentrega recomeça em `U + 1`. *A formulação anterior — "a
    linha entra no commit que fecha a ÚLTIMA unidade do batch, jamais num intermediário" — foi
    **revogada**: ela confundia a unidade de completude (o dia) com a unidade de ack (o
    intervalo), contradizia a condição (iii) da reconciliação em operação normal e contradizia
    o comando administrativo, que sempre gravou por dia.*
    *Rejeitado:* ack no início
    com processamento em background — perde o dia inteiro em silêncio se o processo cair, e
    nada distingue isso de um dia sem posição.
  - **Snapshot só é versionado se o valor DIFERE do vigente.** *Rejeitado:* INSERT
    incondicional a cada `eod.ready` — infla a tabela e faz `calculado_em` mentir sobre ter
    havido mudança, destruindo a rastreabilidade que a §7.4 promete de graça.
  - **Dia sem preço ≤ D para instrumento posicionado: NÃO se inventa valor e NÃO se repete o
    preço anterior como observação.** O critério da §7.1 é único — só há linha em dia com
    posição ≠ 0, e dia sem pregão não gera linha — e nunca se mistura com "repete o preço".
    Falta de preço em dia útil com posição é **sinal** (métrica + alerta), não preenchimento.
    *Rejeitado:* forward-fill materializado, proibido nominalmente pela §9 dos padrões.
    **Como o sinal é obtido, e isto é um DESVIO da §10.32 — rotulado, na mesma convenção
    dos outros deste arquivo.** A §10.32 manda a distinção **vir marcada** do produtor, e
    a versão anterior desta decisão dizia que ela vem: *"a §4.5 devolve `dataRef: null` com
    `motivo: sem_preco_ate_a_data`, e o F6 grava essa ausência como tal; esta fase lê a
    marcação"*. **Ela não vem, e a decisão era inexecutável**, por dois fatos de contrato:
    `sem_preco_ate_a_data` é campo da **resposta REST** do `/prices/asof`, e o
    `PriceObserved` da §5.1 **não tem campo `motivo`** — ausência de preço no caminho de
    push não gera evento nenhum —; e o caminho diário desta fase é o handler de
    `eod.ready(D)`, que **não chama o `asof`**: ele lê `preco_atual` e `historico_precos`.
    Não há marcação persistida para ler, e o F6 registra por que criá-la seria pior.
    **Regra desta fase, então:** "não há preço ≤ D para o instrumento X" é derivado por
    **consulta definida** — *não existe linha em `historico_precos` com `data_ref ≤ D` para
    aquele `instrumento_id`* —, e o desvio é rotulado **aqui** para o executor não passar a
    fase re-derivando e relatando que seguiu a marcação. Duas fronteiras que **continuam**
    valendo, e é o que sobra da §10.32 nesta fase: (i) a distinção entre
    `sem_preco_ate_a_data` e `instrumento_desconhecido` **vem marcada** do `asof` e não se
    re-deriva (é do F6); (ii) "houve revisão de preço?" **vem marcado** na coluna `revisao`
    e não se descobre comparando valores (gatilho 2, abaixo). O que se deriva é **só** a
    ausência de linha, que é uma pergunta ao próprio banco e não uma inferência sobre o
    dado alheio.
  - **`caixa:*` fica FORA desta regra, e é a V4 do F3 que decide.** `caixa:BRL` e
    `caixa:a_liquidar` têm preço **1,000000 por definição** — não é observação, não é
    forward-fill —, **nunca** recebem `PriceObserved` e **nunca** são pedidos ao Hub. Duas
    consequências que precisam estar escritas aqui, porque é aqui que elas mordem: elas
    **geram linha de snapshot** (sem o preço por definição, `preco` NOT NULL as impediria
    de existir), e elas **não entram no alerta de preço ausente** — senão a regra acima
    produziria alerta diário permanente, todo dia, para sempre, e ninguém olharia mais
    para ele.
  - **Decisão B, segunda metade: reconciliação ALERTA, nunca corrige.** *Rejeitado:*
    auto-cura — a projeção voltaria a bater e o bug do handler que a fez divergir ficaria
    invisível, que é o oposto do que a reconciliação existe para fazer. Reconstruir só as
    chaves divergentes **é** auto-cura, com outro nome.
  - **Um só código para os três gatilhos**, com a assinatura da §7.4. *Rejeitado:* um
    caminho para trade retroativo e outro para revisão de preço — duplicaria a regra de
    versionamento, que é a parte delicada, em dois lugares que divergem na primeira correção.
  - **O gatilho 1 recebe, nesta fase, um chamador novo que já estava previsto: o job de
    liquidação do F5.** Quando ele insere uma liquidação **retroativa** (catch-up depois de
    uma parada), ele enfileira `recalcular(cliente, instrumento, desde=data_liquidacao)` —
    a chamada já está escrita lá e passa a ter destino aqui. Sem isso, os snapshots dos dias
    perdidos ficariam com `caixa:a_liquidar` **para sempre**, e nada os consertaria.
  - **O worker lê o histórico local por default**, com o `/prices/asof` como caminho
    secundário quando faltar dia no intervalo. *Rejeitado:* sempre REST — poria uma
    indisponibilidade do Hub dentro do caminho de correção de extrato.
  - **Fan-out do gatilho 2 roda FORA do handler do evento**, com teto e enfileiramento.
    *Rejeitado:* recalcular sincronamente dentro do handler — segura o ack, e num host de um
    núcleo o teto de CPU não contém nada, só corta rajada (§10.13); o consumo inteiro para
    atrás de um recálculo grande.
  - **A DURABILIDADE DO FAN-OUT NÃO É DA FILA: É DE UMA VARREDURA DERIVADA. Sem esta decisão,
    "com teto e enfileiramento" significa `Channel<T>` em memória, e um restart perde a
    correção de preço PARA SEMPRE.** A conta é curta e não tem conserto depois: o
    `PriceObserved` já foi **acked** (a decisão acima só tira o fan-out do handler; nada
    segura o ack), o handler de `eod.ready` seguinte percorre `[U_anterior + 1, D]` e **não
    volta** aos dias já materializados, pelo FATO 2 o Hub **não reanuncia** aquela data, e a
    reconciliação compara livro × `posicao_corrente` — **não** snapshot × preço. A revisão
    nunca chega ao documento que o cliente vê (P2), em silêncio, e o Fluxo 5 é metade do
    critério de pronto do item 4 da §9. **É violação direta do P2 da `ARQUITETURA`** ("não
    existem flags de bootstrap nem estado de controle separado dos dados; **crash em qualquer
    ponto se resolve na próxima execução**") — e este arquivo é implacável com gauge em
    memória no drenador e deixaria passar a mesma coisa no caminho de **correção de dado**.
    **Decisão: `varredura de defasagem de snapshot`, DERIVADA, na disciplina do P5.** Ela
    percorre os `(cliente, instrumento, data)` que **têm snapshot vigente** com `data ≤ U`, e
    marca como **defasado** todo aquele em que a projeção é **mais velha que a entrada** que
    deveria tê-la produzido — isto é, quando existe (a) linha de `historico_precos` daquele
    instrumento **com `revisao > 0`**, `data_ref ≤ data` e **`observado_em` posterior ao
    `calculado_em`** do snapshot vigente (cobre o gatilho 2), ou (b) movimento daquela
    chave com `data_evento ≤ data` e **`registrado_em` posterior ao `calculado_em`** (cobre o
    gatilho 1, inclusive a liquidação retroativa do job do F5). Nenhum estado de controle
    novo: as três grandezas comparadas já existem e já são gravadas por quem sabe.
    **O `revisao > 0` da alínea (a) NÃO é filtro de eficiência — é o que mantém a ADR-9
    intacta, e sem ele esta decisão derruba o Pronto (a) desta própria fase.** A varredura
    entrega o **gatilho 2**, e o gatilho 2 é definido sobre `revisao > 0`; uma varredura que
    olhasse **toda** observação faria um `prices.td` **comum** chegando depois do snapshot
    re-versionar o dia — isto é, preço individual disparando re-valoração diária, que é
    exatamente o que a ADR-9 proíbe e o que o Pronto (a) reprova por asserção. *Este é o
    contraexemplo que a primeira redação desta decisão não sobrevivia, e ele fica escrito.*
    **O ESCOPO DO `recalcular` QUE ELA ENFILEIRA É O CLIENTE, NÃO A CHAVE DEFASADA — e sem
    isso ela não conserta o caso que mais motivou a decisão.** Achado um `(cliente,
    instrumento, data)` defasado, a varredura enfileira `recalcular(cliente, i, desde=data)`
    **para cada instrumento `i` daquele cliente** com movimento ou posição no intervalo, e não
    só para o instrumento defasado. *O contraexemplo que obriga a isto é o do F5:* a
    liquidação retroativa torna defasado o snapshot de **`caixa:a_liquidar`**, que existe; mas
    a linha que **falta criar** é a de **`caixa:BRL`**, que naquele dia **não tem snapshot** e
    portanto **não é percorrida**. Recalcular só a chave defasada deixaria o dinheiro fora do
    documento — exatamente o dano que a varredura existe para reparar. O custo é
    O(instrumentos do cliente), que a §11 descreve como minúsculo. *A assinatura do §7.4
    continua a mesma (`recalcular(cliente?, instrumento, desde)`): o que muda é quantas
    chamadas a varredura faz, não a função.*
    **Limite declarado, e ele é o preço de a varredura ser derivada de snapshots existentes:**
    um dia em que o cliente **não tem nenhum** snapshot (posição zero em tudo naquele dia, por
    I6) não é percorrido por chave nenhuma. *Rejeitado, e é a saída óbvia:* usar `movimentos`
    como conjunto dirigente e disparar quando **não existe** snapshot para a data —
    dispararia **para sempre** em todo dia de posição zero, que é o estado normal de qualquer
    venda que zera, e viraria o alerta diário permanente que este arquivo condena. Esse caso
    fica coberto pelos dois caminhos que já existem: a chamada enfileirada (caminho normal) e
    o `eod.ready` que vier a fechar o dia, quando ele for `> U`. **O que a varredura garante é
    a CORREÇÃO DO QUE JÁ FOI MATERIALIZADO**, e é assim que ela está escrita — não "a
    varredura cobre tudo". **A fila em memória continua existindo e continua sendo o caminho normal —
    ela é LATÊNCIA, não garantia**; a varredura é a garantia, e é ela que faz "crash em
    qualquer ponto se resolve na próxima execução" ser verdade aqui.
    **É mais um laço, e a parada é classificada (§10.31, e ele está em I15):** **COMPLETUDE**
    = varri todos os `(cliente, instrumento, data)` com snapshot vigente e `data ≤ U`;
    **LIMITE** = teto de chaves por ciclo → **falha**, nunca sucesso parcial — e o ciclo
    seguinte recomeça, porque a condição é derivada do dado e não se consome.
    **A varredura NÃO é um quarto gatilho e não fura a ADR-9:** ela chama o **mesmo**
    `recalcular`, com o mesmo recorte `[desde, U]`, e portanto **não materializa dia que o
    `eod.ready` não fechou** — é o meio pelo qual os gatilhos 1 e 2 chegam ao destino.
    *Rejeitado:* **fila durável** (tabela de pendências de recálculo, ou republicação no
    broker). Funciona, e foi a outra saída considerada; perde por dois motivos: seria a
    **segunda** tabela de controle desta casa, dentro da fase que já paga um desvio nominal da
    §9 por causa da primeira (`eod_processado`), e "a Custódia já tem tabelas de controle"
    viraria precedente — exatamente o que o *escopo estreito do desvio* existe para impedir; e
    ela registraria **intenção de recalcular**, que é estado a mais para divergir, enquanto a
    varredura pergunta ao dado se o recálculo **aconteceu**. *Rejeitado:* pendurar a
    verificação no job de reconciliação existente — aquele job **alerta e nunca corrige**, por
    decisão B, e misturar um corretor dentro dele apagaria a regra que o define.
    *Rejeitado:* rodar a varredura no mesmo ciclo do `eod.ready` — ela precisa rodar **também
    quando nenhum evento chega**, que é a metade dos casos que ela existe para pegar (crash
    depois do ack, com o Hub em silêncio no dia seguinte).

  **AVISO SOBRE A PROVA DO FLUXO 5 — leia antes de despachar.** A `ARQUITETURA` §13 item 5
  registra que o importador da TD API **descarta correção retroativa** (só INSERE, nunca
  atualiza preço no lugar). Consequência: **`revisao > 0` NÃO tem como nascer da fonte
  `td-api`**, e o Fluxo 5 **não é verificável pelo caminho real**. A prova do gatilho 2 é
  **sintética**, por publicação manual de um `prices.td` com `revisao: 1` no exchange, e
  isso tem que estar escrito na fase — sem o aviso, ou ela fecha com o gatilho 2 nunca
  exercitado (código escrito, testes verdes, caminho nunca rodado), ou alguém passa um dia
  procurando por que o Hub "nunca publica revisão".

  **NÃO ENTRA:** extrato (F8 — o snapshot existe no banco e se confere por SQL); corpaction
  e o gatilho 3 (F9 — ele cai no gatilho 1 por construção e não ganha código novo).

  **Configuração: nenhuma** das cinco listas muda — `eod_processado` é **tabela local**,
  criada por migration desta fase, não configuração; e os **três ajustes numéricos desta
  fase** entram em `appsettings` com default no código: o **teto do intervalo do handler**
  (90 dias úteis), o **teto de chaves por ciclo da varredura de defasagem** e a **cadência**
  dela. Os três são ajuste de aplicação e **não segredo** — as cinco listas são de credencial
  e endpoint, e nenhum dos dois muda aqui; **o que não é configurável, nos três, é a
  existência deles**. **Muda o perfil de recurso** (acima), e a varredura entra na medição:
  ela é O(chaves com snapshot vigente ≤ `U`) por ciclo, que é a maior varredura periódica
  deste roadmap.

  **O alerta desta fase ("dia útil sem `eod.ready` até 12:00", mais o de preço ausente) só
  existe quando chega à nuvem** — procedimento no `LEIA-ME-KIT.md`, seção "No repo do
  `tesouro-direto`".

  **Âncoras:** `ARQUITETURA` §7.4 inteira (assinatura única, três gatilhos, o algoritmo como
  função pura sobre dois livros imutáveis), §7.3 (ramo EodPricesReady: "um batch, todos os
  clientes; é o ÚNICO gatilho da valoração diária"), §7.1 (snapshot versionado, `vigente`, só
  em dias com posição ≠ 0), §8.3 e §8.6 (Fluxos 2 e 5 — "cinco fluxos, duas engrenagens"),
  **§13 item 5**, §11, §12, **ADR-5** (o desvio desta fase), ADR-9; `PADROES` §3 (DDL,
  snake_case, upsert idempotente por chave natural, índices nomeados), §9 (forward-fill
  materializado é antipadrão; estado de controle duplicando dados é antipadrão — é o que esta
  fase desvia, rotulado),
  §10.8, §10.12, §10.13, §10.14, §10.31, §10.32, §10.33, **§10.34** (o fuso do alerta das
  12:00), **§10.35** (fila em memória não é durabilidade — é ela que sustenta a varredura de
  defasagem); `LEIA-ME-KIT` "Dimensionar recurso sem medir, e chamar o número de folgado" e
  "A cláusula que você ACRESCENTA não passa pela mesma revisão da que você corrige".

  **Prompt:**
  ```
  Implemente o handler de EodPricesReady, os snapshots versionados e o worker de
  recalculo (ARQUITETURA 7.3 ramo EodPricesReady, 7.4 INTEIRA, Fluxos 1, 2 e 5).

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  ENTRA:
  - handler de eod.ready(D): UM BATCH, todos os clientes com posicao != 0, sobre o
    intervalo [U_anterior + 1, D] (decisao 1c), percorrido em ORDEM CRESCENTE e gravando
    eod_processado(d) PARA CADA DIA `d`, na MESMA TRANSACAO que fecha aquele dia inteiro
    (todos os clientes x todos os instrumentos daquele dia). A UNIDADE DE COMPLETUDE E O
    DIA, NAO O BATCH: o batch e N TRANSACOES, uma por dia. O ACK e que sai so na
    COMPLETUDE DO INTERVALO INTEIRO — ack e watermark sao coisas separadas.
    snapshot(D) = A DOBRA DA V1 DO F3 COM CORTE EM data_evento <= D, nas TRES colunas
    (quantidade, preco_medio, custo_total), x preco(ultimo <= D). NAO COPIE as tres
    colunas de posicao_corrente: elas sao a MESMA dobra com corte D = INFINITO, ou seja,
    o preco medio e o custo de HOJE — copia-los para um dia passado grava quantidade de D
    com valor de hoje, viola custo_total = preco_medio x quantidade em TODO snapshot
    historico e faz um movimento retroativo reescrever o preco medio da serie inteira, no
    documento que o cliente ve. Linha SO em dia util com
    posicao != 0; dia sem pregao nao gera linha. O calendario de dias uteis e o do F5.
    AS LINHAS caixa:BRL E caixa:a_liquidar ENTRAM NO SNAPSHOT, com preco = 1,000000 POR
    DEFINICAO (V4 do F3) — snapshots_posicao.preco e NOT NULL, entao sem isso NAO
    NASCERIA linha de caixa nenhuma: o F5 escritura o limbo D->D+1 no livro e esta fase o
    apagaria do documento, deixando o extrato de posicao do F8 sem a parcela de caixa. NAO
    USE a igualdade "soma dos instrumentos + caixa = patrimonio diario" (7.5) como criterio:
    e ela que a PENDENCIA BLOQUEANTE do caixa:BRL (F3, V2) deixa em aberto, e o F8 Pronto (g)
    ja deixou de afirma-la. O que esta fase precisa e so que A LINHA DE CAIXA EXISTA no
    snapshot.
  - snapshots_posicao versionado: marca vigente=false e INSERE a nova com calculado_em.
    Versiona SO SE o valor DIFERE do vigente.
  - worker recalcular(cliente?, instrumento, desde) da 7.4: UM SO CODIGO para os tres
    gatilhos E PARA O HANDLER DE eod.ready, percorrendo [desde, U] e inserindo QUANDO
    DIFERE DO VIGENTE OU QUANDO NAO EXISTE LINHA (decisao 1b). U = MAX(data_ref) de
    eod_processado, NUNCA uma data de relogio. Gatilho 1 = movimento com data_evento no
    passado (ligue o que o F4 deixou so registrado). Gatilho 2 = PriceObserved com
    revisao > 0, escopo TODOS os clientes posicionados x 1 instrumento (ligue o que o
    F6 deixou MARCADO no dado — nao re-derive "houve revisao?" comparando valores, PADROES 10.32).
  - job de reconciliacao livro x posicao_corrente que ALERTA e NAO CORRIGE, comparando AS
    TRES COLUNAS (quantidade, preco_medio, custo_total) — e ele usa A MESMA DOBRA DA V1 DO
    F3, COM CORTE D = INFINITO, que o handler (corte D = o dia) e o comando de
    reconstrucao (corte D = infinito) usam, NUNCA uma terceira implementacao: duas
    dobras divergem na primeira correcao aplicada em uma so, e a reconciliacao passaria a
    alertar sobre a propria divergencia. E UMA FUNCAO COM UM PARAMETRO DE CORTE, nao duas
    funcoes. E ELE ALERTA NAS DUAS DIRECOES DE U CONTRA A PROJECAO, E SAO DUAS ALINEAS
    SEPARADAS PORQUE PEGAM COISAS OPOSTAS:
      (iii) MAX(snapshots_posicao.data) > U — snapshot escrito POR FORA do handler e do
            comando, ou as duas tabelas mexidas em ordens diferentes. Com a unidade de
            completude sendo o DIA, isto e invariante literal A QUALQUER INSTANTE, INCLUSIVE
            COM BATCH EM CURSO (snapshot de d e eod_processado(d) sao a MESMA transacao),
            entao o alerta e INCONDICIONAL — ele NAO precisa saber se ha handler rodando, e
            NAO DEVE precisar: exigir isso seria criar um segundo estado de controle.
      (iv)  MAX(snapshots_posicao.data) < U COM U MAIOR QUE O ULTIMO `D` CONHECIDO (o maior
            entre MAX(data_ref) anunciado por um eod.ready e o D da mensagem estacionada).
            E o estado "U ADIANTADO", que a alinea (iii) NAO VE — ela vigia `>`, e aqui o
            sintoma e `<`. E o unico detector da unica corrupcao que esta tabela permite, e
            e o que um `materializar --ate` no futuro produziria. A comparacao e deterministica
    porque I4 NAO tem filtro por relogio (o corte infinito) — e a decisao da liquidacao por
    job, no F5, que preserva isso.
  - comando administrativo DENTRO DO CONTAINER, `materializar --desde --ate`, SEM ENDPOINT
    (ADR-10) — e o caminho de bootstrap FORA do handler de evento para o caso "livro grande
    com eod_processado vazia", e e ele que faz o eod.ready estacionado por
    intervalo_acima_do_teto voltar a caber no teto (decisao 1b). Mesma familia do comando de
    reconstrucao de posicao_corrente do F4.
    O `--ate` TEM GUARDA, E ELA NAO E OPCIONAL: ESTE COMANDO E O UNICO ESCRITOR CAPAZ DE
    ADIANTAR `U`. `materializar --desde 2026-01-01 --ate 2026-12-31` (uso plausivel — o
    comando existe para intervalo grande) gravaria eod_processado ate dezembro, e a partir
    dai (1) o handler calcula [U_anterior + 1, D] com U_anterior > D, isto e, intervalo
    VAZIO OU INVERTIDO: o batch "fecha" por vacuidade e nenhum eod.ready materializa mais
    nada, PARA SEMPRE; (2) o worker percorre [desde, U] ate uma data FUTURA, materializando
    dias que nenhum eod.ready fechou — que e o que o recorte existe para impedir (ADR-9).
    REGRA: RECUSE `--ate` maior que o ULTIMO `D` ANUNCIADO (o maior entre MAX(data_ref) de
    eod_processado e o D da mensagem estacionada com intervalo_acima_do_teto, se houver),
    com FALHA ALTA, mensagem dizendo qual e o limite e por que, e NADA GRAVADO. O detector
    complementar e a alinea (iv) da reconciliacao, acima — validacao de argumento nao cobre
    UPDATE manual nem binario antigo.
    PARADA CLASSIFICADA (PADROES 10.31), porque isto e um laco e ele esta em I15:
    COMPLETUDE = o intervalo inteiro pedido materializado, dia a dia, em ORDEM CRESCENTE;
    LIMITE = interrupcao do operador, falha no meio, ou recusa do --ate -> FALHA COM CODIGO
    DE SAIDA NAO-ZERO, nunca "materializei o que deu". ISTO NAO ACRESCENTA VALOR A LISTA DE
    x-custodia-motivo: aquela lista e de MENSAGEM ESTACIONADA no broker, e este comando nao
    estaciona mensagem nenhuma — o desfecho dele e codigo de saida e log. Interromper no
    meio e SEGURO por construcao: como cada dia commita inteiro e em ordem crescente, U fica
    no ultimo dia INTEIRO e a proxima execucao recomeca de onde parou.
  - alerta de "dia util sem eod.ready ate 12:00" (ARQUITETURA 12), lido da coluna
    eod_processado.processado_em — NUNCA de data_ref: data_ref e propriedade do DADO DA
    FONTE e nao responde a pergunta da secao 12 ("chegou evento hoje?"), e ela erra nos DOIS
    sentidos (decisao 1b, com os dois contraexemplos escritos la). NAO ESCREVA "data_ref
    nunca e hoje": isso e absoluto falso, e o contraexemplo (ii) da decisao 1b — uma fonte
    futura que publique preco do PROPRIO dia — o derruba. E NUNCA de um gauge em memoria, que zeraria no deploy (o mesmo defeito
    que o F4 rejeita no drenador).
  - drenagem do custodia.parked de DOIS motivos, pelo drenador e pela mecanica do F4
    (parada classificada, sem inspecionar routing key): `tipo_nao_tratado_eod` (o backlog
    acumulado desde o F2) e `intervalo_acima_do_teto` (o motivo que ESTA fase cria, na
    decisao 1b — a passagem dele e metade do Pronto (f2), e sem ela "parar" e "perder" ficam
    indistinguiveis). As DUAS passagens tem que devolver COMPLETUDE com n_motivo >= 1.

  DECISOES JA TOMADAS:
  1. ADR-9, COM O QUALIFICADOR INTEIRO: eod.ready e o UNICO gatilho da VALORACAO DIARIA
     (7.3, palavra por palavra). Preco individual NUNCA materializa o snapshot do dia,
     nem "so para o instrumento que mudou". A EXCECAO NOMEADA e o GATILHO 2 da 7.4, que
     VOCE IMPLEMENTA nesta fase: revisao > 0 dispara recalcular(TODOS os posicionados,
     desde=dataRef). Se voce ler "unico gatilho de valoracao" sem o "diaria" e nao
     implementar o gatilho 2, o FLUXO 5 NAO EXISTE — e ele e metade do criterio de pronto
     do item 4 da secao 9.
  1a. DOIS FATOS SOBRE O eod.ready, MEDIDOS NO CODIGO DO PRODUTOR (../hub-precos). NAO
     PRESUMA NADA ALEM DELES, E NAO PRECISA RE-MEDIR:
       FATO 1: O `D` DO eod.ready(D) NAO E UMA DATA DE RELOGIO, E NA TD VEM DEFASADO.
         hub-precos/src/Hub.Infrastructure/Persistence/Repositories/IngestaoReadRepository.cs
         linhas 37-47: SqlDataEodFechado = SELECT MIN(wm) FROM (SELECT MAX(p.data_ref) AS
         wm ... GROUP BY i.id) t, sobre os instrumentos ATIVOS da classe TD. D e, entao,
         O MENOR DOS ULTIMOS PRECOS: para cada instrumento ativo da classe TD QUE TEM
         ALGUM PRECO, o MAX(data_ref) dele; D e o MENOR desses maximos — derivado do DADO,
         nunca do relogio. NAO ESCREVA "a data mais recente em que todos os instrumentos
         ativos que tem algum preco tem preco": isso e mais forte do que o SQL. MIN de
         maximos garante que NENHUM instrumento tem o ultimo preco ANTES de D, nao que cada
         um tenha preco EM D (contraexemplo: A com precos em D-1 e D+1, B com preco em D ->
         MIN(MAX) = D e A nao tem preco em D). Inofensivo aqui, porque a valoracao e
         preco(ultimo <= D) e nao preco(em D) — mas NAO escreva teste afirmando "todo
         instrumento tem preco em D": ele falha por acaso de dado. Como a TD publica o fechamento do dia anterior
         por volta das 06:00 BRT, D e tipicamente hoje-1, e mais antigo em segunda, feriado
         ou com instrumento atrasado.
         O QUALIFICADOR IMPORTA: aquele SQL usa JOIN precos, NAO LEFT JOIN (linha 43),
         entao instrumento ativo SEM NENHUM PRECO e EXCLUIDO do MIN em vez de anula-lo. O
         Hub cobre isso com SqlDataEodAtivosSemPreco (linhas 54-63), que so gera WARNING e
         NAO BLOQUEIA a emissao (IngerirPrecosTdCommandHandler.cs:339-345). Logo
         eod.ready(D) PODE CHEGAR com instrumento ativo sem preco algum — e o caso do
         Pronto (k): SINAL, nunca preco fabricado.
         E HA UM TERCEIRO QUALIFICADOR, sobre o CONJUNTO de que o MIN e tomado: o WHERE/JOIN
         daquele SQL (linhas 42 e 44) restringe por instrumento_fontes.fonte = @fonte E por
         ativo_ate >= @hoje — um parametro de RELOGIO. Isso NAO torna D uma data de relogio
         (a assercao principal sobrevive), mas o conjunto do MIN depende de `hoje` e da
         fonte, e um instrumento ativo SEM LINHA em instrumento_fontes para aquela fonte some
         do MIN E do SqlDataEodAtivosSemPreco — nem entra na conta nem gera o warning. NAO
         ESCREVA "sao dois qualificadores": sao tres, e a consequencia pratica e a mesma do
         segundo (o Pronto k).
       FATO 2: eod.ready(D) E EMITIDO UMA VEZ POR DATA, PARA SEMPRE.
         hub-precos/src/Hub.Application/Ingestao/IngerirPrecosTdCommandHandler.cs linha
         337 so emite se fechado > eod.UltimoEmitido; e a migration
         hub-precos/.../20260822231916_CriaIndiceUnicoOutboxEod.cs linha 15 cria
         CREATE UNIQUE INDEX ux_outbox_eod_data ON outbox ((payload->>'data'))
         WHERE tipo = 'EodPricesReady'. NAO HA REENTREGA para a mesma data, e o D PODE
         PULAR DATAS (se fechado salta de 08-10 para 08-14, os dias 11, 12 e 13 nunca sao
         anunciados).
  1b. O MECANISMO QUE FAZ AS DUAS REGRAS CONVIVEREM E RECORTE DE INTERVALO, E ELE E UM
     DESVIO PARCIAL DA 7.4 — REGISTRE-O, igual ao ref_externa NOT NULL do F3:
       o worker percorre [desde, U] e INSERE QUANDO DIFERE DO VIGENTE **OU QUANDO
       NAO EXISTE LINHA**, com U = O ULTIMO DIA JA MATERIALIZADO POR ESTA CASA =
       MAX(data_ref) de eod_processado.
     A 7.4 escreve "para cada dia util D em [desde, hoje]" e "se difere do vigente (ou NAO
     EXISTE LINHA)": o que muda e SO O LIMITE SUPERIOR, de `hoje` para U; a clausula "nao
     existe linha" fica LITERAL. POR QUE MUDA: materializar dia e do eod.ready (ADR-9) e,
     pelo FATO 1, o dia que ele materializa NAO E UMA DATA DE RELOGIO — escrever `hoje` ou `hoje-1` no
     algoritmo e escrever um palpite sobre o relogio de outro servico.
     eod_processado E A DDL NOVA DESTA FASE (nao "a unica tabela nova do roadmap": o F5
     tambem cria uma, o CALENDARIO DE DIAS UTEIS, que e configuracao semeada por
     migration). UMA LINHA POR DIA MATERIALIZADO — nao uma por D anunciado —, gravada na
     MESMA TRANSACAO que fecha aquele dia inteiro, pelo handler ou pelo comando
     administrativo, que aqui fazem a MESMA coisa. Um eod.ready(D) que materializa [D-4, D]
     deixa CINCO linhas, e a de D exatamente uma. NAO ESCREVA "uma linha por D cujo batch
     fechou em COMPLETUDE": essa formulacao descrevia outra tabela e fazia a alinea (iii) da
     reconciliacao alertar em operacao normal. A DDL INTEIRA, JA DECIDIDA — NAO A REDESENHE:
       eod_processado (
         data_ref      date        PRIMARY KEY,
         processado_em timestamptz NOT NULL
       )
     snake_case, migration EF, molde ../operacoes/src/Operacoes.Infrastructure/Persistence/.
     PAPEIS SEPARADOS, e confundi-los torna o alerta da secao 12 inimplementavel:
       data_ref date            -> a data DO DADO (o D do evento). E DELA, E SO DELA, QUE
                                   SAI U = MAX(data_ref).
       processado_em timestamptz -> a hora DE RELOGIO em que ESTE servico fechou o batch.
                                   E DELA, E SO DELA, QUE SAI O ALERTA DAS 12:00.
     A PRIMARY KEY (data_ref) NAO E ZELO: o Pronto (c) exige que a reentrega deixe
     eod_processado(D) COM UMA LINHA, e isso e CONSTRAINT, nao disciplina de handler — sem
     ela duas reentregas concorrentes gravam duas linhas e nada reclama. Grave com
     INSERT ... ON CONFLICT (data_ref) DO NOTHING (PADROES secao 3, upsert idempotente por
     chave natural). DO NOTHING E ESCOLHA: processado_em e a hora em que aquele D foi
     materializado PELA PRIMEIRA VEZ, que e o que o alerta das 12:00 quer saber; um
     DO UPDATE SET processado_em = now() faria a reentrega DO BROKER adiar o alerta sem que
     nenhum dado novo tivesse chegado. INDICES: SAO DOIS PADROES DE ACESSO NOVOS e a secao 3
     e literal ("todo padrao de acesso novo exige indice correspondente na mesma migration").
     MAX(data_ref) e servido pela PRIMARY KEY; MAX(processado_em) — a leitura do alerta das
     12:00 — ganha ix_eod_processado_processado_em NA MESMA MIGRATION, no padrao de nome
     ix_tabela_colunas. NAO pule o segundo alegando que a tabela e pequena: a secao 3 nao
     abre excecao por tamanho.
     O ALERTA "dia util sem eod.ready ate 12:00" (ARQUITETURA 12) E SOBRE processado_em E
     NUNCA SOBRE data_ref, PORQUE PELO FATO 1 data_ref NAO RESPONDE A PERGUNTA DA SECAO 12.
     A secao 12 pergunta "CHEGOU EVENTO HOJE?" (chegada), e data_ref e propriedade do DADO
     DA FONTE: na TD ela vem defasada (tipicamente hoje-1), entao MAX(data_ref) < hoje e
     verdade em todo dia util normal e o alerta dispararia TODO DIA, PARA SEMPRE. E se um
     dia uma fonte publicar preco do PROPRIO dia, data_ref viraria hoje e o alerta NUNCA
     dispararia, mesmo com o Hub mudo desde ontem — o mesmo campo errando nos dois sentidos.
     A REGRA E: MAX(processado_em) < hoje 12:00 num dia util — E "hoje", "dia util" e
     "12:00" SAO NO FUSO America/Sao_Paulo, decidido no F3 (item 5e) e herdado aqui. A REGRA
     DO ALERTA CARREGA O OFFSET EXPLICITAMENTE, porque o avaliador do Grafana Cloud roda em
     UTC: sem isso o alerta dispara (ou deixa de disparar) por tres horas todo dia, e o sinal
     erra na janela em que o Hub publica (06:00 BRT).
     ESTA TABELA E UM DESVIO DA ADR-5 E DO ANTIPADRAO NOMINAL DA SECAO 9 DO PADROES
     ("estado de controle duplicando dados — derive dos dados, padrao
     watermark/reconciliacao") — ROTULADO, e esta na lista canonica de desvios (no F5). E O
     UNICO DESVIO DESTE ROADMAP QUE VIOLA UM ANTIPADRAO NOMINAL, entao ele carrega a
     clausula inteira: JA APROVADO PELO ADVISOR com as quatro condicoes escritas nesta
     decisao (U atrasa e nunca adianta, com A UNIDADE DE COMPLETUDE SENDO O DIA: a linha
     eod_processado(d) entra na MESMA TRANSACAO que fecha o dia `d` inteiro, percorrido em
     ordem crescente — a formulacao anterior, "so no commit que fecha a ULTIMA unidade do
     batch", FOI REVOGADA pelo advisor porque tratava o batch como unidade de completude e
     contradizia a alinea (iii) da reconciliacao em operacao normal; a regra do par
     snapshots_posicao <-> eod_processado; a DDL com PRIMARY KEY (data_ref); e o escopo
     estreito), e VOCE GRAVA NA MEMORIA ao fechar a fase — igual ao cursor do F8.
     O ARGUMENTO QUE SUSTENTA O DESVIO E O I6 (os outros dois que estavam escritos aqui nao
     sustentavam — veja os NAO USE abaixo): so existe linha de snapshot em
     dia util com posicao != 0, entao um dia materializado em que NENHUM cliente tinha
     posicao NAO DEIXA LINHA em snapshots_posicao — MAX(data) dali nao distingue
     "materializado e vazio" de "nao materializado", o derivado TRAVA naquele dia e o
     proximo eod.ready re-materializa um intervalo que so cresce. A PREMISSA DA ADR-5 E "o
     derivado nao pode mentir", E AQUI ELA E FALSA: o derivado mente devolvendo uma data
     plausivel. E por isso que isto e DESVIO e nao aplicacao da ADR — as duas condicoes de
     migracao que ela LISTA (a opcao (b) incomodar, ou o MAX pesar) nao sao esta.
     NAO USE o argumento antigo ("TRUNCATE mudaria U"): a ADR-5 aceita esse efeito com
     todas as letras ("apagar forca re-backfill — e o botao de reparo, documentar"), e
     rejeitar o derivado porque o reparo funciona e rejeitar a ADR pelo motivo que ela
     declara aceitavel. E NAO CITE A 10.32 aqui: ela e sobre PRODUTOR marcando distincao
     para CONSUMIDOR; aqui e o consumidor registrando o proprio estado, que e o objeto da
     secao 9. eod_processado NAO e projecao do livro: e registro de consumo de evento de
     contrato, MUTAVEL, sem trigger de imutabilidade, fora do alcance da 10.21 — e por isso
     ela nasce nesta fase e nao no F3.
     PERDE-LA DEGRADA E NAO CORROMPE, MAS SO COM O TETO ABAIXO E SO PORQUE "U NULO" TEM
     SIGNIFICADO DEFINIDO. E SAO DUAS METADES, UMA POR CONSUMIDOR DA TABELA — ESCREVER UMA
     SO DEIXA A OUTRA SEM REGRA, QUE FOI O QUE A VERSAO ANTERIOR DESTE PROMPT FEZ:
       HANDLER de eod.ready(D): U_anterior nulo e LIMITE INFERIOR -> o intervalo COMECA em
         MIN(data_evento) DO LIVRO, nao em D. Assim "perdi a tabela" e "primeira execucao
         de todas" produzem o MESMO comportamento correto e o handler NAO PRECISA
         DISTINGUI-LAS; e como so se versiona quando difere do vigente, cada dia ja correto
         custa ZERO INSERT.
       WORKER recalcular: U nulo e LIMITE SUPERIOR -> o intervalo [desde, U] e VAZIO e o
         WORKER NAO FAZ NADA ate o proximo eod.ready regravar a linha. ISSO NAO PERDE
         CORRECAO: o eod.ready seguinte re-percorre [MIN(data_evento), D] COM A MESMA
         FUNCAO e cria ou versiona exatamente os dias que o worker teria tocado — o que a
         perda custa ao gatilho 2 e LATENCIA, nunca conteudo.
     NAO ESCREVA SO A PRIMEIRA METADE: "materializar a partir de MIN(data_evento)" e limite
     INFERIOR e fala do HANDLER; sem a segunda, o worker fica com [desde, ???] e voce vai
     percorrer ate `hoje`, que e exatamente o desvio da 7.4 que esta fase existe para
     evitar. Com a regra antiga ("U nulo -> intervalo e so D"), perder a
     tabela com U = D-4 deixaria D-3..D-1 SEM LINHA PARA SEMPRE, porque pelo FATO 2 nenhum
     eod.ready futuro anuncia esses dias — isso e CORROMPER, nao degradar.
     REJEITADO: registro de bootstrap explicito para distinguir tabela vazia de primeira
     execucao — e a FLAG DE BOOTSTRAP que a secao 9 do PADROES proibe PELO NOME, dentro da
     mesma decisao que ja esta desviando da ADR-5. REJEITADO: um INBOX de eventos
     consumidos para derivar U dele — nao ha inbox nesta casa, e inbox registra CHEGADA e
     nao COMPLETUDE (U avancaria sobre dia cujo batch nao fechou, a unica corrupcao que a
     regra "U atrasa, nunca adianta" proibe). REJEITADO: linha-sentinela para o dia vazio,
     tornando o I6 falso de proposito — exigiria cliente/instrumento FABRICADOS dentro do
     documento que o cliente le (P2) e filtro em toda leitura do F8. LIMITE DECLARADO: com
     o livro vazio, MIN(data_evento) e nulo e o intervalo e so D — o que e o certo, porque
     sem movimento nao ha posicao != 0 para materializar.
     TETO DO INTERVALO DO HANDLER, E SEM ELE "DEGRADA" E FALSO: o F4 entra em producao
     antes desta fase e acumula livro, entao com eod_processado perdida MIN(data_evento)
     pode ser DE ANOS ATRAS e o batch vira (anos de dias uteis x clientes x instrumentos)
     dentro de um handler que so da ack na COMPLETUDE (decisao 2). O consumer_timeout do
     RabbitMQ fecha o canal, a mensagem volta, o handler RECOMECA O MESMO BATCH — laco de
     reentrega que nunca fecha, eod_processado(D) nunca gravado e, pelo FATO 2, nenhum
     eod.ready futuro reanuncia aquele D. ISSO E CORROMPER. REGRA: conte os DIAS UTEIS de
     [inicio, D] ANTES de comecar; passando do teto o handler NAO PROCESSA — devolve
     LIMITE (PADROES 10.31), ALERTA, e a mensagem vai para a custodia.parked pelo
     custodia.parking com o motivo `intervalo_acima_do_teto` (valor novo, ja nomeado na
     lista fechada do F4). NUNCA nack em laco e NUNCA ack-e-descarta. O teto vem de
     IConfiguration, DEFAULT 90 DIAS UTEIS (um eod.ready normal traz UM dia; um salto
     operacional plausivel traz menos de dez), e a MEDICAO DESTA FASE confirma ou corrige
     o numero — o que NAO e configuravel e a EXISTENCIA do teto. Isso nao mexe em nenhuma
     das cinco listas de configuracao: e appsettings com default no codigo, nao segredo.
     CAMINHO DE REPARO, porque parar nao pode virar perder: o bootstrap de livro grande se
     faz FORA DO HANDLER DE EVENTO, por comando administrativo dentro do container —
     `materializar --desde --ate`, mesma familia do comando de reconstrucao de
     posicao_corrente (decisao B do F4) e do drenador do parking, SEM ENDPOINT (ADR-10).
     O COMANDO NAO ESTA SUJEITO AO TETO (e explicito, tem operador olhando, e e o caminho
     para intervalo grande) e GRAVA eod_processado PARA CADA DIA QUE MATERIALIZA, pela mesma
     regra do handler. Rodado o comando, U avanca, e a passagem do drenador do
     motivo intervalo_acima_do_teto reentrega o eod.ready(D) estacionado, que agora cabe no
     COMMIT POR DIA E ADOTADO (e a unidade de completude desta fase), MAS ELE NAO
     SUBSTITUI O TETO — e o argumento antigo deste rejeitado ("a reentrega nao progride
     porque eod_processado(D) so entra no ultimo commit") CAIU JUNTO COM AQUELA REGRA, alem
     de ser CIRCULAR (rejeitava a alternativa citando a regra que a alternativa propunha
     mudar). PROGREDIR NAO E CABER, por tres razoes independentes: (i) o ack so sai na
     completude do intervalo, e custodia.prices e a MESMA FILA que carrega trades.registered
     (7.3) — um batch de anos avancando um dia por reentrega mantem a fila inteira parada
     atras dele, e o registro de operacoes para junto; (ii) num host de 1 NUCLEO e sob o teto
     de memoria desta fase, isso e um laco quente de reentregas competindo com os vizinhos
     (PADROES 10.14) por horas, INDISTINGUIVEL DE LENTIDAO para quem olha de fora;
     (iii) PADROES 10.31: um laco cuja unica parada e a exaustao do intervalo NAO TEM PARADA
     POR LIMITE NENHUMA — o teto e o que da NOME a condicao, e o nome
     (intervalo_acima_do_teto, com alerta e parking) e o que transforma "travado em silencio"
     em "estacionado e visivel".
     A PROPRIEDADE DA QUAL TUDO ISSO E CONSEQUENCIA: U PODE ATRASAR, NUNCA ADIANTAR. U
     atras e AUTO-CURAVEL (re-percurso custa zero INSERT pela regra "so versiona se
     difere"); U A FRENTE e a unica corrupcao desta tabela — tira do alcance do worker dias
     que ninguem materializou (e o UNICO escritor capaz de produzi-la e o comando
     `materializar`, por isso a guarda do --ate).
     A UNIDADE DE COMPLETUDE E O DIA, NAO O BATCH: percorra [inicio, D] em ORDEM CRESCENTE e
     grave eod_processado(d) na MESMA TRANSACAO que fecha o dia `d` INTEIRO (todos os
     clientes x todos os instrumentos daquele dia) — nunca antes dela, nunca num commit que
     fechou `d` pela metade. O batch e N TRANSACOES, UMA POR DIA. A ORDEM CRESCENTE E PARTE
     DA REGRA: como U = MAX(data_ref), materializar fora de ordem faria U SALTAR por cima de
     dias nao materializados, que e a corrupcao proibida. Interrupcao no meio do intervalo
     deixa U no ultimo dia INTEIRO — atrasado, nunca adiantado — e a reentrega recomeca em
     U + 1. ACK E WATERMARK SAO COISAS SEPARADAS: o ack sai so na COMPLETUDE DO INTERVALO
     INTEIRO; eod_processado e sobre o DIA. NAO ESCREVA "a linha entra so no commit que
     fecha a ultima unidade do batch": essa regra FOI REVOGADA.
     A DIRECAO INVERSA, E ELA NAO ESTAVA COBERTA: snapshots_posicao TRUNCADA com
     eod_processado INTACTA. U continua em D, o worker nunca volta aqueles dias e pelo FATO
     2 nenhum eod.ready novo os anuncia = BURACO PERMANENTE E SILENCIOSO — era o que o
     watermark derivado curava por construcao ("botao de reparo" da ADR-5), e trocar o
     derivado pela tabela obriga a escrever a cura. TRES REGRAS:
       (i) TRUNCAR snapshots_posicao EXIGE TRUNCAR eod_processado NA MESMA OPERACAO — a
           projecao e o watermark dela sao um PAR, e isso entra no TEXTO do procedimento de
           reparo, nao na cabeca de quem executa;
       (ii) o Pronto prova a reconstrucao COM OS DOIS TRUNCADOS e registra, como metade
           negativa, que truncar SO A PROJECAO nao se auto-cura;
       (iii) o job de reconciliacao ALERTA quando MAX(snapshots_posicao.data) > U, e com a
           unidade de completude sendo o DIA isso e INVARIANTE LITERAL A QUALQUER INSTANTE,
           inclusive com batch em curso (snapshot de d e eod_processado(d) sao a MESMA
           transacao) — por isso o alerta e INCONDICIONAL;
       (iv) e a alinea SIMETRICA: ALERTA quando MAX(snapshots_posicao.data) < U COM U MAIOR
           QUE O ULTIMO `D` CONHECIDO. E o estado "U adiantado", que a (iii) nao ve, e o
           unico detector da unica corrupcao que esta tabela permite.
     ESCOPO DO DESVIO, ESTREITO DE PROPOSITO: o que esta autorizado e ESTE watermark
     (eod_processado, para o recorte da valoracao diaria) e so ele. A ADR-5 continua
     valendo para qualquer outro watermark da Custodia — o bootstrap/backfill de preco do
     F6 permanece DERIVADO, sem tabela de controle. Sem esta frase o desvio vira precedente
     generico.
     NAO ESCREVA "so reversiona dias ja materializados", como dizia a versao anterior
     deste prompt: isso QUEBRA O GATILHO 1 no caso mais comum. Uma compra registrada
     retroativamente para um periodo em que o cliente tinha posicao ZERO nao tem snapshot
     para reversionar (7.1, convencao 2: so ha linha em dia com posicao != 0) — o worker
     tem que CRIAR as linhas. E com UM SO CODIGO para os tres gatilhos, proibir a criacao
     quebra o gatilho 1 sem tocar no 2.
     O CASO QUE ISSO FECHA, E COM O FATO 2 ELE E O NORMAL E NAO A EXCECAO: movimento
     retroativo ou revisao > 0 chegando DEPOIS de o dia ja ter sido materializado. Com U a
     dicotomia e EXAUSTIVA SOBRE OS DIAS QUE O HUB ANUNCIA (se um instrumento ativo parar
     de receber preco para sempre, o `fechado` do Hub trava pelo FATO 1 e nenhum eod.ready
     novo chega — esses dias NAO sao materializados por ninguem, por desenho da ADR-9, e
     quem detecta esse estado e o alerta das 12:00 sobre processado_em) —
     data_evento <= U: o dia esta DENTRO do intervalo e o worker
     versiona (mudou) ou cria (nao existia); data_evento > U: aquele dia AINDA NAO FOI
     MATERIALIZADO POR NINGUEM, e o eod.ready que vier a fecha-lo le o livro JA COM o
     movimento. SO NESTE SEGUNDO RAMO o intervalo vazio esta correto, e agora com prova.
     A versao anterior deste prompt dizia "se desde for hoje o intervalo e vazio, isso esta
     correto, nao conserte" — APAGUE ESSA IDEIA: com o FATO 2, um dia que o recorte exclui
     NAO E CRIADO POR MAIS NINGUEM, e "nao conserte" transformava buraco em ordem.
  1c. O HANDLER DE eod.ready(D) MATERIALIZA [U_anterior + 1, D], NAO SO D — e isso e a
     segunda metade do FATO 2. ISTO E UM DESVIO DA 7.3, QUE ESCREVE O RAMO NO SINGULAR
     ("EodPricesReady(D): para cada (cliente, instrumento) com posicao != 0: snapshot(D)")
     — REGISTRE-O, igual ao ref_externa NOT NULL do F3; ele esta na lista canonica de
     desvios, no F5. Sem o rotulo voce le a 7.3 (como este prompt manda) e encontra a
     contradicao sem explicacao. POR QUE: como o Hub so emite quando fechado >
     ultimoEmitido, D PULA datas, e os dias pulados sao dias uteis com posicao != 0 que
     NENHUM evento futuro anuncia. Percorra o intervalo com A MESMA FUNCAO do worker (7.4,
     "um so codigo"), grave eod_processado(D) e so entao de o ack (decisao 2). COM
     U_anterior NULO o intervalo comeca em MIN(data_evento) DO LIVRO (decisao 1b), NAO em
     D: a versao anterior deste prompt dizia "U_anterior nulo -> so D", e essa regra
     tambem se aplicava a PERDA da tabela, deixando os dias entre o U perdido e D sem linha
     para sempre. MIN(data_evento) do livro nao e "o comeco dos tempos": e o primeiro dia
     em que existe fato para valorar — E COM O TETO DA DECISAO 1b, que corta o intervalo em
     LIMITE e estaciona em vez de retentar. REJEITADO: materializar so D e contar com o worker para os
     pulados (o worker so passa por um dia se algum gatilho o alcancar; um dia pulado de um
     cliente sem movimento retroativo nunca ganharia linha, e o eod.ready daquela data ja
     foi queimado). REJEITADO: job de varredura de buracos (terceira engrenagem para
     descobrir o que o handler ja sabe ao receber o evento).
  1d. O campo `classes` do EodPricesReady (5.1) e IGNORADO nesta fase, e a ausencia e
     DECIDIDA: em fase 1 so existe `td`, e a ADR-9 adia a politica de classe atrasada
     ("Fase 2: publicar por classe e valorar parcial, ou timeout com ultimo <= D"). NAO
     invente valoracao parcial e NAO filtre instrumento por classe a partir desse campo.
  2. ACK do eod.ready SO NA COMPLETUDE do batch, e o batch e (dias do intervalo da
     decisao 1c x clientes x instrumentos). O laco tem que dizer se parou por COMPLETUDE
     ou por LIMITE (PADROES 10.31), E OS DOIS ROTULOS ESTAO ESCRITOS: COMPLETUDE = o
     intervalo inteiro materializado, ack; LIMITE = os dias uteis de [inicio, D] passaram
     do teto da decisao 1b, e ai o handler NEM COMECA (parking com intervalo_acima_do_teto
     + alerta, nunca retentativa). Confirmar o evento com o lote processado pela metade
     e o mesmo defeito com outra roupa. MAS A UNIDADE DO WATERMARK E OUTRA: grave
     eod_processado(d) POR DIA, na MESMA TRANSACAO que fecha aquele dia INTEIRO, com o
     intervalo percorrido em ORDEM CRESCENTE. Gravar com O DIA pela metade e que faria U
     avancar sobre o que ninguem materializou; gravar com O INTERVALO pela metade e CORRETO
     e desejavel, porque deixa U no ultimo dia inteiro e a reentrega recomeca em U + 1. O
     ACK e que sai so na completude do INTERVALO. NAO CONFUNDA AS DUAS UNIDADES: foi essa
     confusao que fez a alinea (iii) da reconciliacao alertar em operacao normal.
  3. Dia sem preco <= D para instrumento posicionado: NAO invente valor e NAO repita o
     preco anterior como observacao (forward-fill materializado e antipadrao nominal da
     secao 9 do PADROES). Falta de preco em dia util com posicao e SINAL: metrica +
     alerta. COMO O SINAL E OBTIDO — E ISTO E UM DESVIO DA PADROES 10.32, ROTULADO:
     a versao anterior deste prompt mandava "LER A MARCACAO" do produtor, e ELA NAO
     EXISTE PARA VOCE. Dois fatos de contrato: sem_preco_ate_a_data e campo da RESPOSTA
     REST do /prices/asof, e o PriceObserved da 5.1 NAO TEM campo `motivo` (ausencia de
     preco no push nao gera evento nenhum); e o seu caminho diario e o handler de
     eod.ready(D), que NAO CHAMA o asof — ele le preco_atual e historico_precos. NAO HA
     MARCACAO PERSISTIDA PARA LER, e o F6 registra por que criar uma seria pior (so o
     caminho REST a alimentaria). Entao DERIVE, por CONSULTA DEFINIDA: "nao existe linha
     em historico_precos com data_ref <= D para aquele instrumento_id". O que CONTINUA
     vindo marcado e nao se re-deriva: (i) a distincao entre sem_preco_ate_a_data e
     instrumento_desconhecido, que e do F6; (ii) "houve revisao?", que e a coluna
     `revisao`. O que voce deriva e SO a ausencia de linha — pergunta ao proprio banco,
     nao inferencia sobre dado alheio.
     EXCECAO, e ela e definicao e nao caso especial: caixa:BRL e caixa:a_liquidar tem
     preco 1,000000 POR DEFINICAO (V4 do F3), nunca recebem PriceObserved e nunca sao
     pedidos ao Hub. Elas GERAM linha de snapshot e ficam FORA deste alerta — senao a
     regra dispara todo dia, para sempre, e ninguem olha mais para ela.
  4. A reconciliacao ALERTA, JAMAIS CORRIGE — nem "so as chaves divergentes", que e
     auto-cura com outro nome e esconderia o bug do handler. A correcao e o comando
     administrativo do F4, executado por gente.
  5. O worker le o HISTORICO LOCAL por default; /prices/asof so quando faltar dia no
     intervalo.
  6. O fan-out do gatilho 2 roda FORA do handler do evento, com teto e enfileiramento —
     recalcular sincronamente seguraria o ack, e num host de UM nucleo o teto de CPU nao
     contem nada, so corta rajada (PADROES 10.13).
  6b. A DURABILIDADE DO FAN-OUT NAO E DA FILA: E DE UMA VARREDURA DERIVADA. NAO LEIA "com
     teto e enfileiramento" como "Channel<T> em memoria e pronto". O PriceObserved JA FOI
     ACKED (a decisao 6 so tira o fan-out do handler; nada segura o ack); o handler de
     eod.ready seguinte percorre [U_anterior + 1, D] e NAO VOLTA aos dias ja materializados;
     pelo FATO 2 o Hub NAO REANUNCIA aquela data; e a reconciliacao compara livro x
     posicao_corrente, NAO snapshot x preco. Um restart entre o ack e o recalculo perde a
     correcao de preco PARA SEMPRE, em silencio, no documento que o cliente ve (P2) — e o
     Fluxo 5 e metade do criterio de pronto do item 4 da secao 9. E VIOLACAO DIRETA DO P2 DA
     ARQUITETURA ("crash em qualquer ponto se resolve na proxima execucao").
     IMPLEMENTE A `varredura de defasagem de snapshot`, DERIVADA (disciplina do P5, sem
     estado de controle novo): ela percorre os (cliente, instrumento, data) que TEM SNAPSHOT
     VIGENTE com data <= U e marca como DEFASADO todo aquele em que existe
       (a) linha de historico_precos daquele instrumento COM revisao > 0, data_ref <= data
           e `observado_em` POSTERIOR ao `calculado_em` do snapshot vigente (gatilho 2). O
           `revisao > 0` NAO E FILTRO DE EFICIENCIA: e o que mantem a ADR-9 intacta. Uma
           varredura sobre TODA observacao faria um prices.td COMUM chegando depois do
           snapshot re-versionar o dia — preco individual disparando re-valoracao diaria,
           que a ADR-9 proibe e que o Pronto (a) desta fase REPROVA por assercao; ou
       (b) movimento daquela chave com data_evento <= data e `registrado_em` POSTERIOR ao
           `calculado_em` (gatilho 1, inclusive a liquidacao retroativa do job do F5).
     O ESCOPO DO recalcular QUE ELA ENFILEIRA E O CLIENTE, NAO A CHAVE DEFASADA: achado um
     (cliente, instrumento, data) defasado, enfileire recalcular(cliente, i, desde=data) PARA
     CADA instrumento `i` daquele cliente com movimento ou posicao no intervalo. SEM ISSO ela
     nao conserta o caso que mais a motivou — na liquidacao retroativa do F5 quem fica
     defasado e o snapshot de caixa:a_liquidar (que EXISTE), e a linha que FALTA CRIAR e a de
     caixa:BRL, que naquele dia NAO TEM SNAPSHOT e portanto NAO E PERCORRIDA. O custo e
     O(instrumentos do cliente), minusculo (secao 11). A assinatura da 7.4 nao muda; muda
     quantas chamadas voce faz.
     A FILA EM MEMORIA CONTINUA EXISTINDO e continua sendo o caminho normal — ela e LATENCIA,
     NAO GARANTIA. A varredura e a garantia.
     PARADA CLASSIFICADA (10.31, e ela esta em I15): COMPLETUDE = varri todos os (cliente,
     instrumento, data) com snapshot vigente e data <= U; LIMITE = teto de chaves por ciclo
     -> FALHA, nunca sucesso parcial (o ciclo seguinte recomeca, porque a condicao e derivada
     do dado e nao se consome).
     ELA NAO E UM QUARTO GATILHO E NAO FURA A ADR-9: chama o MESMO recalcular, com o mesmo
     recorte [desde, U], entao NAO materializa dia que o eod.ready nao fechou.
     LIMITE DECLARADO: um dia em que o cliente nao tem NENHUM snapshot (posicao zero em tudo,
     por I6) nao e percorrido. NAO "conserte" isso usando `movimentos` como conjunto
     dirigente e disparando quando NAO EXISTE snapshot para a data: isso dispararia PARA
     SEMPRE em todo dia de posicao zero — o estado normal de qualquer venda que zera — e
     viraria o alerta diario permanente que este arquivo condena. Esse caso fica com os dois
     caminhos que ja existem (a chamada enfileirada e o eod.ready que vier a fechar o dia).
     NAO PENDURE a varredura no job de reconciliacao: aquele job ALERTA E NUNCA CORRIGE
     (decisao 4), e misturar um corretor dentro dele apaga a regra que o define. E NAO a rode
     no mesmo ciclo do eod.ready: ela precisa rodar TAMBEM quando nenhum evento chega, que e
     metade dos casos que ela existe para pegar.

  AVISO SOBRE A PROVA DO FLUXO 5, e ele muda o que voce tem que entregar: a ARQUITETURA
  13.5 registra que a TD API DESCARTA correcao retroativa (so INSERE). Logo revisao > 0
  NAO NASCE da fonte td-api e o Fluxo 5 NAO E VERIFICAVEL pelo caminho real. A prova do
  gatilho 2 e SINTETICA: publique manualmente um prices.td com revisao: 1 no exchange e
  diga, no relato, que foi injecao. Nao procure a revisao chegando sozinha do Hub.

  MEDIR RECURSO DE NOVO: os 192m foram medidos em 2026-09-07 para uma API pequena SEM
  worker. Rode `docker stats` DURANTE um recalculo largo (todos os posicionados) E DURANTE
  um eod.ready com SALTO DE DATAS (decisao 1c — o batch vira dias x clientes x
  instrumentos), nao em regime, e olhe o que sobra para os VIZINHOS antes de subir o
  proprio teto (PADROES 10.14): tesouro-direto-alloy ja estava a 88% do teto dele e
  hub-precos-app roda SEM LIMITE, enxergando 1,9 GB de host em vez do cgroup. Se o teto
  subir, reescreva nos DOIS composes e refaca a conta dos vizinhos.

  NAO ENTRA: extrato (F8), corpaction e o gatilho 3 (F9 — ele cai no gatilho 1 por
  construcao e nao ganha codigo novo).

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) um `eod.ready(D)` **real do Hub** — o gatilho de verdade
  é parte da prova — produzindo linha de snapshot para cada (cliente, instrumento) com
  posição ≠ 0 em D, com `valor = quantidade × preco(último ≤ D)` conferido à mão contra
  `preco_atual`; Fluxo 2 retroativo ponta a ponta, **nas DUAS formas, porque uma só não
  distingue implementação nenhuma**: (i) uma operação com `data_evento` no passado
  **versionando** os snapshots do intervalo, e (ii) um movimento retroativo para um período
  **sem snapshot nenhum** (o cliente tinha posição zero lá) **CRIANDO** as linhas
  faltantes — que é o caso típico do registro manual e o que um worker "que só
  re-versiona" reprova. Mais: **nenhuma linha nasce, por qualquer um dos dois, para dia
  algum > `U`** — o fixture escolhe um dia acima do último `eod_processado` e prova que ele
  continua sem linha; a asserção é sobre `U`, **não** sobre "dia corrente", porque pelo fato
  1 o dia que o `eod.ready` fecha não é uma data de relógio e uma asserção escrita no relógio passa ou
  falha por acaso de calendário; Fluxo 5 **por injeção sintética** de
  `prices.td` com `revisao: 1`, dito como tal;
  **a passagem completa do drenador do motivo `tipo_nao_tratado_eod` devolvendo COMPLETUDE
  e residual 0, com `n_motivo ≥ 1`** — o teste
  **planta** a mensagem antes de rodar, porque com `n_motivo = 0` o desfecho é
  **VAZIO_DO_MOTIVO** e classificá-lo como COMPLETUDE fecharia a fase
  por vacuidade (é a varredura que sabe o estoque; gauge zera no deploy e o broker conta
  mensagens, não tipos). E — a prova que fecha o buraco que atravessava
  F5→F7→F8 — **o limbo D→D+1 aparecendo no DOCUMENTO, não só no livro**: um dia D com
  `a_liquidar` gerando **duas** linhas de snapshot (o título e `caixa:a_liquidar`), o dia
  D+1 gerando `caixa:BRL` depois de o job de liquidação do F5 rodar, e a **liquidação não
  mudando o patrimônio**: `Σ qtd_delta(caixa:a_liquidar) + Σ qtd_delta(caixa:BRL)`
  inalterado pelas duas pernas, que valem preço 1 por definição. *A versão anterior exigia
  "patrimônio constante entre D e D+1", que só é verdade se o preço não se mover **e** a
  posição for inteiramente liquidada — critério que uma implementação **correta** reprova.
  Se o fixture zerar a posição do título de propósito, a constância aparece como
  **consequência**, e a hipótese fica escrita no fixture.* (**Estrita**)
  (a) publicar **N** `PriceObserved` **com `revisao = 0`** e provar que
  `count(snapshots_posicao)` **não muda**, com o `eod.ready` seguinte como **controle
  positivo** — é a prova executável da ADR-9, e sem o controle positivo a asserção passaria
  também com o mecanismo de detecção quebrado (§10.8). **A `revisao = 0` não é detalhe do
  fixture: é o escopo do invariante.** Um `PriceObserved` com `revisao > 0` **deve** mudar a
  tabela — é o gatilho 2, que reversiona o passado —, então o mesmo teste traz o **segundo
  controle positivo**: injetar `revisao: 1` e exigir que os snapshots do intervalo
  **ganhem versão nova**, sem que nenhuma linha de dia `> U` seja materializada por isso.
  Escrito assim, o Pronto reprova as duas regressões opostas: valorar por ping individual e
  não implementar o gatilho 2. **E há uma terceira regressão a reprovar, que só existe desde
  que a varredura de defasagem entrou:** os `PriceObserved` com `revisao = 0` do fixture
  chegam **depois** de o dia já ter snapshot, e a varredura roda entre as duas asserções — se
  ela olhasse toda observação em vez de só `revisao > 0`, ela versionaria os snapshots e a
  primeira metade deste critério cairia. O teste **roda a varredura de propósito** entre a
  publicação e a asserção, e é isso que prova que o recorte dela por `revisao > 0` está
  implementado e não só escrito;
  (b) dia com posição zerada e dia sem pregão **não** geram linha;
  (c) reentrega do mesmo `eod.ready(D)` **não** cria segunda versão quando nada mudou — a
  reentrega é a **do broker** (nack, ou reinício antes do ack), porque pelo fato 2 o Hub
  **não** republica a mesma data; e `eod_processado(D)` continua com **uma** linha — o que
  aqui é **constraint** (`PRIMARY KEY (data_ref)`) e não asserção de teste, e a asserção
  confere que o caminho de gravação usa `ON CONFLICT (data_ref) DO NOTHING`, isto é, que
  `processado_em` **não** foi reescrito pela reentrega: adiar o alerta das 12:00 por uma
  reentrega do broker é o defeito que o `DO NOTHING` existe para impedir.
  **Com CONTROLE POSITIVO na mesma passagem, e sem ele o critério é vácuo por construção:**
  depois da primeira passagem `U = D`, o intervalo da reentrega é `[D+1, D]` = **vazio**, o
  handler não faz nada, e "não cria segunda versão" passaria **mesmo com o versionamento
  quebrado** (§10.8, a mesma família do `git status` que o F1 rejeita). O controle positivo é
  a **mesma reentrega com algo mudado** — um preço de `D` corrigido no histórico antes do
  segundo consumo — que **tem** de criar versão nova. A metade negativa só vale acompanhada
  da positiva;
  (d) **exatamente uma** linha `vigente` por (cliente, instrumento, data), provado por
  varredura da tabela e não pelo caminho feliz;
  (e) **determinismo**: rodar `recalcular` duas vezes sobre o mesmo estado faz **zero
  INSERT** na segunda — a §7.4 chama o algoritmo de função pura sobre dois livros imutáveis;
  se a segunda execução versionar de novo, ele não é;
  (f) o batch interrompido no meio **não** confirma o evento **e não grava
  `eod_processado(D)`**, provado por injeção de falha — as duas metades, porque `U` avançado
  sobre um batch pela metade tira os dias faltantes do alcance do worker **para sempre**.
  **E, com a unidade de completude sendo o DIA, esta alínea diz mais duas coisas e vira o
  CONTROLE POSITIVO da alínea (iii) da reconciliação:** (i) os dias **anteriores** ao ponto
  de falha, cada um fechado inteiro, **têm** sua linha em `eod_processado` — é isso que faz a
  reentrega recomeçar em `U + 1` em vez de refazer o intervalo todo, e uma asserção que
  exigisse `eod_processado` **vazia** depois da falha reprovaria a implementação correta;
  (ii) depois da falha, **`MAX(snapshots_posicao.data) ≤ U`** — nenhum snapshot ficou visível
  sem o watermark dele, porque os dois são a mesma transação. *A versão anterior desta alínea
  produzia, com commit por dia, um `MAX(snapshots.data) > U` **durável** até o próximo
  `eod.ready`, isto é, o próprio alerta da (iii) disparando por operação normal. Escrita
  assim, ela prova a (iii) em vez de contradizê-la.*
  **E a PERDA TOTAL da tabela, no mesmo critério:** com `eod_processado` truncada depois de
  um `U = D−4`, o `eod.ready(D)` seguinte materializa a partir de `MIN(data_evento)` do livro
  e **os dias ÚTEIS de `D−3..D−1` em que o cliente do fixture tem posição ≠ 0 têm linha** ao
  fim. *O qualificador não é preciosismo: pelo Pronto (b) e por I6, dia sem pregão e dia com
  posição zerada **não** geram linha, e uma asserção escrita como "os três dias têm linha"
  falha por acaso de calendário sempre que a janela pegar um fim de semana ou um feriado. É o
  **fixture** que escolhe a janela, fechada em dias úteis com posição — a mesma disciplina
  que este Pronto já exige ao mandar escrever a asserção do `> U` sobre `U` e não sobre o
  relógio.* É essa a asserção que torna "perdê-la degrada, não corrompe" conferível em vez de
  tranquilizadora, e é ela que reprova a regra antiga ("`U` nulo → materializa só `D`"), sob
  a qual esses dias ficariam vazios para sempre. **Mais TRÊS asserções que a mesma decisão
  exige, e sem elas ela fica meio provada:** *(f1)* **a metade do WORKER** — com
  `eod_processado` truncada, um `PriceObserved` com `revisao > 0` chegando **antes** do
  próximo `eod.ready` **não cria nem versiona linha nenhuma** (o intervalo `[desde, U]` é
  vazio), e o `eod.ready` seguinte cria/versiona os mesmos dias como **controle positivo** —
  é a prova de que "`U` nulo" tem regra nos **dois** consumidores, e sem o controle positivo
  a metade negativa passaria com o worker quebrado; *(f2)* **o TETO** — com o intervalo
  acima do teto (o fixture **reduz o teto** em vez de fabricar anos de livro), o `eod.ready(D)`
  **não é processado**, **não** grava `eod_processado(D)`, devolve **LIMITE**, alerta, e a
  mensagem aparece na `custodia.parked` com `x-custodia-motivo: intervalo_acima_do_teto`; e,
  depois de `materializar --desde --ate`, a passagem do drenador daquele motivo devolve
  **COMPLETUDE com `n_motivo ≥ 1`** e os dias ficam materializados — o **ciclo inteiro**,
  porque provar só a recusa deixa "parar" e "perder" indistinguíveis; *(f3)* **a DIREÇÃO
  INVERSA** — truncar `snapshots_posicao` **e** `eod_processado` juntas reconstrói os dias
  `≤ U`, e truncar **só** `snapshots_posicao` **não** se auto-cura: os dias `≤ U` continuam
  sem linha depois do `eod.ready` seguinte. Esse segundo caso entra como asserção **negativa
  declarada** — é ela que justifica a regra do par (truncar a projeção exige truncar o
  watermark) e que reprova qualquer "melhoria" futura que pretenda curar a projeção sem tocar
  no watermark. *Sem a metade negativa, (f3) passaria verde com a regra do par nunca
  implementada;*
  (g) recálculo de um instrumento **não** toca snapshot de outro instrumento nem de cliente
  fora do escopo;
  (h) a versão antiga continua legível e "por que o extrato de julho mudou?" é respondível
  pelos dados (`calculado_em`, `registrado_em`, `revisao`/`observadoEm`);
  (i) **o job de reconciliação tem CINCO asserções, não três, e o inventário é recontado
  aqui e não copiado:** as **três comparações de coluna** (quantidade, `preco_medio`,
  `custo_total`) — cada uma com uma divergência plantada **na própria coluna**, porque
  plantar só na quantidade deixa as outras duas sem prova — **mais as duas alíneas de estado
  do watermark**: (iii) `MAX(snapshots_posicao.data) > U` alertando, provado escrevendo um
  snapshot **por fora** do handler e do comando; e (iv) `MAX(snapshots_posicao.data) < U` com
  `U` além do último `D` conhecido alertando, provado adiantando `eod_processado` à mão. Nas
  cinco, **alerta e não corrige** — a asserção afirma também que a linha divergente
  **continua divergente** depois do job. *Sem (iii) e (iv) escritas como asserção, elas são
  prosa: o job compila, o alerta nunca existe, e a única corrupção que a tabela permite fica
  sem detector.* **E o CONTROLE NEGATIVO das duas: em operação normal — batch corrente
  inclusive — nenhuma das duas alíneas dispara**, que é o que separa um detector de um alerta
  diário permanente;
  (i2) **os DOIS índices de `eod_processado` conferidos contra o banco**, não contra a
  migration: `PRIMARY KEY (data_ref)` e `ix_eod_processado_processado_em`, lidos de
  `pg_indexes` com o nome no padrão `ix_tabela_colunas`. É a §10.22 aplicada — objeto de
  schema cuja ausência degrada **em silêncio** (o alerta das 12:00 vira seq scan, e a chave
  ausente deixa duas reentregas concorrentes gravarem duas linhas) vai para a sonda, não para
  a prosa. A varredura de `pg_indexes` do F3 já reprova índice fora da convenção; o que falta
  e entra aqui é a asserção de **presença** destes dois;
  (j) **`caixa:*` não dispara o alerta de preço ausente**, provado num dia útil com posição
  de caixa e sem nenhum `PriceObserved` de caixa (é o falso positivo diário que a V4
  impede), **com controle positivo**: um instrumento **do Hub** posicionado e sem preço ≤ D
  no mesmo dia **dispara**;
  (k) um instrumento posicionado em D para o qual **não existe linha de preço com
  `data_ref ≤ D`** produz o **sinal** (métrica + alerta) e **nenhuma** linha de snapshot
  para aquele instrumento naquele dia — nunca uma linha com preço fabricado. **Com controle
  positivo, e ele é o que separa as duas coisas que a versão anterior deste Pronto
  confundia:** o mesmo instrumento com uma linha de preço em **D−3** e nenhuma em D **gera**
  snapshot em D, valorado com o preço de D−3 — isso é `preco(último ≤ D)`, que é a regra da
  §7.4, e **não** é forward-fill; forward-fill seria gravar uma **observação** de preço para
  D, e isso continua proibido. *A formulação anterior — "um item marcado com
  `motivo: sem_preco_ate_a_data` **no histórico**" — era infalseável: o F6 decide, com
  razão, que essa ausência **não vira linha**, então o item marcado não existe no histórico
  para ser encontrado, e nenhuma implementação satisfazia o critério sem inventar o
  armazenamento que o F6 proibiu.*
  (l) **o snapshot histórico carrega `preco_medio` e `custo_total` DE `D`, não de hoje** —
  é a prova de que a dobra recebeu o corte e não foi copiada de `posicao_corrente`:
  `compra 10 @ 100` em `D1` e `compra 10 @ 200` em `D3` deixam o snapshot de `D2` com
  `preco_medio = 100` (não 150) e o de `D3` com 150; e um movimento retroativo posterior
  **não** altera o `preco_medio` de nenhum snapshot **anterior** à `data_evento` dele. Mais
  a varredura estrita: em **toda** linha de `snapshots_posicao` com `quantidade > 0`,
  `custo_total = preco_medio × quantidade` — o invariante do F3 conferido no documento, que
  é onde a cópia sem corte o quebraria;
  (m) **o recorte `[desde, U]` nos dois ramos da dicotomia, e o dia pulado:** um
  `eod.ready(D)` com **salto de datas** (`U_anterior = D−4`, injetado) materializando **os
  dias pulados também**, com **uma linha de `eod_processado` por DIA do intervalo — cinco, no
  caso — e a de `D` exatamente uma**, e com as datas em ordem crescente. *A versão anterior
  desta asserção dizia "`eod_processado(D)` gravado uma vez", que era verdade e insuficiente:
  ela ficava verde tanto com a granularidade por dia quanto com a por batch, que são as duas
  regras que este arquivo já sustentou e que dão respostas diferentes para o alerta da (iii);* um movimento retroativo
  com `data_evento ≤ U` **versionando** o dia; e o mesmo movimento com `data_evento > U`
  **não criando nada agora** e aparecendo **correto de primeira** no `eod.ready` que vier a
  fechar aquele dia — que é o ramo em que o intervalo vazio está certo. Sem os dois ramos,
  "o worker não fez nada" é indistinguível de "o worker deixou um buraco permanente";
  (n) **A VARREDURA DE DEFASAGEM, provada por CRASH e não pelo caminho feliz — é ela que
  torna o gatilho 2 durável, e sem esta alínea a decisão fica escrita e não entregue.** Nos
  **dois** caminhos, e os dois são injeção: (n1) um `prices.td` com `revisao: 1` sobre uma
  data já materializada é **acked** e o processo é **derrubado antes do recálculo** — os
  snapshots do intervalo ficam com a versão velha (metade negativa, que sozinha é verdadeira
  na implementação certa e na errada) e, **na execução seguinte, sem que nada seja
  reenfileirado**, a varredura os reversiona (metade positiva, e é ela que prova a
  recuperação); (n2) o mesmo com o job de liquidação do F5 — um `a_liquidar` vencido há três
  dias é liquidado, o processo cai antes do recálculo, e a execução seguinte tem de produzir
  **as duas** linhas do dia: `caixa:a_liquidar` **zerada** e **`caixa:BRL` CRIADA**. *(n2) é o
  teste que reprova a implementação que recalcula só a chave defasada: `caixa:BRL` não tem
  snapshot naquele dia, não é percorrida, e só aparece porque o `recalcular` enfileirado pela
  varredura tem escopo de **cliente**.* Mais o **controle negativo, sem o qual a varredura
  vira alerta permanente**: rodada duas vezes sobre um estado já convergido, a segunda
  passagem faz **zero INSERT** e enfileira **zero** recálculos;
  (o) **A GUARDA DO `--ate`, nas duas direções.** `materializar --desde X --ate Y` com `Y`
  **maior** que o último `D` anunciado **falha com código de saída não-zero, não grava
  NENHUMA linha** de `eod_processado` e diz na mensagem qual é o limite — **com controle
  positivo**: o mesmo comando com `Y` igual ao último `D` anunciado roda e materializa. E o
  **detector**, provado por fora do comando (porque validação de argumento não cobre `UPDATE`
  manual nem binário antigo): adiantando `eod_processado` à mão para além do último `D`
  conhecido, a alínea (iv) da reconciliação **alerta**. *Sem esta alínea, o único caminho do
  sistema capaz de violar "`U` atrasa, nunca adianta" entra sem validação e sem detector — e
  o efeito não é um erro visível, é o handler passando a calcular intervalo vazio para sempre;*
  Mais: `docker stats` durante o recálculo **e durante um `eod.ready` com salto de datas**,
  com o efeito nos vizinhos conferido.

- [ ] **F8** — extratos: a única superfície HTTP de negócio, e ela é de leitura.
  **Dependência externa nova: nenhuma.**

  Os dois extratos da §7.5 no padrão `MapReadGet` do molde
  (`../operacoes/src/Operacoes.API/Http/ReadEndpointExtensions.cs` e
  `Endpoints/InstrumentosEndpoints.cs`). **Movimentação:** `SELECT` ordenado do livro por
  `data_evento`, desempate `registrado_em`, desempate final `id` — auditável por construção,
  **não é relatório calculado**. **É a MESMA tripla da dobra da V1 do F3, e é também a chave
  do cursor** (decisão de paginação, abaixo): o extrato e a dobra leem o mesmo livro na mesma
  ordem, e divergir aqui seria explicar ao cliente por que o preço médio não bate com a lista
  de movimentos que ele acabou de ler. A §7.5 escreve `(data_evento, desempate
  registrado_em)` e **não** define desempate para `registrado_em` empatado; o `id` fecha essa
  ordem sem trocar nenhuma das duas chaves que ela prescreve. **Posição:** leitura de `snapshots_posicao WHERE vigente`,
  agregável por dia. *A §7.5 escreve essa agregação como "soma dos instrumentos + caixa =
  patrimônio diário"; **enquanto a PENDÊNCIA BLOQUEANTE do `caixa:BRL` (F3, V2) estiver
  aberta, essa igualdade NÃO é afirmada por esta fase** — o campo de total chama-se
  `somaDosValores` e o Pronto (g) diz exatamente o que ele afirma.* Erro em
  problem+json com `code`, leitura via **Dapper** com SQL explícito (leitura via EF é
  defeito, não estilo — §3), portas devolvendo `Result<T>`, `X-Api-Key` exigida,
  GET+HEAD+OPTIONS com 405 e `Allow`, ETag/304.

  Esta é a fase que mais parece justificar um endpoint de escrita "só um", e por isso o
  escopo diz de novo: **ADR-10, nenhum.** Se alguém pedir "um endpoint para corrigir a
  posição", a resposta é **estorno em Operações**, e a recusa é arquitetural, não
  preferência. E aqui ela deixa de ser prosa: **um teste varre o `EndpointDataSource` e
  REPROVA qualquer verbo diferente de GET/HEAD/OPTIONS sob `/v1`**, com guarda contra
  coleção vazia (§10.8, senão o teste fica verde quando a varredura quebrar). É a única
  prova executável da ADR-10, e é ela que reprova o PR no dia em que o pedido vier.

  **Decisões desta fase:**

  - **O CONTRATO DOS DOIS ENDPOINTS, decidido aqui — porque esta é a única superfície HTTP
    do serviço e o resto do arquivo fecha DDL coluna a coluna e cabeçalho valor a valor.**
    *A §7.5 descreve o que cada extrato lê e a ordem; ela não dá rota, parâmetro nem forma de
    resposta. Deixar isso para o executor é deixá-lo inventar o contrato de uma API pública
    dentro da fase que a entrega, e o Pronto (g) já pressupunha um parâmetro de dia que
    ninguém tinha definido.*

    ```
    GET /v1/extratos/movimentacao
        clienteId      OBRIGATÓRIO   (ausente -> 400 com code, nunca "todos os clientes")
        de, ate        opcionais     date, sobre data_evento, intervalo FECHADO nos dois lados
        instrumentoId  opcional      filtro exato, id do Hub cru (sem normalização)
        cursor         opcional      OPACO, nunca construído pelo cliente
        pageSize       opcional      default 100, clamp em 500

    GET /v1/extratos/posicao
        clienteId      OBRIGATÓRIO   (ausente -> 400 com code)
        data           opcional      date; default = MAX(data) dos snapshots VIGENTES
                                     daquele cliente
        cursor         opcional      OPACO, sobre instrumento_id
        pageSize       opcional      default 100, clamp em 500
    ```

    **Toda `date` é no fuso `America/Sao_Paulo`** (decisão do F3, item 5e).
    **O default de `data` na posição é `MAX(data)` do próprio cliente, NUNCA "hoje" — e isto
    é consequência direta do FATO 1 do F7:** o dia que o `eod.ready` fecha **não é uma data de
    relógio** (`D` é tipicamente `hoje−1`, e mais antigo em segunda, feriado ou instrumento
    atrasado), então um default em "hoje" devolveria **lista vazia em todo dia útil normal** —
    o mesmo erro que fez o alerta das 12:00 ser medido em `processado_em` e não em `data_ref`.
    **`data` sem snapshot devolve 200 com lista vazia e a `data` ecoada, nunca 404:** ausência
    de posição não é ausência de recurso, e 404 aqui seria indistinguível de cliente
    inexistente — que esta fase, por não ter catálogo de clientes, não sabe responder.
    **`de > ate` é 400 com `code`**, não lista vazia.
    **Valores monetários e quantidades saem como STRING DECIMAL**, nunca float JSON — é a
    mesma convenção que a §5.1 impõe na entrada, e trocá-la na saída reintroduziria na borda
    de leitura o erro de precisão que o livro evita.
    **Forma da resposta**, com os nomes vindo das colunas e sem renomear nada:
    movimentação devolve `refExterna`, `tipo`, `instrumentoId`, `qtdDelta`,
    `valorFinanceiro`, `dataEvento`, `registradoEm` e `refEstorno`; posição devolve `data` e,
    por linha, `instrumentoId`, `quantidade`, `precoMedio`, `custoTotal`, `preco` e `valor`
    (`= quantidade × preco`). **`X-Total-Count` é omitido nos dois** (é o desvio da §2
    declarado abaixo) e o `Link` com `next` é a superfície de navegação.
    **A posição também pagina, e isso não é zelo:** o número de instrumentos de um cliente é
    pequeno **hoje**, não **limitado por construção**, e "coleção sem limite superior" é
    antipadrão nominal da §9 — o mesmo argumento que obriga a paginação da movimentação. O
    cursor dela é sobre `instrumento_id`, que é chave única dentro de `(cliente, data)`.
    **O CAMPO DE TOTAL DA POSIÇÃO CHAMA-SE `somaDosValores`, NÃO `patrimonio`, e o nome é a
    decisão:** ele é, literalmente, a soma dos `valor` das linhas devolvidas — nada além
    disso. Chamá-lo de patrimônio afirmaria a igualdade da §7.5 que a **PENDÊNCIA BLOQUEANTE
    do `caixa:BRL`** (F3, V2) deixa em aberto, e afirmá-la num nome de campo é pior que num
    critério de teste, porque o nome vai para o cliente e não sai mais.
    *Rejeitado:* `clienteId` na rota (`/v1/clientes/{id}/extratos/...`) — sugere um recurso
    "cliente" que esta casa **não tem** (não há tabela de clientes, por I11/ADR-4) e convida o
    `GET /v1/clientes` que a fase seguinte acrescentaria. *Rejeitado:* um endpoint só com
    `tipo=movimentacao|posicao` — as duas respostas têm formas diferentes e políticas de cache
    diferentes, e o discriminador viraria `switch` na borda.
  - **De onde vem o `clienteId`: parâmetro OBRIGATÓRIO da requisição, e a autorização por
    usuário final está FORA de escopo — ausência decidida, não esquecida.** A fase inteira é
    sobre "o extrato de uma pessoa", e nada dizia se o cliente vem de query, de rota ou do
    principal autenticado. Decisão: **parâmetro obrigatório** (ausente ⇒ 400 com `code`,
    nunca "todos os clientes"), e o chamador é **serviço confiável**, autenticado por
    `X-Api-Key` de serviço. O que sustenta: o `PADROES` §3 diz que *"quem garante que
    `cliente_id` existe é a borda autenticada"*, e a §6 da `ARQUITETURA` diz que *"a UI só
    fala com Operações"* — não há usuário final chegando aqui. *Rejeitado:* derivar
    `clienteId` do principal da `X-Api-Key` — a chave é **de serviço**, uma só, e amarrar
    identidade de cliente a ela criaria uma terceira noção de identidade sem nenhum emissor
    que a preencha. **Consequência que precisa estar escrita, para ninguém confundir as
    duas coisas:** com uma chave única de serviço, o Pronto "`clienteId` de outro cliente não
    devolve linha nenhuma" prova **filtro**, não **isolamento** — ele é satisfeito por um
    `WHERE cliente_id = @x` e não afirma nada sobre autorização. Isso é aceito de propósito
    nesta fase; no dia em que houver usuário final, autorização entra como fase própria,
    **antes** de o endpoint ser exposto para fora da rede interna.
  - **`Cache-Control: private`, parametrizável por `IConfiguration`, nunca constante e
    NUNCA `public`.** O motivo aqui é mais grave que no `operacoes` (§10.27): lá o que
    variava por cliente era a ordem de um autocomplete; **aqui é o extrato inteiro de uma
    pessoa**. `public` autorizaria um proxy ou CDN compartilhado a servir o extrato de um
    cliente para outro — vazamento por um header herdado do molde sem checar a premissa
    dele. O teste afirma a **presença** de `private` **e a ausência** de `public`.
  - **Paginação obrigatória**, ao contrário do F5 do `operacoes`. Lá a coleção era limitada
    por construção e a §2 autorizava dispensar; aqui um cliente antigo tem milhares de
    movimentos e o limite superior é exigido pela §2 e pela §9. **Cursor sobre
    `(data_evento, registrado_em, id)` — a MESMA tripla do `ORDER BY`, e a mesma da dobra da
    V1 do F3 —, nunca offset**: offset sofre corrida com inserção concorrente (o
    mesmo argumento da ADR-7), e o livro recebe inserção o tempo todo pelo consumidor.
    **Ordenar por uma chave e paginar por outra é defeito, não estilo, e a versão anterior
    deste roadmap fazia exatamente isso** — ordenava por `(data_evento, registrado_em)` e
    paginava por `(data_evento, id)`. O par `(data_evento, id)` **não refina**
    `registrado_em`: ele o **remove** da chave. `registrado_em` é `now()`, hora de
    **transação**; `id` é `bigserial` alocado na execução do INSERT — dois escritores
    concorrentes na mesma `(cliente, instrumento, data_evento)` ordenam-se **ao contrário**
    nos dois campos, e o segundo escritor existe **por desenho** a partir do F5 (o
    `IHostedService` de liquidação escreve enquanto o consumidor de eventos escreve). Com as
    duas chaves diferentes, a página **pula ou duplica linha** sempre que a fronteira cai
    dentro de um grupo empatado em `registrado_em` — e esse grupo é comum, não hipotético:
    são as linhas que **todo resgate** grava na mesma transação. **E o número aqui é QUATRO,
    não três — o inventário é recontado NO ESCOPO DESTA FASE, não copiado do F3.** No F3 o
    "três" está certo, porque lá o recorte da dobra é `(cliente, instrumento)` e só três das
    quatro caem em `caixa:a_liquidar`; **aqui o extrato de movimentação é POR CLIENTE, sem
    recorte de instrumento** (`clienteId` é o único parâmetro obrigatório), então o grupo
    empatado em `(data_evento, registrado_em)` inclui também a **`venda`**, que está no
    instrumento do título: são **`venda`, `ir_retido`, `iof` e `a_liquidar`** — as quatro com
    `data_evento = dataEvento` e o mesmo `registrado_em`, porque `now()` é hora de transação.
    *Quantas são exatamente depende do fato: **quatro** com IOF, **três** sem IOF (prazo ≥ 30
    dias) e **duas** quando a base do IR é zero ou negativa (F5), porque aí `ir:` e `iof:` não
    existem. O fixture do Pronto (e2) fixa o caso de **quatro**, de propósito, porque é o
    maior — e um `pageSize` escolhido para cortar um grupo de três não corta o de quatro.*
    Com a tripla nas duas coisas, a §7.5 fica satisfeita **literalmente** e não há ordem nova
    a rotular; o custo é um campo a mais dentro do cursor opaco.
    **E isto é um DESVIO da §2 dos padrões, não uma aplicação dela — registre-o como tal,
    aprove-o com o `advisor` e grave-o na memória**, na mesma convenção do `ref_externa NOT
    NULL` do F3 e da decisão do `operacoes` de dispensar paginação no F5 dele. A §2 prevê
    `page/pageSize` com clamp (default 100, máx 500), `X-Total-Count` **sempre** e `Link`
    (next/prev) só quando `page` informado; cursor **não** é uma das formas previstas, e é
    **incompatível com `X-Total-Count` "sempre"** — contar o livro inteiro de um cliente a
    cada página é exatamente o custo que o cursor evita. Sem o rótulo, o executor cai entre
    duas normas e o `guardiao-padroes` reprova a entrega pela §2.
    **Destino dos dois headers, decidido aqui:** `X-Total-Count` é **omitido** na resposta
    paginada por cursor (afirmar um total que não se conta seria pior que não afirmar), e o
    header `Link` **passa a ser a superfície de navegação**, com `next` carregando o cursor
    **opaco** — sem `prev`, porque o cursor sobre a tripla é unidirecional e um
    `prev` fabricado mentiria na presença de inserção concorrente, que é o motivo de existir
    o cursor. O clamp de `pageSize` (default 100, máx 500) **fica**: essa metade da §2 vale
    igual, e é ela que dá o limite superior que a §9 exige.
  - **`IContentVersionProvider` deriva SÓ do banco local, SEM balde temporal** — e a
    premissa fica escrita: aqui, ao contrário do `operacoes` (§10.28), **tudo** que o
    endpoint serve está neste banco; não há catálogo remoto cacheado dentro da resposta. Se
    um dia o extrato passar a exibir dado vindo do Hub em tempo de leitura, esta decisão
    **expira** e o balde temporal volta a ser necessário.
  - **Sem GET condicional contra fonte externa e sem last-known-good neste caminho** — não
    há fonte externa aqui; registrar a ausência evita que alguém porte o cache do
    `operacoes` por fidelidade ao molde (§10.29, §10.30).
  - **Extrato de movimentação é `SELECT` do livro, não relatório.** *Rejeitado:* derivá-lo
    dos snapshots — perderia exatamente a auditabilidade que faz o livro ser a verdade.

  **NÃO ENTRA:** qualquer POST/PUT/PATCH/DELETE; qualquer reconstrução exposta por HTTP
  (decisão B); corpaction (F9).

  **Configuração: nenhuma.** Conferir que a rota `/v1/smoke-inexistente` do smoke test do
  deploy **continua inexistente** — a prova "401 sem chave e 404 com chave" deixa de valer
  no dia em que ela existir.

  **Âncoras:** `ARQUITETURA` §7.5, §7.1 ("o que o cliente viu tem que continuar existindo" —
  P2; auditoria "como estava em X" por `calculado_em <= X`), §8.4 (o limbo D→D+1), ADR-10;
  `PADROES` §1, §2 (contratos HTTP; coleção sem limite superior é antipadrão da §9), §3
  (leitura por Dapper, `Result<T>` também na leitura), §8, §10.8, §10.19, §10.27, §10.28,
  §10.29, §10.30.

  **Prompt:**
  ```
  Implemente os dois extratos da secao 7.5 do ARQUITETURA, no padrao MapReadGet do molde
  ../operacoes/src/Operacoes.API/Http/ReadEndpointExtensions.cs e
  ../operacoes/src/Operacoes.API/Endpoints/InstrumentosEndpoints.cs.

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  MOVIMENTACAO: SELECT ordenado do livro por data_evento, desempate registrado_em,
  DESEMPATE FINAL id — a MESMA TRIPLA da dobra da V1 do F3 e a MESMA CHAVE do cursor da
  decisao 2 abaixo. A 7.5 escreve (data_evento, desempate registrado_em) e nao define
  desempate para registrado_em empatado; o id fecha a ordem sem trocar nenhuma das duas.
  Auditavel por construcao — NAO derive dos snapshots, isso perderia exatamente a
  auditabilidade que faz o livro ser a verdade. Tributos e liquidacoes aparecem como
  LINHAS PROPRIAS.
  POSICAO: leitura de snapshots_posicao WHERE vigente, agregavel por dia. A 7.5 escreve
  essa agregacao como "soma dos instrumentos + caixa = patrimonio diario", MAS NAO A AFIRME:
  e ela que a PENDENCIA BLOQUEANTE do caixa:BRL (F3, V2) deixa em aberto. O campo de total
  chama-se `somaDosValores` e e, literalmente, a soma dos `valor` das linhas devolvidas.

  O CONTRATO DOS DOIS ENDPOINTS ESTA DECIDIDO — implemente-o, nao o invente:
    GET /v1/extratos/movimentacao
        clienteId      OBRIGATORIO   (ausente -> 400 com code, NUNCA "todos os clientes")
        de, ate        opcionais     date, sobre data_evento, intervalo FECHADO nos dois lados
        instrumentoId  opcional      filtro exato, id do Hub CRU (sem normalizacao)
        cursor         opcional      OPACO
        pageSize       opcional      default 100, clamp em 500
    GET /v1/extratos/posicao
        clienteId      OBRIGATORIO   (ausente -> 400 com code)
        data           opcional      date; default = MAX(data) dos snapshots VIGENTES DAQUELE
                                     CLIENTE — NUNCA "hoje": pelo FATO 1 do F7 o dia que o
                                     eod.ready fecha nao e uma data de relogio (D e
                                     tipicamente hoje-1), entao um default em "hoje"
                                     devolveria LISTA VAZIA EM TODO DIA UTIL NORMAL
        cursor         opcional      OPACO, sobre instrumento_id
        pageSize       opcional      default 100, clamp em 500
  TODA `date` e no fuso America/Sao_Paulo (decisao do F3, item 5e).
  `data` sem snapshot -> 200 com lista VAZIA e a `data` ecoada, NUNCA 404 (ausencia de
  posicao nao e ausencia de recurso, e esta casa nao tem catalogo de clientes para
  responder "cliente inexistente"). `de > ate` -> 400 com code, nao lista vazia.
  VALORES MONETARIOS E QUANTIDADES SAEM COMO STRING DECIMAL, nunca float JSON — mesma
  convencao que a 5.1 impoe na entrada.
  FORMA: movimentacao devolve refExterna, tipo, instrumentoId, qtdDelta, valorFinanceiro,
  dataEvento, registradoEm e refEstorno; posicao devolve `data`, `somaDosValores` e, por
  linha, instrumentoId, quantidade, precoMedio, custoTotal, preco e valor (= quantidade x
  preco). X-Total-Count OMITIDO nos dois (desvio declarado); Link com `next` e a navegacao.
  A POSICAO TAMBEM PAGINA: o numero de instrumentos de um cliente e pequeno HOJE, nao
  LIMITADO POR CONSTRUCAO, e "colecao sem limite superior" e antipadrao nominal da secao 9.
  NAO ponha clienteId na rota (/v1/clientes/{id}/...): sugere um recurso "cliente" que esta
  casa NAO TEM (sem tabela de clientes, I11/ADR-4) e convida o GET /v1/clientes da fase
  seguinte. E NAO faca um endpoint so com tipo=movimentacao|posicao: as duas respostas tem
  formas e politicas de cache diferentes, e o discriminador viraria switch na borda.

  Leitura via DAPPER com SQL explicito em ReadRepository, NUNCA EF (PADROES secao 3 —
  leitura via EF e defeito, nao estilo). Portas devolvendo Result<T>. problem+json com
  `code`. X-Api-Key exigida. GET + HEAD + OPTIONS, 405 com Allow, supressao de corpo em
  HEAD, ETag/304.

  ADR-10, E ELA DEIXA DE SER PROSA NESTA FASE: escreva um teste que varre o
  EndpointDataSource e REPROVA qualquer verbo diferente de GET/HEAD/OPTIONS sob /v1, com
  GUARDA CONTRA COLECAO VAZIA (PADROES 10.8 — senao o teste fica verde no dia em que a
  varredura quebrar). E a unica prova executavel da ADR-10, e e ela que reprova o PR no
  dia em que alguem pedir "um endpoint so para corrigir a posicao". A resposta a esse
  pedido e ESTORNO em Operacoes.

  DECISOES JA TOMADAS:
  0. O clienteId e PARAMETRO OBRIGATORIO da requisicao (ausente -> 400 com `code`, NUNCA
     "todos os clientes"), e o chamador e SERVICO CONFIAVEL autenticado por X-Api-Key de
     servico. Autorizacao por usuario final esta FORA DE ESCOPO — ausencia DECIDIDA: o
     PADROES secao 3 diz que quem garante que cliente_id existe e a BORDA AUTENTICADA, e
     a secao 6 da ARQUITETURA diz que a UI so fala com Operacoes. NAO derive o clienteId
     do principal da X-Api-Key: a chave e de SERVICO, uma so. E ESCREVA NO NOME DO TESTE
     que "clienteId de outro cliente nao devolve linha" prova FILTRO, nao ISOLAMENTO —
     com chave unica de servico, ela e satisfeita por um WHERE e nao afirma nada sobre
     autorizacao. Isso e aceito de proposito nesta fase.
  1. Cache-Control `private`, vindo de IConfiguration, NUNCA constante e NUNCA `public`.
     O extrato varia por cliente; `public` autorizaria um proxy compartilhado a servir o
     extrato de um cliente para outro (PADROES 10.27). Teste afirma a PRESENCA de
     `private` E A AUSENCIA de `public`.
  2. PAGINACAO OBRIGATORIA, ao contrario do F5 do operacoes: aqui a colecao NAO e
     limitada por construcao. Cursor sobre (data_evento, registrado_em, id) — A MESMA
     TRIPLA DO ORDER BY —, NUNCA offset: offset sofre corrida com insercao concorrente
     (mesmo argumento da ADR-7) e o livro recebe insercao o tempo todo pelo consumidor.
     ORDENAR POR UMA CHAVE E PAGINAR POR OUTRA E DEFEITO, e era o que a versao anterior
     deste prompt mandava fazer (ORDER BY data_evento, registrado_em; cursor sobre
     data_evento, id). (data_evento, id) NAO REFINA registrado_em — ELE O REMOVE da
     chave: registrado_em e now() (hora de TRANSACAO) e id e bigserial alocado no INSERT,
     entao dois escritores concorrentes na mesma (cliente, instrumento, data_evento) se
     ordenam AO CONTRARIO nos dois campos, e o segundo escritor existe POR DESENHO a
     partir do F5 (o IHostedService de liquidacao escreve enquanto o consumidor escreve).
     Com as duas chaves diferentes a pagina PULA OU DUPLICA LINHA quando a fronteira cai
     dentro de um grupo empatado em registrado_em — e esse grupo e comum: sao as linhas que
     todo resgate grava NA MESMA TRANSACAO. AQUI O NUMERO E QUATRO, NAO TRES: o "tres" do F3
     esta certo LA, onde o recorte da dobra e (cliente, instrumento) e so tres das quatro
     caem em caixa:a_liquidar; ESTE extrato e POR CLIENTE, SEM RECORTE DE INSTRUMENTO
     (clienteId e o unico parametro obrigatorio), entao o grupo inclui tambem a VENDA, que
     esta no instrumento do titulo — venda, ir_retido, iof e a_liquidar, as quatro com
     data_evento = dataEvento e o mesmo registrado_em (now() e hora de TRANSACAO). Sao
     QUATRO com IOF, TRES sem IOF (prazo >= 30 dias) e DUAS quando a base do IR e zero ou
     negativa (F5). O fixture do Pronto (e2) fixa o caso de QUATRO, de proposito: um
     pageSize escolhido para cortar um grupo de tres NAO corta o de quatro.
     ISTO E UM DESVIO DA SECAO 2 DO PADROES, NAO UMA APLICACAO DELA. A secao 2 preve
     page/pageSize com clamp (default 100, max 500), X-Total-Count SEMPRE e Link
     (next/prev) so quando `page` informado; cursor nao e uma das formas previstas e e
     INCOMPATIVEL com "X-Total-Count sempre". Marque-o como DESVIO POR CORRECAO, leve-o
     ao advisor e GRAVE NA MEMORIA — igual ao ref_externa NOT NULL do F3. Sem o rotulo o
     guardiao-padroes reprova a entrega pela propria secao 2.
     DESTINO DOS HEADERS, ja decidido: X-Total-Count OMITIDO (afirmar um total que nao se
     conta e pior que nao afirmar); o header Link vira a superficie de navegacao, com
     `next` carregando o cursor OPACO e SEM `prev` (o cursor sobre a tripla e
     unidirecional, e um prev fabricado mentiria sob insercao concorrente — que e o
     motivo de existir o cursor). O CLAMP de pageSize (default 100, max 500) FICA: essa
     metade da secao 2 vale igual, e e ela que da o limite superior que a secao 9 exige.
  3. IContentVersionProvider deriva SO do banco local, SEM balde temporal: aqui, ao
     contrario do operacoes (PADROES 10.28), tudo que o endpoint serve e local. Registre
     a premissa: se um dia o extrato exibir dado do Hub em tempo de leitura, ela expira.
  4. Sem GET condicional contra fonte externa e sem last-known-good neste caminho — nao
     ha fonte externa aqui. Registre a AUSENCIA, para ninguem portar o cache do operacoes
     por fidelidade ao molde.

  NAO ENTRA: qualquer POST/PUT/PATCH/DELETE; qualquer reconstrucao exposta por HTTP;
  corpaction (F9).

  Confira que /v1/smoke-inexistente CONTINUA inexistente — a prova "401 sem chave e 404
  com chave" do deploy deixa de valer no dia em que essa rota existir.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) extrato de movimentação batendo **linha a linha** com um
  `SELECT` direto do livro — se divergir, o extrato está calculando algo e não deveria — com
  tributos e liquidações visíveis como linhas próprias; extrato de posição batendo com a
  soma dos snapshots **vigentes** do dia, incluindo as linhas de caixa; o limbo D→D+1 do
  Fluxo 3 aparecendo honestamente em dois dias consecutivos; 304 com `If-None-Match`.
  (**Estrita**) (a) o teste de varredura do `EndpointDataSource` **reprovando** um POST
  plantado sob `/v1` — com a guarda contra coleção vazia provada por mutação; (b)
  `Cache-Control` da resposta **real** contendo `private` e **não** contendo `public`; (c)
  401 **sem** chave **e** 401 **com chave errada** (só o primeiro passaria também com chave
  errada); (d) `clienteId` **ausente** devolvendo **400** com `code` — nunca o extrato de
  todo mundo —, e `clienteId` de outro cliente **não** devolvendo linha nenhuma;
  **`de > ate` devolvendo 400 com `code`** e não lista vazia; **`data` sem snapshot
  devolvendo 200 com lista vazia e a `data` ecoada**, nunca 404; e **o default de `data` na
  posição sendo `MAX(data)` dos snapshots vigentes daquele cliente, provado com `U` em
  `hoje−1`** — o teste chama o endpoint **sem** `data` num dia em que não há snapshot de
  hoje e exige a linha de `hoje−1`; escrito com "hoje" como default, ele devolveria vazio em
  todo dia útil normal, que é o mesmo erro do alerta medido em `data_ref`. *O nome do
  teste diz o que ele prova: **filtro**, não isolamento — com uma `X-Api-Key` única de
  serviço, essa asserção é satisfeita por um `WHERE cliente_id = @x` e não afirma nada sobre
  autorização, que esta fase declara fora de escopo;* (e) o limite
  superior da coleção provado com um cliente de **muitos** movimentos, não com o feliz de
  três linhas, e o cursor exercitado nos **DOIS** modos de quebra, que são defeitos
  diferentes e nenhum pega o outro: (e1) **inserção concorrente** — inserir movimento no
  livro entre a página 1 e a página 2 **não** duplica nem pula linha, que é o motivo inteiro
  de o cursor existir e o teste que um offset reprovaria. **E o LIMITE de (e1) fica
  declarado, porque a própria decisão desta fase o introduz:** o cursor protege contra o
  deslocamento causado por inserção **commitada depois** da leitura do cursor; uma transação
  de escrita **iniciada ANTES** da página 1 e commitada **depois** insere uma linha **atrás**
  do cursor, e essa linha é pulada. Isso segue do argumento que sustenta a tripla —
  `registrado_em` é hora de **transação** e `id` é `bigserial` alocado no INSERT, logo
  **nenhum dos dois é monotônico na ordem de COMMIT** — e o segundo escritor de transação
  longa existe **por desenho**: é o `IHostedService` de liquidação do F5, que é
  O(`a_liquidar` em aberto). O fixture de (e1) fixa a hipótese **"sem transação de escrita
  aberta antes da página 1"**, e diz isso no nome do teste; sem a hipótese escrita, (e1)
  afirma uma garantia que o desenho não dá; (e2) **fronteira de página dentro
  de um grupo EMPATADO** — as **quatro** linhas que um resgate **com IOF** grava na mesma
  transação (`venda`, `ir_retido`, `iof` e `a_liquidar`: mesmo `data_evento` e mesmo
  `registrado_em`, V2 do F3), **das quais três em `caixa:a_liquidar` e uma no instrumento do
  título** — e são quatro porque **este** extrato é por cliente, sem recorte de instrumento;
  o "três" do F3 é o número da dobra, que é por `(cliente, instrumento)` —, com `pageSize`
  escolhido **de propósito** para cortar esse grupo de **quatro** ao meio, e a concatenação das páginas batendo **linha a linha e na
  mesma ordem** com o `SELECT` sem paginação. *Sem (e2) esta fase fecha verde com um defeito
  real: era o que acontecia enquanto o `ORDER BY` era `(data_evento, registrado_em)` e o
  cursor era `(data_evento, id)` — ordens diferentes só divergem **dentro** do empate, e
  (e1) não produz empate nenhum;* (f) a consulta de auditoria por
  `calculado_em <= X` devolvendo a versão antiga, provando que o documento que o cliente
  viu continua existindo; (g) as **linhas de caixa presentes** no extrato de posição — `caixa:BRL` e
  `caixa:a_liquidar`, com `preco = 1,000000` —, e o `somaDosValores` da resposta **igual à
  soma dos `valor` das linhas que ela mesma devolveu**, conferido item a item. É a ponta em
  que a V4 do F3 e o snapshot de caixa do F7 são conferidos por quem lê.
  **Esta alínea DEIXOU DE AFIRMAR "soma dos instrumentos + caixa = patrimônio", e a troca é
  deliberada:** enquanto a **PENDÊNCIA BLOQUEANTE do `caixa:BRL`** (F3, V2) estiver aberta,
  aquela igualdade **fecharia VERDE com o número errado** — o extrato diria 1800 para um
  cliente que tem 900, no dia seguinte a uma reaplicação, que é o ciclo de vida normal do
  produto. Dos três estados possíveis (certo, errado e visível, errado e verde), o pior é o
  terceiro, e era esse que o critério anterior produzia. O que ficou é uma asserção de
  **consistência interna da resposta**, que não depende da decisão pendente e continua
  valendo sob qualquer desfecho dela. *Quando a pendência fechar, é AQUI que a igualdade
  volta como critério — e a redação dela dependerá da saída escolhida: com a perna simétrica,
  "soma das linhas = patrimônio"; com o livro-caixa parcial, "soma dos instrumentos =
  patrimônio, e caixa é o não-reinvestido conhecido".* (h) o contrato de
  paginação conferido como **desvio declarado**: `X-Total-Count` **ausente**, header `Link`
  com `next` e **sem** `prev`, e `pageSize` acima de 500 **clampado**.

- [ ] **F9** — corpactions: um fato do Hub virando N movimentos. Por último de propósito.
  **Dependência externa nova: o Hub publicando `corpactions.*` — e isso exige código no
  `../hub-precos`, OUTRO REPO.**

  **PENDÊNCIAS QUE ESTA FASE HERDA OU ABRE, e as duas estão escritas nas decisões abaixo:**
  ela **herda a PENDÊNCIA BLOQUEANTE do `prazo`** (F5 — a alíquota do cupom depende dela do
  mesmo jeito) e **abre a pendência da reversão de corpaction**, que hoje não é
  representável. Nenhuma das duas se resolve dentro desta fase.

  **AVISO DE BLOQUEIO, leia antes de despachar:** a §9 item 5 põe **o gerador determinístico
  de cupom/vencimento no Hub**, não aqui — "gerador determinístico no Hub (cupom/vencimento)
  + tradução em movimentos com dedupe". Esta fase pode estar bloqueada por código que não é
  desta casa, e é a **única** dependência deste roadmap que não se resolve aqui dentro.
  Confira antes de abrir a fase: se o Hub ainda não publica `corpactions.td`, o Pronto não é
  executável e a vez é do outro repo.

  Handler de `CorporateActionObserved`, drenando o motivo `tipo_nao_tratado_corpactions` do
  `custodia.parked` — **são os únicos eventos estacionados que NÃO se recuperam por REST**,
  e é para eles que o estacionamento existiu. **Cupom:** para cada cliente com posição > 0
  no instrumento **na data**, movimento `cupom` (qtd 0, valor = qtd × `valorPorUnidade`) +
  `ir_retido` pela tabela regressiva do prazo + `a_liquidar` → `liquidacao`, reusando o motor
  do F5 — **inclusive o job de liquidação**, que grava as duas pernas em D+1 útil.
  **MAS A BASE DO CUPOM É O VALOR INTEGRAL, e reusar o motor do F5 sem esta qualificação
  SUBTRIBUTA TODO CUPOM.** O motor do F5 calcula sobre o **ganho de alienação**
  (`valorFinanceiro − preco_medio × quantidade`); um cupom **não tem custo de aquisição** —
  ele é renda, é o que a V1 do F3 já diz ao mandar `cupom` não tocar `custo_total` nem
  `preco_medio` —, então subtrair um preço médio dele daria base zero ou negativa e
  **nenhuma** linha de `ir_retido`. A `ARQUITETURA` §11 trata "IR de **cupom na fonte**" como
  caso próprio. **Regra: o motor recebe a BASE como parâmetro**, e quem a calcula é o
  chamador — `ganho de alienação` na venda e no vencimento (F5), **valor integral** no cupom
  (aqui). *O que se reusa é a tabela regressiva, as faixas, o IOF e o arredondamento; o que
  NÃO se reusa é a fórmula da base, e é por isso que ela tem nome nas duas pontas.* **
  Vencimento:** `resgate` (o `tipo` do livro, que **é** vencimento — V2 do F3) com
  `qtd_delta = -qtd` e valor = qtd × PU final, mais a tributação da ponta e a liquidação em
  D+1 útil. O fan-out mercado→clientes acontece **aqui**, no join com posições — o Hub
  publica o fato **uma** vez.

  **Instrumento e sinal de cada linha: a tabela de LINHAS DERIVADAS da V2 do F3, copiada.**
  O principal (`cupom` ou `resgate`) fica no **instrumento do título**; `ir_retido`, `iof` e
  `a_liquidar` ficam em **`caixa:a_liquidar`**, com o `a_liquidar` **BRUTO** e os tributos
  debitando-o; a liquidação move o **saldo** para `caixa:BRL`. Nada disso se decide aqui —
  se algum número não fechar, a divergência é com o F3, e é lá que ela se resolve.

  **As chaves são as da V3 do F3, e são MAIS de uma por fato.** A §7.3 exige que **um**
  cupom vire, **por cliente**, no mínimo quatro linhas (cupom, `ir_retido`, `a_liquidar` e a
  liquidação de **duas pernas**), e um vencimento outras tantas. Usar uma chave só para
  todas — o que o rascunho anterior mandava — faz elas colidirem no
  `UNIQUE (cliente_id, ref_externa)` **já na primeira gravação**, e o Pronto "reentrega do
  mesmo fato é no-op" não pegaria isso, porque a colisão é entre linhas do **mesmo** cliente.
  A convenção completa está fechada no F3; aqui ela se copia, não se inventa:

  ```
  cupom:      cupom:<instrumentoId>:<data>        (o movimento principal)
              ir:cupom:<instrumentoId>:<data>
              aliq:cupom:<instrumentoId>:<data>
              liq:cupom:<instrumentoId>:<data>:aliq
              liq:cupom:<instrumentoId>:<data>:brl
  vencimento: venc:<instrumentoId>:<data>         (o movimento principal)
              ir:venc:... · iof:venc:... · aliq:venc:...
              liq:venc:<instrumentoId>:<data>:aliq
              liq:venc:<instrumentoId>:<data>:brl
  ```

  Por último de propósito, e a §9 diz a razão: "é o tema mais sutil e tudo acima funciona sem
  ele".

  **Decisões desta fase:**

  - **O join usa a posição NA DATA do evento, derivada do livro** (Σ `qtd_delta` com
    `data_evento <= data`), **não** `posicao_corrente`. Com o nome do arquivo: é **a dobra
    da V1 com corte `D = a data do evento`**, a mesma do snapshot do F7, precisando aqui só
    da coluna `quantidade` — não é um terceiro jeito de somar o livro. **Isto é um DESVIO
    POR CORREÇÃO do texto literal da §7.3 — registre-o e grave-o na memória ao fechar a
    fase**, na mesma convenção do `ref_externa NOT NULL` do F3: a §7.3 diz "para cada cliente com
    `posicao_corrente > 0` no instrumento na data", e a fase usa o livro. Sem o rótulo, o
    executor que ler a §7.3 — como o próprio prompt manda — encontra a contradição sem
    explicação e tende a seguir a `ARQUITETURA`. *Rejeitado:* ler a projeção — ela é
    o **agora**, e uma corpaction atrasada precisa do **então**; usar a projeção geraria
    cupom para quem já vendeu e omitiria quem vendeu depois da data. O livro é a verdade; a
    projeção é descartável.
  - **Corpaction atrasada NÃO ganha caminho próprio:** gera movimentos retroativos e cai no
    **gatilho 1** do worker do F7. *Rejeitado:* um recálculo específico de corpaction —
    duplicaria a engrenagem que a §7.4 diz ser única ("fluxos 2 e 5 terminam no MESMO
    worker"), e duas engrenagens que fazem a mesma coisa divergem na primeira correção
    aplicada em uma só.
  - **Cupom e vencimento escrevem no MESMO livro** que compra e venda, sem tipo de tabela nem
    caminho especial — venda voluntária e resgate compulsório são indistinguíveis para o
    extrato, e isso é **projeto** (§8: "fluxos 3 e 4 escrevem no MESMO livro"). *Rejeitado:*
    tabela de eventos corporativos separada — bifurcaria a projeção contábil em duas fontes
    que divergem.
  - **O tipo da ação vem MARCADO no evento** e o consumidor decide só por ele; nada de
    inferir cupom × vencimento por presença de campo ou por valor (§10.32). `acao`
    desconhecida **não vira movimento nenhum** e vai para o parking com o motivo
    **`acao_desconhecida`** — o nome está escrito aqui e já consta da lista fechada do F4,
    porque aquela fase declarou o cabeçalho `x-custodia-motivo` **obrigatório e sem
    default**, e "com motivo nomeado" sem dizer o nome é justamente o default voltando pela
    porta dos fundos. Nunca é tratada como cupom por semelhança.
  - **Pendência aberta: NÃO EXISTE reversão de corpaction, e a ausência é declarada aqui
    em vez de descoberta na primeira vez que um cupom for lançado errado.** A família `est:`
    da V3 do F3 é definida sobre `<tradeIdEstorno>`, e cupom e vencimento **não têm
    `tradeId`**: o `<fato>` deles é `cupom:<instrumentoId>:<data>` / `venc:…`. Some-se que o
    único evento de reversão que esta casa consome é `TradeRegistered` com
    `operacao = estorno`, de **Operações**, que não conhece corpactions. **Consequência: um
    cupom lançado errado — pelo Hub, ou por bug do fan-out — é IRREVERSÍVEL num livro
    append-only.** Isso pode ser aceitável (o gerador do Hub é determinístico, e o fato é
    derivado do próprio instrumento), mas é **ausência decidida em outro lugar deste arquivo
    e ausência silenciosa aqui**, e a diferença importa: toda outra família de linha tem
    caminho de reversão escrito. **Pendência aberta — decidir antes de escrever código desta
    fase**, e as opções são duas: (a) estender a gramática `est:` para as famílias de
    corpaction (`est:cupom:<instrumentoId>:<data>`, `est:ir:cupom:…`), o que é edição da **V3
    do F3** e não desta fase — e o F3 é a fase que se declara "a última barata" para o
    vocabulário; ou (b) declarar por escrito que corpaction não se reverte, e que o reparo é
    **estorno manual por comando administrativo** com a gramática decidida no momento do
    incidente — que é a opção que este roadmap recusa em toda outra ponta. *Não escolhi por
    você porque (a) mexe num vocabulário que uma fase anterior fechou e (b) aceita dado
    irreparável; as duas são decisões de dono, e nenhuma é urgente até o Hub publicar
    `corpactions.td`, que é a dependência externa que já bloqueia esta fase.*
  - **Enum mínimo, e ele já está fechado no F3 (V1):** `cupom` e `resgate` (que no livro
    **é** vencimento) já estão na lista, e nada precisa crescer nesta fase. *Rejeitado:* a
    taxonomia completa da B3 no dia 1 — cada valor a mais é um valor no CHECK de uma tabela
    append-only que ninguém exercita. Dividendo, desdobramento, grupamento e bonificação são
    fase 2 (§9 item 6), e entram por migration **quando** entrarem.

  **Configuração: nenhuma.**

  **Âncoras:** `ARQUITETURA` §7.3 (ramo CorporateActionObserved), §8.5 Fluxo 4, §5.1
  (contrato), §7.4 gatilho 3 ("cai no gatilho 1"), §9 item 5 e a nota final sobre começar o
  enum pelo que as carteiras reais têm, §11 (tributação portada do simulador); `PADROES`
  §10.21, §10.31, §10.32.

  **Prompt:**
  ```
  ANTES DE COMECAR: confira se o Hub ja publica corpactions.td. A secao 9 item 5 do
  ARQUITETURA poe o GERADOR DETERMINISTICO de cupom/vencimento NO HUB (repo
  ../hub-precos), nao aqui. Se ele nao existir, esta fase esta BLOQUEADA por outro repo
  e a vez e de la — diga isso e pare, nao improvise um gerador nesta casa.

  Implemente o handler de CorporateActionObserved (ARQUITETURA 7.3 ramo
  CorporateActionObserved, Fluxo 4 da 8.5, contrato na 5.1).

  CODIGO SEM COMENTARIO NENHUM nos .cs — nem //, nem /* */, nem ///.

  CUPOM: para cada cliente posicionado no instrumento NA DATA -> movimento `cupom`
  (qtd_delta 0, valor = qtd x valorPorUnidade) + ir_retido pela tabela regressiva do
  prazo + a_liquidar -> liquidacao. REUSE o motor do F5, nao reescreva — inclusive o JOB
  DE LIQUIDACAO, que grava as duas pernas em D+1 util.
  MAS A BASE DO CUPOM E O VALOR INTEGRAL, E REUSAR O MOTOR SEM ISSO SUBTRIBUTA TODO CUPOM.
  O motor do F5 calcula sobre o GANHO DE ALIENACAO (valorFinanceiro - preco_medio x
  quantidade); um cupom NAO TEM CUSTO DE AQUISICAO — e renda, e e por isso que a V1 do F3
  manda `cupom` nao tocar custo_total nem preco_medio —, entao subtrair preco medio dele
  daria base zero ou negativa e NENHUMA linha de ir_retido. A ARQUITETURA 11 trata "IR de
  CUPOM NA FONTE" como caso proprio. REGRA: O MOTOR RECEBE A BASE COMO PARAMETRO, e quem a
  calcula e o chamador — ganho de alienacao na venda e no vencimento (F5), VALOR INTEGRAL
  no cupom. Reuse a tabela regressiva, as faixas, o IOF e o arredondamento; NAO reuse a
  formula da base.
  E ESTA FASE HERDA A PENDENCIA BLOQUEANTE DO `prazo` (F5): a aliquota do cupom depende
  dela do mesmo jeito. Se a pendencia estiver aberta, PARE — vale o mesmo aviso do F5.
  VENCIMENTO: `resgate` (o tipo do LIVRO, que E vencimento — V2 do F3) com
  qtd_delta = -qtd e valor = qtd x PU final + tributacao da ponta + liquidacao em D+1
  util.

  IDEMPOTENCIA — AS CHAVES SAO AS DA V3 DO F3, E SAO MAIS DE UMA POR FATO. A 7.3 exige
  que UM cupom vire, POR CLIENTE, no minimo quatro linhas (cupom, ir_retido, a_liquidar
  e a liquidacao de DUAS PERNAS). Uma chave so para todas elas COLIDE no UNIQUE
  (cliente_id, ref_externa) JA NA PRIMEIRA GRAVACAO — e o Pronto "reentrega e no-op" nao
  pegaria, porque a colisao e entre linhas do MESMO cliente. Copie a convencao, nao a
  invente:
    cupom:      cupom:<instrumentoId>:<data>              (movimento principal)
                ir:cupom:<instrumentoId>:<data>
                aliq:cupom:<instrumentoId>:<data>
                liq:cupom:<instrumentoId>:<data>:aliq
                liq:cupom:<instrumentoId>:<data>:brl
    vencimento: venc:<instrumentoId>:<data>               (movimento principal)
                ir:venc:... · iof:venc:... · aliq:venc:...
                liq:venc:<instrumentoId>:<data>:aliq
                liq:venc:<instrumentoId>:<data>:brl

  DRENE o custodia.parked do motivo `tipo_nao_tratado_corpactions`, pelo drenador e pela
  mecanica do F4 (le messages_ready antes, consome no maximo N, republica o resto,
  parada CLASSIFICADA; nao inspecione routing key). Sao os UNICOS eventos estacionados
  que NAO se recuperam por REST, e e para eles que o estacionamento existiu.

  DECISOES JA TOMADAS:
  1. O join usa a posicao NA DATA do evento, derivada do LIVRO (soma de qtd_delta com
     data_evento <= data), NAO posicao_corrente — e isso E A DOBRA DA V1 COM CORTE
     D = a data do evento, a mesma do snapshot do F7, usando aqui so a coluna quantidade.
     ISTO E UM DESVIO POR CORRECAO DO TEXTO LITERAL DA 7.3 ("para cada cliente com
     posicao_corrente > 0 no instrumento na data") — REGISTRE-O e grave na memoria ao
     fechar a fase, igual ao ref_externa NOT NULL do F3. A projecao e o AGORA; uma
     corpaction atrasada precisa do ENTAO — usar a projecao geraria cupom para quem ja
     vendeu e omitiria quem vendeu depois da data.
  2. Corpaction atrasada NAO ganha caminho proprio: gera movimentos retroativos e cai no
     GATILHO 1 do worker do F7, sem codigo novo. A 7.4 diz que a engrenagem e unica.
  3. Cupom e vencimento escrevem no MESMO livro que compra e venda — sem tabela separada
     e sem caminho especial. Venda voluntaria e resgate compulsorio sao indistinguiveis
     para o extrato, e isso e projeto (secao 8).
  4. O tipo da acao vem MARCADO no evento; NAO infira cupom x vencimento por presenca de
     campo ou por valor (PADROES 10.32). `acao` desconhecida NAO vira movimento nenhum e
     vai para o parking com o motivo `acao_desconhecida` — esse nome exato, ja previsto na
     lista fechada do F4: aquela fase declarou x-custodia-motivo OBRIGATORIO E SEM
     DEFAULT, e "com motivo nomeado" sem dizer o nome e o default voltando pela porta dos
     fundos.
  5. Enum minimo: `cupom` e `resgate` (que no livro E vencimento) JA ESTAO na lista V1
     fechada no F3 — nao acrescente valor nenhum nesta fase. NAO a taxonomia completa da
     B3: cada valor a mais e um valor no CHECK de uma tabela append-only que ninguem
     exercita. Dividendo, desdobramento, grupamento e bonificacao sao fase 2, e entram
     por migration QUANDO entrarem.

  O fan-out por cliente e mais um laco cuja condicao de parada tem que dizer se parou por
  COMPLETUDE ou por LIMITE (PADROES 10.31): confirmar o corpaction com metade dos
  clientes processados repete o defeito do F7 com outra roupa.

  Ao final, guardiao-padroes e DEPOIS revisor, em serie, nunca em paralelo. Achado grave
  corrigido pede AS DUAS de novo sobre o delta. Peca ao guardiao que confira tambem os
  textos que VOCE escreveu. Commite antes de rodar o revisor.
  ```

  <br>**Pronto:** (**permissiva**) um `corpactions.td` de cupom sobre um instrumento que
  **N ≥ 2** clientes possuem gerando N conjuntos completos de movimentos, um por cliente —
  e a conferência é por **CONTAGEM DE LINHAS POR CLIENTE igual ao conjunto completo da
  §7.3** (cupom, `ir_retido`, `a_liquidar` e as **duas pernas** da liquidação), não "N
  conjuntos": contar conjuntos passa mesmo com o conjunto incompleto, que é como a colisão
  de chaves se esconde. **E o conjunto completo só existe em DOIS DIAS DIFERENTES — a
  hipótese do fixture fica escrita, como o F5 fez com o "preço congelado":** cupom,
  `ir_retido` e `a_liquidar` nascem em **D**, no handler; as **duas pernas** nascem em
  **D+1 útil**, por **job** (é o desvio (1) da lista canônica). *Uma asserção de contagem
  feita logo depois do handler encontra **três** linhas e reprova a implementação correta.* O
  fixture posiciona a conferência **depois de o job de liquidação de D+1 ter rodado** — com o
  relógio do teste avançado, não com espera — e diz isso no nome do teste; a contagem em **D**
  é uma asserção **separada**, de três linhas; um vencimento zerando a posição e creditando o caixa em D+1 útil;
  **a passagem completa do drenador do motivo `tipo_nao_tratado_corpactions` devolvendo
  COMPLETUDE e residual 0, com `n_motivo ≥ 1`** — o
  teste **planta** a mensagem antes de rodar, e **aqui a cláusula do `n_motivo ≥ 1` é a que
  mais pesa em todo o roadmap**: sem nenhum corpaction estacionado por engano (binding que
  nunca funcionou, consumidor que deu ack-e-descarta, fila purgada — e a fila pode estar
  **cheia de outros motivos** em qualquer um dos três), a passagem devolveria
  COMPLETUDE e residual 0 **por vacuidade** se o desfecho VAZIO_DO_MOTIVO não existisse, e
  fechar esta fase com o checkbox marcado é
  perder corpaction **em definitivo** — são os únicos eventos estacionados que **não** se
  recuperam por REST. Continua valendo o resto: é a varredura, não um gauge (que zera no
  deploy) e não inspeção de routing key no broker (que conta mensagens, não tipos); e o extrato
  de movimentação do F8 mostrando `cupom` e `ir_retido`
  como linhas próprias, conferíveis contra o extrato da corretora.
  (**Estrita**) (a) reentrega do **mesmo** fato é no-op para **todos** os N — e o teste tem
  que ter **N > 1**: com um cliente só, `UNIQUE (cliente_id, ref_externa)` passa por
  acidente. Mais: **a PRIMEIRA gravação de um único cliente já prova que as chaves não
  colidem entre si** — as 4–5 linhas entram juntas, na mesma transação, e o teste conta
  cada `ref_externa` da família (`cupom:…`, `ir:cupom:…`, `aliq:cupom:…`,
  `liq:cupom:…:aliq`, `liq:cupom:…:brl`) exatamente uma vez; (b) cliente **sem** posição no instrumento naquela data **não** ganha linha
  nenhuma, e quem zerou a posição **antes** da data também não; (c) não existe movimento de
  cupom sem `CorporateActionObserved` correspondente (varredura do livro); (d) `acao`
  desconhecida não vira movimento e vai para o parking com motivo nomeado — nunca é tratada
  como cupom por semelhança — o motivo é `acao_desconhecida`, conferido pelo nome e não por
"foi para o parking"; (e) uma corpaction processada **com atraso** gerando movimentos
  retroativos que caem no gatilho 1 do worker do F7 e reversionam os snapshots do intervalo,
  **sem caminho novo** — que é o que a §7.4 afirma e esta fase tem que provar; (f) o laço de
  fan-out interrompido no meio **não** confirma o evento.

---

## Ao fechar cada F

Marque o checkbox, referencie o PR, e leve o que doeu para `PADROES.md` §10 (regra técnica
aprendida por incidente) ou `LEIA-ME-KIT.md` (armadilha de infra, erro de condução). Commit
e PR registram **quando**; aqueles dois registram **o que não repetir** — e são os únicos
que o próximo repo lê. A memória em arquivo carrega sozinha no início da sessão; o grafo MCP
só aparece se alguém buscar, então fato que a próxima sessão precisa saber sem perguntar vai
nos **dois**.

Três coisas que este roadmap pede em toda fase, e que não são cerimônia:

1. **`guardiao-padroes` e DEPOIS `revisor`, em série, nunca em paralelo** — o revisor muta a
   implementação de propósito para provar que um teste é vácuo, e o guardião lendo esse
   estado reporta como defeito real algo que já não existe. **Achado grave corrigido pede AS
   DUAS de novo sobre o delta:** no F2 do `operacoes` cada rodada de correção gerou um
   defeito novo que só a revisão seguinte pegou.
2. **Peça ao guardião que confira também os textos que VOCÊ escreveu** (este roadmap, o
   `PADROES.md`, a nota de fecho): no F2 do `operacoes` os quatro últimos defeitos foram do
   orquestrador, não dos executores, e nenhuma revisão estava apontada para eles.
3. **Commite antes de rodar o revisor.** Ele muta a implementação de propósito, e um
   `git checkout` numa entrega não commitada apaga o trabalho **em silêncio** — o arquivo
   continua existindo, compila, e os testes passam. E revisão sobre arquivos novos começa por
   `git status --short`: `git ls-files` e `git diff` não mostram o que ainda não foi
   rastreado.

6. **Antes de despachar uma fase, releia as PENDÊNCIAS dela — elas estão no cabeçalho da
   fase e nas decisões, com o nome, o estado, quem decide e as opções.** Há **três** abertas
   neste arquivo: `caixa:BRL` nunca ser debitado (**bloqueia o F3**, e a correção é um campo
   novo na §5.1 do `../plataforma-docs`), a definição de `prazo` (**bloqueia o F5 e, por
   herança, o F9**) e a reversão de corpaction (**aberta no F9**, não bloqueante até o Hub
   publicar `corpactions.td`). Fechar uma delas é editar a fase dona **e** os critérios de
   Pronto que a citam — o Pronto (g) do F8 diz, dentro dele, o que volta a valer quando a
   primeira fechar.

E duas do dado, que valem enquanto este livro for append-only:

4. **Toda prova em produção grava linha que não sai.** Decida a limpeza (`TRUNCATE`) **antes**
   do POST ou da publicação, conferindo as tabelas em 0/0 — depois do 201 a decisão já foi
   tomada por você. E saiba que o `TRUNCATE` deixa de ser saída no dia em que houver dado
   real ao lado.
5. **Nunca purgue a `custodia.prices`.** Ela guarda backlog real desde o F2. Retire mensagem
   de prova com `basic.get` + ack da mensagem específica.
