# Comece aqui — custodia

Repo preparado por `scripts/novo-repo.sh` do `hub-precos` em 2026-09-07.
O kit, a infra e o CI já vieram **com as guardas da seção 10 do PADROES**, que o repo
de referência não tem. Falta o que um script não pode fazer.

## 1. Leia antes de despachar o primeiro executor

- `PADROES.md` seção 10 — cada item nasceu de um incidente real.
- `LEIA-ME-KIT.md`, seção **"Critério de pronto do F1"** — o critério de pronto desta
  primeira fase. Ele **não** termina quando compila.
- `LEIA-ME-KIT.md`, seção **"Erros de orquestração no `hub`"** — se você vai conduzir
  os agents, é ali que você vai errar.

## 2. Ainda por fazer neste repo

- [x] `git init` e commits; criar o repo no GitHub. Confirmado: `git log --oneline`
      mostra 8 commits (`71382b2` "chore: esqueleto de scaffolding, portado e adaptado
      do operacoes" até `cb05648`); `gh repo view renatojsilvas/custodia` resolve o
      repo remoto; `git remote -v` mostra `origin
      git@github.com:renatojsilvas/custodia.git`.
- [x] Clonar o molde como irmão: `../operacoes`, `../hub-precos`,
      `../tesouro-direto-api` e `../plataforma-docs` existem os quatro em disco, ao
      lado deste repo (confirmado com `ls ..`).
- [x] **Portar o CÓDIGO do molde** (do `operacoes`, não deste repo): os 4 projetos
      existem em `src/` (`Custodia.API/Application/Domain/Infrastructure`), com
      `Result`/`IResult` (`Domain/Common`), `ResultExtensions` e `SerilogExtensions`
      (`API/Extensions`), `ApiKeyMiddleware` e `CorrelationIdMiddleware`
      (`API/Middleware`). Confirmado por
      `find src -type f -name '*.cs' -not -path '*/obj/*'`.
      **Nota:** `MapReadGet`/`ReadEndpointExtensions` ainda **não** existe — e é
      diferente do caso do `operacoes`: aqui não é "ainda não", é **nunca antes de F5**
      *e* a Custódia não tem endpoint de escrita nesta plataforma nem depois (ADR-10) —
      só leitura (extrato) nasce em fase futura. Portar o helper sem um GET que o use
      seria molde sem consumidor.
- [x] Copiar `tests/*.Architecture.Tests/` do molde — `tests/
      Custodia.Architecture.Tests/` existe (confirmado com `ls tests`).
- [x] `Custodia.sln` e os csproj. Confirmado: `Custodia.sln` na raiz, 9 `.csproj` no
      total (4 em `src/`, 5 em `tests/`: `Custodia.{Infrastructure,Application,API,
      Domain}.Tests` mais `Custodia.Architecture.Tests`).
- [x] Escrever o `README.md`. Conteúdo é específico da Custódia: o que o serviço é (só
      consome evento, sem endpoint de escrita — ADR-10), `docker compose up -d`, os
      dois segredos obrigatórios, a ressalva de mensageria (não publica contrato da
      §5.1, mas emite infra própria: `custodia.retry`/`custodia.parked`/
      `custodia.prices.dlq`), e o gate de cobertura real (`scripts/coverage-gate.py`,
      85%, lido do `.github/workflows/ci.yml` — não existe gate dentro de nenhum
      `.csproj`). **Rodei os dois comandos de subida literalmente como estão escritos
      no README** (Docker e `dotnet run`) antes de considerar esta entrega pronta — ver
      relatório da tarefa.
- [x] Revisar `docs/ROADMAP.md`: já não é o template do hub — tem a fila F1–F9 derivada
      do `ARQUITETURA.md` §7/§9, com prompt próprio por fase (confirmado por leitura;
      arquivo não tocado nesta tarefa).
- [x] Conferir `.env.example` e os composes: os nomes foram substituídos (serviço
      chamado `custodia`, nunca `app` — `PADROES.md` §10.1), as portas (5082 app, 5435
      db) evitam colisão com as do hub (5080/5433) e do operações (5081/5434), e os
      limites de recurso vêm de medição na VPS em 2026-09-07: `nproc` = **1**, memória
      total **1967 MB**, disponível **859 MB** (menos do que os 1035 MB medidos na
      rodada do `operacoes`, dois dias antes — o orçamento apertou, não folgou).

## 3. Fora deste repo

- [x] Secrets no GitHub: os cinco — `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`,
      `CUSTODIA_APP_PASSWORD` e `CUSTODIA_API_KEY` — confirmados nesta fase com
      `gh secret list --repo renatojsilvas/custodia` (todos com timestamp de
      2026-09-07T23:40).
- [x] **No repo do `tesouro-direto`, para métrica (que é *pull* e mora lá):
      commitado em `14f0406`.** Seis arquivos: `infra/alloy/config.alloy` (alvo
      `job="custodia"`), `scripts/grafana-cloud/apply-cloud.sh` (os **cinco pontos**
      próprios: `FOLDER_UID_CUSTODIA`, bloco `if -f` das regras, bloco `if -f` do
      dashboard, conferência da contagem, `uids_dashboards_verificar`),
      `infra/grafana/cloud/contactpoints.yaml` (`telegram-custodia`), `policies.yaml`
      (rota filha `service = custodia`), mais as cópias de
      `infra/grafana/dashboards/custodia.json` e `infra/grafana/cloud/rules-custodia.yaml`.
      **O que faz este item ser `[x]` e não "existe em disco" é o commit**: os arquivos
      de alerta do `hub` e do `operacoes` já ficaram lá como não rastreados uma vez, e
      sumiriam no primeiro clone limpo, com o publicador voltando a pular o bloco em
      silêncio (`LEIA-ME-KIT.md`, "Escrever no repo certo e esquecer de rastrear lá").
      Confira com `git -C ../tesouro-direto-api ls-files -- infra/grafana/dashboards/custodia.json
      infra/grafana/cloud/rules-custodia.yaml`, que tem que devolver **as duas linhas** —
      **não** com `git status`, que devolve a mesma coisa (nada) para "copiado e
      commitado" e para "nunca criado".
      *(Este item esteve `[ ]` por 22 segundos: quando o texto foi escrito era verdade,
      e o commit do repo vizinho veio logo depois. Registro a inversão porque ela é a
      forma barata de a §10.20 acontecer — texto que descreve um estado que já mudou.)*
- [ ] Rodar o `apply-cloud.sh` com `GC_GRAFANA_URL`, `GC_GRAFANA_TOKEN` e
      `TELEGRAM_BOT_TOKEN` exportados na invocação, **conferindo o VALOR do token do
      Telegram e não só que ele não está vazio** — a guarda `${VAR:?}` só testa vazio, e
      um placeholder passa por ela e cala o Telegram de **todos** os serviços
      publicados, com o script reportando sucesso. Depois, **provar pela API do Grafana
      Cloud** que a regra e o dashboard estão na pasta `Custodia` — editar arquivo não é
      publicar, e é essa distinção que deixou os alertas do `hub` semanas sem existir.
- [ ] **Orçamento de memória da VPS.** Medido em 2026-09-07 por SSH, antes de escolher
      qualquer número: `nproc` = 1, 1967 MB totais, 859 MB disponíveis. O teto do
      `custodia` (192m + `cpu_shares: 512`, `docker-compose.prod.yml`) vem daí e do
      vizinho de perfil mais parecido, não de "parece folgado". Falta **revisar o teto
      dos VIZINHOS** à luz do serviço novo (§10.14) — em particular o
      `hub-precos-app`, que roda **sem limite nenhum** e cujo GC enxerga os 1,9 GB do
      host em vez de um cgroup (§10.12 aberta hoje).
- [x] Semear a memória do projeto: as 12 ADRs já estão no grafo MCP — **dado como
      conferido no despacho desta tarefa; eu não tenho acesso à ferramenta MCP
      `memoria` neste subagente para reconferir por conta própria.**

## 4. Critério de pronto do F1 (cinco provas)

Nenhuma das cinco foi verificada nesta tarefa — o escopo daqui foi só `README.md` e
`COMECE-AQUI.md`. Registradas para quem for fechar o F1:

1. **Um merge na `main` deploya sozinho** — deploy na unha por SSH não conta.
2. `curl` no `/health/ready` **pela VPS** (não confundir com o `curl` local que esta
   tarefa rodou contra `127.0.0.1`/`localhost` — aquele prova o README, não a VPS).
3. A série do `job=custodia` visível no Grafana Cloud — a fiação já está commitada no
   `tesouro-direto-api` (`14f0406`), mas **arquivo commitado não é série na nuvem**:
   falta o deploy (para haver o que raspar) e o `apply-cloud.sh`.
4. O dashboard `custodia.json` aparecendo lá, **com dados** — mesma dependência, e
   "com dados" é o que separa publicado de publicado-e-vazio.
5. **Um alerta seu disparando de propósito e chegando no Telegram** — a única que prova
   a corrente inteira: regra publicada, avaliando, contact point com token bom e
   roteamento funcionando. Não há script que substitua; é a mão.

E depois de todo merge, confira o run de **push**, não o do PR: **verde no CI não é
deployado** (`PADROES.md` §10.17). No hub um PR ficou 12 dias mergeado e fora do ar
porque um teste instável pulou o job de deploy.
