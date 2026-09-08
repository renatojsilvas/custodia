# Dashboard e alertas do Custodia

`dashboards/custodia.json` — painel do Grafana Cloud para este serviço.
`cloud/rules-custodia.yaml` — regras de alerta desta própria Custódia, publicadas no
Grafana Cloud.

## Por que os arquivos moram aqui e são aplicados de outro repo

Log é *push*: quem inicia a conexão é a própria aplicação, então ela só precisa saber o
endereço do destino (`Loki__Uri=http://alloy:3100`) — resolve inteiro neste repo.
**Métrica é *pull*: quem inicia é o coletor.** O Alloy do `tesouro-direto-api` é quem
abre a conexão para raspar `/metrics` deste serviço, então é ele quem precisa saber o
endereço do alvo (`custodia-app:8080`) — e o coletor mora no repo vizinho, não aqui.
Por isso o dashboard e as regras deste serviço só viram observabilidade de verdade depois
de edições em `../tesouro-direto-api` (ver `LEIA-ME-KIT.md`, seção "No repo do
`tesouro-direto`"):

1. alvo do scrape em `infra/alloy/config.alloy`, com `job="custodia"`;
2. cópia de `dashboards/custodia.json` para `infra/grafana/dashboards/` lá;
3. cópia de `cloud/rules-custodia.yaml` para `infra/grafana/cloud/` lá (nunca
   `rules.yaml`: esse nome já é das 21 regras do TD, e o PUT do publicador as
   sobrescreveria);
4. **cinco pontos dentro do `apply-cloud.sh` de lá, um por serviço vizinho** — não
   existe uma "lista" onde se acrescenta o nome do serviço vizinho: a única lista fixa
   do script (`for d in tesouro-direto host` — localize por `grep`, não por número de
   linha: ele mudou de 281 para 325 dentro desta mesma fase) publica os dashboards
   do TD, na pasta *TesouroDireto*. Um serviço vizinho entra por blocos `if -f`
   PRÓPRIOS, seguindo o mesmo padrão já usado para o Hub e para o Operações
   (conferido ao vivo no arquivo):
   1. `FOLDER_UID_CUSTODIA=$(gc_folder_uid Custodia)` no topo do script (cf.
      `FOLDER_UID_HUB` e `FOLDER_UID_OPERACOES`) — pasta própria da Custódia, nunca a
      do TD;
   2. bloco `if [ -f infra/grafana/cloud/rules-custodia.yaml ]` publicando o grupo de
      regras NESSA pasta (cf. os blocos "Regras do Hub de Preços"/"Regras do
      Operações");
   3. bloco `if [ -f infra/grafana/dashboards/custodia.json ]` publicando o dashboard,
      guardado por uma flag `CUSTODIA_DASHBOARD_PUBLICADO` (cf.
      `HUB_DASHBOARD_PUBLICADO` e `OPERACOES_DASHBOARD_PUBLICADO`);
   4. bloco de conferência da CONTAGEM de regras publicadas na pasta Custodia, lida do
      próprio `rules-custodia.yaml` (cf. a conferência equivalente do Hub e do
      Operações);
   5. `uids_dashboards_verificar+=(custodia)`, condicionado à flag do item 3, para a
      verificação final de datasource resolvido.

   Errar esses cinco pontos tem duas consequências, e nenhuma é barulhenta: (a) quem cair na
   tentação de acrescentar `custodia` ao `for d in tesouro-direto host` publica o
   dashboard na pasta **TesouroDireto**, com HTTP 200 e sem reclamação nenhuma; (b) sem
   o bloco (2) as regras **nunca chegam à nuvem** — e `./scripts/verificar-f1.sh`
   **não pega**: ele faz `grep -oE "rules-[a-z0-9-]+\.yaml"` no publicador, encontra
   `rules-hub.yaml` e `rules-operacoes.yaml`, confirma que os dois existem como
   arquivo, e fica verde — sem nunca perguntar se `rules-custodia.yaml` também deveria
   estar na lista.

Mas o dashboard e as regras **descrevem a própria Custódia**, então é aqui — neste
repo — que devem ser versionados: quem muda uma métrica da Custódia tem que ver o
painel (ou o alerta) quebrar no mesmo diff. Quem publica é o
`scripts/grafana-cloud/apply-cloud.sh`, que vive no `tesouro-direto-api` — ele lê
`infra/grafana/` daquele repo e converge por API (inclusive apagando da nuvem o que sai
da fonte). Enquanto não houver um mecanismo de publicação próprio, aplicar exige copiar
os arquivos para lá, sempre que um deles mudar aqui:

```
cp infra/grafana/dashboards/custodia.json ../tesouro-direto-api/infra/grafana/dashboards/
cp infra/grafana/cloud/rules-custodia.yaml ../tesouro-direto-api/infra/grafana/cloud/
cd ../tesouro-direto-api && ./scripts/grafana-cloud/apply-cloud.sh
```

Duplicação consciente, e o custo é real: as duas cópias divergem em silêncio se só uma
for editada. Ao mexer nestes arquivos, copie de novo e rode o `apply-cloud.sh` — ou o
painel/alerta na nuvem descreve uma versão que não existe mais. O `apply-cloud.sh`
precisa ser invocado com `GC_GRAFANA_URL`, `GC_GRAFANA_TOKEN` e `TELEGRAM_BOT_TOKEN`
**exportados na invocação** — ele não lê o `.env` do serviço.

**O nome do arquivo de regras já nasce `rules-custodia.yaml` aqui** (diferente do
`hub-precos`, cujo arquivo próprio se chama `cloud/rules.yaml` e só ganha o sufixo
`-hub` ao ser copiado para o repo vizinho). Decisão deliberada: este repo descreve um
único serviço, sem risco de colisão de nome local, então manter o mesmo nome nas duas
pontas evita um `cp` com rename e a chance de esquecer o sufixo. O que importa —
`rules-custodia.yaml` e nunca `rules.yaml` no repo vizinho — está garantido porque o
nome já nasce certo.

## O que o dashboard mostra hoje

A Custódia não expõe endpoint de escrita de negócio (ADR-10; ver a seção "O que a
Custódia é, e o que ela NÃO é" no `CLAUDE.md` deste repo) — diferente do Operações, não
existe fase futura em que ela ganhe um `POST` de negócio, nem outbox/relay para o
RabbitMQ. O `custodia.json` tem hoje **10 painéis**, todos de infraestrutura:

`Target`, `Uptime do processo`, `Health checks`, `Requisições em andamento`,
`Requisições por status`, `Latência (p95 / p50)`, `Pool de conexões Postgres`,
`Memória`, `CPU`, `Coletas de lixo por geração`.

Não há painel de **consumo** ainda — a Custódia consome eventos, não os publica, então o
equivalente aqui às métricas de outbox/relay do Operações não é backlog/relay: é
profundidade da fila `custodia.prices`, idade da mensagem mais antiga, mensagens por
desfecho e DLQ/parking (`custodia.retry`, `custodia.parked`, `custodia.prices.dlq`).
Esse painel está agendado no F2 e no F4 do `docs/ROADMAP.md`, não antes — cada painel
novo só entra no mesmo diff que a métrica que ele lê; copiar um painel sem métrica real
por trás cria um painel permanentemente vazio, o mesmo modo de falha silenciosa que o
`apply-cloud.sh` já documenta para o dashboard `load-test-k6` (ver comentário lá). Foi
por esse motivo que os quatro painéis de outbox/relay que este arquivo chegou a ter —
herdados por cópia do molde `operacoes`, nunca alimentados por métrica real aqui — foram
removidos, não deixados vazios.

Dois painéis carregam contexto que não é óbvio pelo número (mesma nota do Hub, com a
diferença real deste serviço):

- **Pool de conexões** — o teto é 5 (`Custodia.Infrastructure/DependencyInjection.cs`,
  `NpgsqlMaxPoolSize`), por decisão de ORÇAMENTO: em produção a Custódia conecta no
  cluster Postgres COMPARTILHADO (`tesouro-direto-db`, ver `docker-compose.prod.yml`),
  dividido com `td_api`, `hub` e `operacoes` (ARQUITETURA §12). Encostar no teto é
  motivo para rever o orçamento do cluster, não só para subir o número.
- **Memória** — diferente do Hub (que não tem limite), o container da Custódia **tem**
  teto (192MB hoje, `docker-compose.prod.yml`, `deploy.resources.limits.memory` +
  `memswap_limit`). Crescimento sustentado aqui derruba a própria Custódia primeiro
  (OOM do container) — mas o runtime .NET por padrão não enxerga esse teto (PADROES
  §10.12), então o painel mostra o consumo visto de DENTRO do processo, não o que o
  cgroup aplicaria por fora.

## Regras de alerta (`cloud/rules-custodia.yaml`)

Duas regras, grupo `custodia-alertas`, pasta `Custodia`:

- **Custódia — App down** (`custodia-app-down`): `up{job="custodia"} == 0`,
  `for: 2m`, `noDataState: Alerting`. Mesma forma de `td-app-down` (repo
  `tesouro-direto-api`, `rules.yaml`) — `up == 0`, não `absent()`, porque o alvo já
  está declarado em `infra/alloy/config.alloy`; o que este alerta vigia é o alvo parar
  de responder. `noDataState: Alerting` porque a série pode sumir por completo se o
  alvo for removido do scrape ou o container renomeado, e isso também precisa soar.
- **Custódia — DB/readiness down** (`custodia-db-readiness-down`):
  `aspnetcore_healthcheck_status{job="custodia",name="AppDbContext"} == 0`, `for: 1m`,
  `noDataState: Alerting`. Mesma forma de `td-db-readiness-down`. A métrica só é
  publicada quando algo chama `/health*` — em produção quem garante isso 24/7 é o
  healthcheck do próprio `docker-compose.prod.yml` (curl em `/health/ready` a cada
  30s), não o scrape do Alloy (que roda a cada 30s também, mas por um caminho
  diferente). `noDataState: Alerting` pelo mesmo motivo da regra acima.

Este grupo chegou a ter mais duas regras — backlog da outbox envelhecido e relay
falhando persistentemente, sobre `custodia_outbox_*`/`custodia_relay_*` — herdadas por
cópia do molde `operacoes`. Foram removidas: a Custódia consome eventos, não os
publica (ADR-10; ver `CLAUDE.md`), não tem outbox nem relay, e aquelas séries nunca
teriam produtor aqui. O substituto real de observabilidade de consumo — profundidade e
idade da fila `custodia.prices`, DLQ e parking — está agendado no F2/F4 do
`docs/ROADMAP.md`; a regra correspondente entra no mesmo diff que a métrica, junto com
o painel equivalente do dashboard.

Sem `contactpoints.yaml` nem `policies.yaml` neste repo, pelo mesmo motivo do Hub e do
Operações: quem define o roteamento do Telegram é o repo de referência. Lá existe um
quarto contact point para o MESMO bot e MESMO chat id — `telegram-custodia` —
diferindo só no `message`, que prefixa a origem (🟢 TESOURO DIRETO / 🔵 HUB DE PRECOS /
🟠 OPERACOES / 🟣 CUSTODIA — o emoji da Custódia não colide com os três já em uso). O
`policies.yaml` de lá tem uma rota FILHA casando `service = custodia` →
`telegram-custodia`; a raiz e as rotas do Hub e do Operações continuam byte a byte
iguais a antes.

**O label `service: custodia` das duas regras acima virou contrato** — é ele que a
rota filha casa no repo de referência. Quem remover ou renomear esse label aqui quebra
o roteamento do lado de lá, sem erro visível na hora — o YAML continua válido, o
`apply-cloud.sh` continua aplicando com sucesso, só o Telegram passa a rotular errado.
O modo de falha, como no Hub e no Operações, **não é silêncio**: o roteamento do
Alertmanager cai para a rota raiz quando nenhuma rota filha casa, então o alerta ainda
chega — só pelo `telegram-tesouro`, com o prefixo errado. Vale saber disso antes de sair
caçando alerta sumido.

## Procedimento de publicação (resumo)

1. Edite `dashboards/custodia.json` e/ou `cloud/rules-custodia.yaml` aqui.
2. Copie os dois para `../tesouro-direto-api/infra/grafana/{dashboards,cloud}/` (ver
   comandos acima).
3. Do `tesouro-direto-api`, exporte `GC_GRAFANA_URL`, `GC_GRAFANA_TOKEN` e
   `TELEGRAM_BOT_TOKEN` e rode `./scripts/grafana-cloud/apply-cloud.sh`. **Confira o
   VALOR do `TELEGRAM_BOT_TOKEN`, não só que ele não está vazio:** a guarda `${VAR:?}`
   do script só testa vazio, e um placeholder (`not-configured-local-dev` e parentes)
   passa por ela, é gravado no contact point e deixa o Telegram mudo **para todos os
   serviços publicados** — com o script reportando sucesso. Aconteceu no fecho do F1 do
   `operacoes` (`LEIA-ME-KIT.md`, "Publicar alerta na nuvem").
4. Confira a saída: o script conta as regras por pasta e reconsulta cada dashboard para
   garantir que os datasources resolveram — falha alta (`ABORTADO`) se algo não bateu.
5. Rode `./scripts/verificar-f1.sh` neste repo para conferir a fiação (alvo do scrape,
   dashboard e regras citados no repo vizinho) — não substitui o passo 4, cobre o "os
   arquivos existem e estão referenciados", não o "a publicação na nuvem funcionou".
