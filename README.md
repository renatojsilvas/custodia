# Custódia

Motor de escrituração multi-cliente da plataforma. A Custódia **não expõe nenhum
endpoint de escrita de negócio** (ADR-10) — ela consome exclusivamente eventos:
`trades.registered` de Operações, `prices.*` e `corpactions.*` do Hub. O livro
`movimentos` é append-only e é a verdade; `posicao_corrente` e `snapshots_posicao` são
projeções **descartáveis** dele — divergiu, o livro vence e a projeção se reconstrói.
`cliente_id` só existe aqui; `instrumento_id` é o id do Hub gravado **cru**, sem FK, sem
de-para (ADR-4). O banco é privado desta casa: a Custódia nunca lê o schema do Hub nem
o de Operações (ADR-12). Ver
[`../plataforma-docs/ARQUITETURA.md`](../plataforma-docs/ARQUITETURA.md) (§7, §9 itens
3–5) e a seção "O que a Custódia é, e o que ela NÃO é" em [`CLAUDE.md`](CLAUDE.md) para
o desenho completo e os invariantes que valem contra qualquer pedido de escrita.

> Solução .NET 8 em Clean Architecture (`Custodia.Domain`, `Custodia.Application`,
> `Custodia.Infrastructure`, `Custodia.API`), seguindo os padrões catalogados em
> [`PADROES.md`](PADROES.md) — herdados do repo `tesouro-direto-api` e compartilhados
> com `hub-precos` e `operacoes`.

## Estado atual

A API sobe, aplica migration no boot (a `InitialCreate` está **vazia** — nenhuma tabela
nesta fase; ela só prova que a role `custodia` consegue escrever no banco) e responde
health/metrics/swagger. **Não existe funcionalidade de negócio implantada ainda**, nem
consumidor de eventos. Este é um esqueleto de infraestrutura — a caneta está na fila
(`docs/ROADMAP.md`). Diferente de Operações e do Hub, `/v1/` **não vai ganhar** rota de
escrita nesta plataforma — é definitivo, não "ainda não chegou" — e rota de leitura
(extrato) só nasce em fase futura.

## Rodar com Docker (caminho padrão)

Sobe banco e API conectados entre si, sem precisar de SDK .NET local. O serviço
`custodia` entra desde já na rede compartilhada `plataforma` (`external: true` no
`docker-compose.yml`, mesmo sem broker configurado nesta fase — ver "Mensageria"
abaixo) — se ela ainda não existir no seu Docker, crie uma vez antes do primeiro `up`:

```bash
docker network create plataforma 2>/dev/null || true   # uma vez; ignora se já existe
cp .env.example .env   # preencha CUSTODIA_APP_PASSWORD e CUSTODIA_API_KEY
docker compose up -d
until [ "$(curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:5082/health/ready)" = "200" ]; do sleep 2; done && echo OK
```

O laço não é frescura: `docker compose up -d` **retorna quando o container arranca, não
quando ele fica pronto** (`PADROES.md` §10.15). Numa subida fria, um `curl` imediato
falha com "empty reply from server" — medido aqui: a API responde `200` cerca de dois
segundos depois do `Started`. E é `-sS`, não `-s`: sozinho, o `-s` suprime a mensagem de
erro do curl e transforma "conexão recusada" em saída vazia, indistinguível de "respondeu
200 com corpo vazio" — dois diagnósticos opostos com a mesma cara (`LEIA-ME-KIT.md`,
"`curl -s` transforma falha de conexão em saída vazia"). O laço espera pela **própria
condição que você vai conferir**, que é o que a §10.15 pede, em vez de esperar pelo
healthcheck (um proxy da condição) ou por nada.

O compose falha o boot se `CUSTODIA_APP_PASSWORD` ou `CUSTODIA_API_KEY` estiverem
vazias (`${VAR:?}`). São dois segredos com papéis diferentes:

- `CUSTODIA_APP_PASSWORD` é a senha da role de aplicação `custodia`: o serviço `db` a
  usa para provisionar a role (`infra/postgres/initdb/01-provision-custodia.sh`, ver
  [`infra/postgres/README.md`](infra/postgres/README.md)), e o serviço `custodia` a usa
  para montar `ConnectionStrings__DefaultConnection` (`Host=db;Port=5432`, a rede
  interna do compose) — o **mesmo** valor nos dois lados, senão a autenticação no
  Postgres falha.
- `CUSTODIA_API_KEY` é a chave que a Custódia **exige** de quem a chama: todo request a
  `/v1/*` precisa do header `X-Api-Key` com esse valor, ou recebe `401`
  (`src/Custodia.API/Middleware/ApiKeyMiddleware.cs`). Ela chega ao container como
  `ApiKey__Key`. Precisa ter no mínimo 32 caracteres e não pode conter um placeholder
  conhecido — `ApiKeyGuard`/`KeyStrengthGuard` recusam o boot fora de
  `Development`/`Testing` nos dois casos; gere uma com `openssl rand -hex 32`.

