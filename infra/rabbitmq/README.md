# `infra/rabbitmq/` — topologia do consumidor, declarada pelo deploy

Um arquivo: `declare-topology.sh`, invocado pelo job `deploy` do
`.github/workflows/ci.yml` depois do `up -d`. Ele **declara** (idempotente) e
**verifica** (bloqueante) a topologia da §5 do `../plataforma-docs/ARQUITETURA.md` pelo
lado do consumidor. O "por quê" de cada decisão está em comentário dentro do próprio
script, junto da linha que ele explica — este README é o mapa, não a duplicata.

## Por que isto existe, e por que antes do consumidor

Evento publicado num exchange topic **sem binding casando é descartado em silêncio**,
com o publish confirmado e o produtor marcando sucesso. Entre a recriação do broker e a
redeclaração da fila do consumidor existe uma janela em que tudo fica verde — deploy,
healthcheck, outbox zerada — e os eventos evaporam (`LEIA-ME-KIT.md`, "Perder o volume
do broker apaga fila e binding, e a outbox não protege contra isso").

**O que estava aberto, medido em 2026-09-08 e não inferido:** o relay do `operacoes`
tinha conexão ABERTA com o broker, e o vhost `/` não tinha **fila nenhuma**. Mas a
`outbox` do `operacoes` tinha **0 linhas** e a tabela `operacoes` **0 registros** — o
relay marca `publicado_em` e não apaga, então isso é prova de que **nenhum trade chegou
a ser publicado**. A janela estava aberta e ninguém tinha caído nela: esta fase é
**preventiva**, não remediadora. A distinção importa porque a conclusão oposta —
"trades estão sendo perdidos agora" — é fácil de inferir da conexão aberta e é **falsa**;
foi exatamente esse o erro que o `LEIA-ME-KIT` já registra sobre este mesmo broker.

Topologia de consumidor é **estado do broker, não do código**, e não sobrevive ao
volume. Por isso ela é declarada por um passo de deploy, e não pelo boot da aplicação:
nesta fase (F2) **não existe aplicação que fale com o broker**.

**A fila acumula de propósito** desde o instante em que o binding existe, esperando o
consumidor do F4. Isso é a entrega, não efeito colateral — e é por isso que:

- **nunca** se purga a `custodia.prices` (o purge é da fila inteira e apagaria backlog
  real). A limpeza de mensagem de prova é `basic.get` + ack **da mensagem específica**;
- **nunca** se publica com routing key `trades.registered` numa prova (iria para o livro
  append-only no F4 e ficaria lá para sempre). A prova usa `prices.smoke`;
- as provas de **roteamento** são de crescimento (`>= antes + 1`) e reprovam: é o que
  prova que o binding entregou, e tráfego real chegando na janela não pode derrubá-las.
  **A prova de fumaça, porém, é CONDICIONAL:** ela só publica quando a `custodia.prices`
  está vazia. Com backlog — o estado normal a partir do primeiro trade — ela é **pulada**,
  por duas razões que se somam: a limpeza seria impossível (a nossa mensagem não estaria
  na cabeça) e deixaria lixo crescendo a cada deploy; e ela seria **vácua**, porque com
  uma fila de terceiro bindada em `prices` e tráfego na janela o delta cresce mesmo com o
  nosso binding removido. Nesse caso o roteamento daquele deploy é provado pela
  **comparação de conjunto** dos bindings, que é estrita nas duas direções e lê a mesma
  fonte de verdade; o log diz qual dos dois caminhos foi usado.
  Já as duas verificações de **saída** — a `custodia.retry` ter esvaziado, e a mensagem
  de prova ter deixado a `custodia.prices` — são de **igualdade por necessidade**, porque
  `>=` ali seria vácuo (`>= antes` é trivialmente verdadeiro). **As duas são
  informativas — avisam e nunca reprovam —, e por razões diferentes:** na
  `custodia.prices` porque um `trades.registered` real pode ter chegado na janela; na
  `custodia.retry`, que não tem outro publicador, porque a igualdade é refém da
  **defasagem de `messages_ready`** contra o `collect_statistics_interval = 60000` do
  broker, e uma medição atrasada reprovaria um deploy sadio. O **veredito** do ciclo de
  retry não é nenhuma das duas: é o marcador lido de volta da fila-sonda (abaixo);
- nenhuma prova é de **valor absoluto**: exigir `messages_ready == 0` reprovaria o deploy
  no instante em que o primeiro trade real chegasse, e esta fase existe para que ele
  chegue.

## O que ele declara

| objeto | tipo | argumentos |
|---|---|---|
| exchange `prices` | topic, durable | nenhum — é do **Hub**; divergiu, **reprova e não redeclara** |
| `custodia.prices` | quorum, durable | `x-delivery-limit: 20`, `x-dead-letter-exchange: custodia.dlx` |
| `custodia.prices.dlq` | quorum, durable | `x-delivery-limit: -1` |
| `custodia.parked` | quorum, durable | `x-delivery-limit: -1` |
| `custodia.retry` | **classic**, durable, sem consumidor | `x-message-ttl: 30000`, `x-dead-letter-exchange: custodia.retry.dlx` |
| `custodia.dlx`, `custodia.parking`, `custodia.retry.in`, `custodia.retry.dlx` | **fanout**, durable | nenhum |

Bindings: `prices` → `custodia.prices` com `prices.#`, `corpactions.#`, `eod.ready`,
`trades.registered`; `custodia.dlx` → `custodia.prices.dlq`; `custodia.parking` →
`custodia.parked`; `custodia.retry.in` → `custodia.retry`; `custodia.retry.dlx` →
`custodia.prices`.

Os quatro exchanges de infraestrutura são **nossos** e são **fanout**. Mensagem
dead-letrada **conserva a routing key original**; um `topic` com binding que não case
tudo descartaria a mensagem morta em silêncio dentro da nossa própria infraestrutura.
`fanout` ignora a routing key por construção — remove a condição em vez de vigiá-la.

## Como o ciclo de retry é provado, e por que não pela contagem

`custodia.retry.in` → `custodia.retry` → (TTL de 30 s) → `custodia.retry.dlx` →
`custodia.prices`. Provar isso por **contagem** não funciona, e a fase já tentou: provar
que a mensagem **saiu** da `custodia.retry` não prova que ela **chegou** — com o
`custodia.retry.dlx` sem binding, o broker a descarta e a contagem cai igual. E olhar a
`custodia.prices` crescer é satisfeito por qualquer `trades.registered` real que chegue
na janela.

Então o veredito é por **marcador**, numa **fila-sonda temporária** bindada ao
`custodia.retry.dlx`. Como esse exchange é `fanout`, a mensagem chega à `custodia.prices`
**e** à sonda; a sonda só tem tráfego nosso, então o `basic.get` nela é determinístico,
não disputa cabeça de fila com mensagem real, e não gasta tentativa de entrega de
ninguém. A sonda nasce com `x-expires` e é apagada no fim.

Duas consequências que andam juntas e que quem mexer num lado precisa ver no outro:

- a sonda participa do conjunto de destinos que `verify_binding_exists` confere no
  `custodia.retry.dlx` — que é comparação **exata**, e não se relaxa;
- por isso o script **varre sondas órfãs** (execução anterior morta entre bindar e
  apagar) no início da verificação, por prefixo exato, logando cada uma. Sem a varredura,
  o lixo de uma execução morta reprovaria o deploy seguinte com um diagnóstico enganoso.

## Desfecho assimétrico (é o coração do passo)

| situação | exit | desfecho no deploy |
|---|---|---|
| `401` na management API | 10 | **reprova**, rápido, sem repetir — é o secret **deste** repo |
| broker inacessível **depois** do laço de espera | 11 | `::warning::` e **segue** |
| fila ou binding ausente depois da declaração | 12 | **reprova** — a declaração é nossa. Também usado quando uma declaração falha, e quando fila/exchange desaparece (ou o management deixa de responder) **no meio** de uma prova, depois de autenticar |
| exchange `prices` presente com propriedades divergentes | 13 | **reprova e não redeclara** |
| prova de fumaça / fanout / retry falhando | 14, 15, 16 | **reprova** — um código por prova, para o log dizer qual |
| **a nossa ferramenta não rodou, ou o alvo não está na rede** (imagem não pôde ser puxada, rede docker ausente, daemon fora, falha do container do `jq`, ou `RABBITMQ_MANAGEMENT_HOST`/`CUSTODIA_RABBITMQ_NETWORK` apontando para nome que não resolve) | 17 | **reprova** — a culpa é deste repositório, não do broker |
| **a limpeza removeu da fila algo que NÃO era a nossa prova** | 18 | **reprova, e NÃO reexecute o deploy** — é o único código que significa dano já consumado; reexecutar roda o mesmo caminho destrutivo. Investigação manual primeiro |

O `::warning::` da linha 2 só é legítimo **porque existe** a regra
`custodia-topologia-ausente` (`infra/grafana/cloud/rules-custodia.yaml`) cobrindo a
janela até o próximo deploy. **Sem essa regra publicada na nuvem, a linha 2 vira
reprova** — quem for removê-la tem que reabrir a decisão no passo de deploy primeiro.

## Credencial

`RABBITMQ_USER` e `RABBITMQ_PASSWORD`, consumidas **só por este script**. Por isso
entram em **duas** listas — o `envs:` do `ssh-action` e o `env:` do **mesmo** step — e
**não** nos composes, nem no `.env` da VPS, nem nas dummies do config gate
(`PADROES.md` §10.33). São normalizadas com `tr -d '\r\n'` na origem (§10.4).

Toda chamada sai de um **container cliente na rede `plataforma`**, nunca por loopback e
nunca por `docker exec` no broker (§10.3), com `curl -sS -w '%{http_code}'` — `curl -s`
transforma falha de conexão em saída vazia e apaga a diferença entre "não conectou" e
"conectou e recusou".

## Rodar à mão

```bash
RABBITMQ_USER=... RABBITMQ_PASSWORD=... ./infra/rabbitmq/declare-topology.sh
```

Duas variáveis de ambiente existem para **provar** o script, documentadas dentro dele,
junto do ponto onde agem:

- `CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO=<chave que ninguém binda>` — injeta uma routing key
  inexistente na lista **esperada**, contra um broker correto, e prova que a comparação
  de conjuntos sabe dizer "não" (§10.8: asserção sem controle negativo passa também
  quando o mecanismo de detecção quebrou);
- `RABBITMQ_MANAGEMENT_HOST` — e aqui há **dois** casos, que o script distingue de
  propósito e que dão códigos diferentes. **Medidos em produção, 2026-09-09:**

  | valor | exit | tempo | por quê |
  |---|---|---|---|
  | `192.0.2.1` (IP de TEST-NET-1, não roteável) | **11** | ~40 s | o laço roda inteiro, `curl (28) timed out`; é o único código que o workflow mapeia para `::warning::` |
  | `nome.que.nao.existe` | **17** | ~8 s | `curl (6) Could not resolve host`. Numa rede docker, nome que não resolve significa que o alvo **não está naquela rede** — configuração NOSSA, acionável aqui, e não melhora com espera |

  Para provar o caminho do `::warning::` use o **IP**, não um nome: até a correção do
  `curl (6)` os dois davam 11, e um procedimento escrito com nome hoje prova 17 e faz
  quem o repetir concluir que o mapeamento quebrou. Rodar o script à mão prova o exit
  code; quem transforma 11 em `::warning::` com o deploy verde é o `case` do workflow.
  **É assim que se prova esse caminho** — derrubar o broker de propósito dispararia
  `hub-relay-falha-persistente` no plantão do `hub-precos`, que é serviço de outro repo.
