# `infra/rabbitmq/` — topologia do consumidor, declarada pelo deploy

Um arquivo: `declare-topology.sh`, invocado pelo job `deploy` do
`.github/workflows/ci.yml` depois do `up -d`. Ele **declara** (idempotente) e
**verifica** (bloqueante) a topologia da §5 do `../plataforma-docs/ARQUITETURA.md` pelo
lado do consumidor. O "por quê" de cada decisão está em comentário dentro do próprio
script, junto da linha que ele explica — este README é o mapa, não a duplicata.

## Por que isto existe, e por que antes do consumidor

Evento publicado num exchange topic **sem binding casando é descartado em silêncio**,
com o publish confirmado e o produtor marcando sucesso. Operações publica
`trades.registered` em produção desde 2026-09-06. Entre a recriação do broker e a
redeclaração da fila do consumidor existe uma janela em que tudo fica verde — deploy,
healthcheck, outbox zerada — e os eventos evaporam (`LEIA-ME-KIT.md`, "Perder o volume
do broker apaga fila e binding, e a outbox não protege contra isso").

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
  Já as duas verificações de **saída** — a `custodia.retry` ter esvaziado, e a mensagem
  de prova ter deixado a `custodia.prices` — são de **igualdade por necessidade**, porque
  `>=` ali seria vácuo (`>= antes` é trivialmente verdadeiro). A da `custodia.retry` é
  bloqueante (aquela fila não tem outro publicador nesta fase, então a igualdade é
  determinística); a da `custodia.prices` é **informativa**: divergir gera AVISO e nunca
  reprova, porque ali um `trades.registered` real pode ter chegado na janela;
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

## Desfecho assimétrico (é o coração do passo)

| situação | exit | desfecho no deploy |
|---|---|---|
| `401` na management API | 10 | **reprova**, rápido, sem repetir — é o secret **deste** repo |
| broker inacessível **depois** do laço de espera | 11 | `::warning::` e **segue** |
| fila ou binding ausente depois da declaração | 12 | **reprova** — a declaração é nossa |
| exchange `prices` presente com propriedades divergentes | 13 | **reprova e não redeclara** |
| prova de fumaça / fanout / retry falhando | 14, 15, 16 | **reprova** — um código por prova, para o log dizer qual |
| **a nossa ferramenta não rodou** (imagem não pôde ser puxada, rede docker ausente, daemon fora) | 17 | **reprova** — a culpa é deste repositório, não do broker |

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
- `RABBITMQ_MANAGEMENT_HOST=<endereço inexistente>` — prova que o laço de espera roda e
  que o script sai com **11**, que é o único código que o `.github/workflows/ci.yml`
  mapeia para `::warning::` em vez de `exit 1`. Rodar o script à mão prova o exit code;
  quem transforma isso em `::warning::` com o deploy verde é o `case` do workflow.
  **É assim que se prova esse caminho** — derrubar o broker de propósito dispararia
  `hub-relay-falha-persistente` no plantão do `hub-precos`, que é serviço de outro repo.