Portas locais (ver `docker-compose.yml`):

- `custodia` publicado em `http://127.0.0.1:5082` — Swagger em
  `http://127.0.0.1:5082/swagger`.
- `db` publicado em `127.0.0.1:5435` — serve para `psql` e para o `dotnet run` local
  (abaixo), não para o `custodia` do compose (que fala com `db` pela rede interna,
  porta 5432).

Essas portas são as seguintes às do `hub-precos` (5080/5433) e do `operacoes`
(5081/5434) — cada repo escolhe as suas para não colidir ao rodar os três ao mesmo
tempo; não há registro central de portas locais.

Esse caminho não usa `dotnet user-secrets` em nenhum momento; as credenciais chegam só
por variável de ambiente.

## Rodar local para desenvolver (`dotnet run`)

Ciclo rápido de edição/depuração, sem rebuildar imagem a cada mudança. Precisa do
.NET 8 SDK instalado.

```bash
# 1. Só o banco, via compose
cp .env.example .env   # preencha CUSTODIA_APP_PASSWORD, se ainda não fez
docker compose up -d db

# 2. Credencial da API via user-secrets (mesma senha do passo 1)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5435;Database=custodia;Username=custodia;Password=<mesma-senha-do-.env>" \
  --project src/Custodia.API

# 3. Rodar
dotnet run --project src/Custodia.API
curl -sSf http://localhost:5181/health/ready && echo OK
```

Swagger em `http://localhost:5181/swagger`.

`src/Custodia.API/appsettings.json` só guarda host/porta/database — nunca a credencial
(`PADROES.md` §6) — por isso o passo 2 é obrigatório: sem ele o boot falha rápido com
uma mensagem indicando exatamente esse comando (`ConnectionStringGuard`, ver
`src/Custodia.API/Extensions/ConnectionStringGuard.cs`), em vez de um erro de
autenticação confuso do Npgsql. **A armadilha**: `dotnet user-secrets` só é lido quando
`ASPNETCORE_ENVIRONMENT=Development`, e é
`src/Custodia.API/Properties/launchSettings.json` quem define isso para o `dotnet run`
— sem esse arquivo (ou rodando a API de outro jeito, sem essa variável), o secret
configurado no passo 2 é ignorado em silêncio e o boot falha por falta de credencial
mesmo com o secret salvo.

**`ApiKey:Key` não precisa de user-secrets em `Development`:** `ApiKeyGuard` (ver
`src/Custodia.API/Extensions/ApiKeyGuard.cs`) recusa o boot fora de
`Development`/`Testing` por chave vazia, por conter um placeholder conhecido ou por ter
menos de 32 caracteres — nenhuma dessas três checagens roda em `Development`/`Testing`
— em `dotnet run` local a API sobe mesmo sem configurar nada, mas todo request a
`/v1/*` continua exigindo o header `X-Api-Key` (`ApiKeyMiddleware` roda em todo
ambiente, guarda de boot ou não). Como `appsettings.json` commita a chave vazia,
qualquer requisição autenticada localmente falha até você configurar uma — mais
simples via user-secrets:

```bash
dotnet user-secrets set "ApiKey:Key" "uma-chave-qualquer-para-dev" --project src/Custodia.API
curl -sSf -H "X-Api-Key: uma-chave-qualquer-para-dev" http://localhost:5181/health/ready
```

`dotnet run` sobe em `http://localhost:5181` — porta fixa conforme
`src/Custodia.API/Properties/launchSettings.json`.

## Quando usar cada caminho

- **Docker (`docker compose up -d`)** — mais perto de produção (mesma imagem, mesma
  forma de receber credencial por env var); não precisa de SDK .NET instalado; toda
  mudança de código exige rebuild da imagem (`docker compose up -d --build`).
- **`dotnet run`** — ciclo rápido de edição/depuração local; exige SDK .NET e o passo
  de `user-secrets`; só o `db` roda em container.

## Estrutura da solução

| Projeto | Papel |
|---------|-------|
| `Custodia.Domain` | Entidades, Value Objects e erros de domínio (`Result`/`Error`). Zero dependências externas. |
| `Custodia.Application` | Casos de uso via MediatR (commands/queries) e interfaces de porta; `LoggingBehavior` no pipeline. |
| `Custodia.Infrastructure` | EF Core (escrita/migrations) e, no futuro, Dapper (leitura), consumidor de eventos e clients externos. |
| `Custodia.API` | Minimal API — endpoints finos, middleware (correlation id, API key), Swagger, health checks, métricas. |

## Padrões

Este repo segue os padrões catalogados em [`PADROES.md`](PADROES.md), herdados do repo
de referência [`../tesouro-direto-api`](../tesouro-direto-api) e corrigidos por
incidente ao longo de `hub-precos` e `operacoes` — antes de criar qualquer estrutura
nova (endpoint, repositório, job, client), localize o equivalente lá e siga o molde.
Quando os moldes divergirem entre si, prefira `../operacoes`: é o mais recente e o
único que já carrega os padrões corrigidos (`MapReadGet` com leitura condicional, cache
com teto, coleta paginada que distingue completude de limite).

## Autenticação

Todo request sob `/v1/*` exige o header `X-Api-Key` com o valor configurado em
`ApiKey:Key` (`ApiKey__Key` por variável de ambiente); sem ele, ou com valor errado, a
resposta é `401` em `application/problem+json`, indistinguível entre "sem chave" e
"chave errada" — não dá para descobrir por tentativa se uma chave existe.
`/health`, `/health/ready`, `/health/live`, `/metrics` e `/swagger` são isentos
(`ApiKey:ExcludedPaths`). A comparação é em tempo constante
(`CryptographicOperations.FixedTimeEquals` sobre SHA-256), para não vazar por
temporização se uma chave começa certa. Ver
`src/Custodia.API/Middleware/ApiKeyMiddleware.cs`.

Fora de `Development`/`Testing`, o boot falha se `ApiKey:Key` estiver vazia, contiver
um placeholder conhecido (`CHANGE-ME-IN-PRODUCTION`, `dev-local-key`,
`uma-chave-qualquer-para-dev`, comparado ignorando separador e maiúsculas/minúsculas) ou
tiver menos de 32 caracteres (`src/Custodia.API/Extensions/ApiKeyGuard.cs`) — uma chave
esquecida, curta demais ou deixada no placeholder de exemplo nunca vira "sem
autenticação" em silêncio. Gere uma chave forte com `openssl rand -hex 32`.

**Como hoje não existe nenhuma rota sob `/v1/*`**, esta camada está ativa e testada,
mas ainda sem nenhum recurso de negócio para proteger — ela nasce pronta para quando o
primeiro consumidor de evento existir.

## Banco de dados

Ver [`infra/postgres/README.md`](infra/postgres/README.md) — provisionamento da role
`custodia`, os dois caminhos de execução do SQL e a armadilha do volume já
inicializado. `/health/ready` hoje é só `AddDbContextCheck<AppDbContext>()`
(`CanConnectAsync()` puro): ele prova que **o banco responde**, não que o schema está
certo (`PADROES.md` §10.18) — como a `InitialCreate` não cria tabela nenhuma, não há
hoje schema de negócio para esse readiness confirmar.

## Testes

Execute os testes com:

```bash
dotnet test
```

O CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) roda a suíte com
cobertura (`--collect:"XPlat Code Coverage"`, formato OpenCover) e aplica um gate de
linha mínima de **85%** via [`scripts/coverage-gate.py`](scripts/coverage-gate.py)
sobre os quatro projetos de `src/` (API, Application, Domain, Infrastructure,
excluindo `Migrations/`, `bin/` e `obj/`) — não há gate configurado dentro de nenhum
`.csproj`.

## Mensageria

**A Custódia não publica nenhum evento de contrato da §5.1** (`trades.registered`,
`prices.*`, `corpactions.*` são publicados por Operações e pelo Hub, nunca por aqui) e
não tem outbox nem relay. Isso não quer dizer "não publica, ponto": o único tráfego
AMQP que ela emite é infraestrutura interna dela mesma — as filas `custodia.retry`,
`custodia.parked` e `custodia.prices.dlq`, previstas para o F2/F4 de
`docs/ROADMAP.md`, junto com o consumidor de eventos que ainda não existe nesta fase.

O broker `plataforma-rabbitmq` é compartilhado com outros serviços, não sobe neste
`docker-compose.yml`. Para rodar localmente com o `dotnet run` e ter o broker
disponível, quando o consumidor existir:

```bash
# Uma vez, criar a rede compartilhada da plataforma
docker network create plataforma

# Depois, subir o broker do repositório hub-precos
docker compose -f ../hub-precos/docker-compose.yml up -d plataforma-rabbitmq
```

## Referências

- [`PADROES.md`](PADROES.md) — padrões de código herdados de `tesouro-direto-api`
- [`LEIA-ME-KIT.md`](LEIA-ME-KIT.md) — critério de pronto das fases e armadilhas de infraestrutura
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — fila de tarefas deste serviço
- [`../plataforma-docs/ARQUITETURA.md`](../plataforma-docs/ARQUITETURA.md) — desenho completo da plataforma
- [`../operacoes`](../operacoes) — molde de porte mais recente, o serviço mais parecido com este
- [`../hub-precos`](../hub-precos) — molde original, referência para ingestão, jobs e adapters
