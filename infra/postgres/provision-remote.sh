#!/usr/bin/env bash
# Provisiona o database e a role do Custodia num Postgres COMPARTILHADO já existente
# (o caminho da VPS), em vez do container próprio do docker-compose.yml local.
#
# POR QUE ESTE SCRIPT EXISTE, e por que `custodia-role.sql` não basta sozinho:
# o `custodia-role.sql` provisiona a ROLE e assume que o database já existe — localmente
# quem o cria é `POSTGRES_DB: custodia` no docker-compose.yml, executado pelo entrypoint
# da imagem antes dos hooks de initdb. Num cluster que já está de pé e é
# compartilhado (§12 do plano do molde: múltiplos serviços num mesmo Postgres),
# não há entrypoint nenhum para rodar — o database precisa ser criado por fora, e é
# o que o passo 1 abaixo faz.
#
# `CREATE DATABASE` não roda dentro de transação nem de bloco DO, então não dá para
# embutir no `custodia-role.sql` (que é atômico de propósito, com BEGIN/COMMIT). Daí a
# separação em dois passos, com `\gexec` no primeiro — a forma canônica de executar
# DDL gerado condicionalmente no psql.
#
# IDEMPOTENTE: seguro reexecutar. O passo 1 não recria um database existente; o
# passo 2 converge a role (create-ou-ALTER) e reaplica ownership/REVOKE/GRANT.
#
# Uso (na VPS, a partir da raiz do repo):
#   CUSTODIA_APP_PASSWORD='...' ./infra/postgres/provision-remote.sh
#
# Variáveis:
#   CUSTODIA_APP_PASSWORD  (obrigatória) senha da role `custodia`
#   PG_CONTAINER      (default: tesouro-direto-db) container do Postgres compartilhado
#   PG_ADMIN_USER     (default: postgres) role admin de bootstrap do cluster
#   CUSTODIA_DB_NAME       (default: custodia) nome do database do Custodia
set -euo pipefail

: "${CUSTODIA_APP_PASSWORD:?CUSTODIA_APP_PASSWORD é obrigatória (senha da role custodia)}"

# NORMALIZAÇÃO — não é frescura, é a causa raiz de um deploy quebrado (2026-08-20 no molde).
# O segredo chegava ao provisionamento por `docker exec -e`, que preserva o valor
# byte a byte, e à aplicação pelo `.env` do compose, que é lido POR LINHA e portanto
# descarta um `\n`/`\r` final. Bastava o segredo ter sido colado com quebra de linha
# para a role nascer com um byte a mais do que a aplicação envia: `28P01 password
# authentication failed`, com os dois lados "parecendo" iguais em qualquer comparação
# feita a partir do `.env`. Normalizar aqui faz os dois caminhos convergirem na origem.
CUSTODIA_APP_PASSWORD="$(printf '%s' "$CUSTODIA_APP_PASSWORD" | tr -d '\r\n')"
[ -n "$CUSTODIA_APP_PASSWORD" ] || { echo "ERRO: CUSTODIA_APP_PASSWORD ficou vazia após remover quebras de linha." >&2; exit 1; }

PG_CONTAINER="${PG_CONTAINER:-tesouro-direto-db}"
PG_ADMIN_USER="${PG_ADMIN_USER:-postgres}"
CUSTODIA_DB_NAME="${CUSTODIA_DB_NAME:-custodia}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROLE_SQL="$SCRIPT_DIR/sql/custodia-role.sql"

[ -f "$ROLE_SQL" ] || { echo "ERRO: não achei $ROLE_SQL" >&2; exit 1; }

if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
  echo "ERRO: container '$PG_CONTAINER' não está rodando. Containers ativos:" >&2
  docker ps --format '  {{.Names}}' >&2
  exit 1
fi

echo "==> 1/2 database '$CUSTODIA_DB_NAME' (cria se não existir)"
# A senha NÃO passa por aqui: este passo é só DDL de database, e mandar a senha
# junto ampliaria sem motivo a superfície onde ela aparece.
docker exec -i "$PG_CONTAINER" \
  psql -v ON_ERROR_STOP=1 -U "$PG_ADMIN_USER" -d postgres -v db="$CUSTODIA_DB_NAME" <<'SQL'
SELECT format('CREATE DATABASE %I', :'db')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db');
\gexec
SQL

echo "==> 2/2 role 'custodia' e permissões (custodia-role.sql, idempotente)"
# `-e` põe as variáveis no ambiente do processo psql DENTRO do container, que é de
# onde o `\getenv` do custodia-role.sql as lê. Nunca via argv (`-v`), para a senha não
# aparecer em `ps` nem no histórico de shell.
docker exec -i \
  -e CUSTODIA_APP_PASSWORD="$CUSTODIA_APP_PASSWORD" \
  -e CUSTODIA_DB_NAME="$CUSTODIA_DB_NAME" \
  "$PG_CONTAINER" \
  psql -v ON_ERROR_STOP=1 -U "$PG_ADMIN_USER" -d "$CUSTODIA_DB_NAME" -f - < "$ROLE_SQL"

echo "==> 3/3 verificando que a credencial provisionada REALMENTE autentica"
# Por que este passo existe: provisionar sem verificar é afirmar sem evidência.
# O teste tem que passar PELA REDE, e não por `docker exec` no próprio Postgres: o
# `pg_hba.conf` da imagem oficial tem `host all all 127.0.0.1/32 trust`, então um
# teste por loopback autentica QUALQUER senha e daria um falso positivo — exatamente
# o tipo de verificação que parece proteger e não protege.
PG_NETWORK="$(docker inspect -f '{{range $k, $v := .NetworkSettings.Networks}}{{$k}} {{end}}' "$PG_CONTAINER" | awk '{print $1}')"
PG_IMAGE="$(docker inspect -f '{{.Config.Image}}' "$PG_CONTAINER")"

[ -n "$PG_NETWORK" ] || { echo "ERRO: não descobri a rede docker de '$PG_CONTAINER'." >&2; exit 1; }

if docker run --rm --network "$PG_NETWORK" \
     -e PGPASSWORD="$CUSTODIA_APP_PASSWORD" \
     "$PG_IMAGE" \
     psql -h "$PG_CONTAINER" -U custodia -d "$CUSTODIA_DB_NAME" -tAc 'select 1' >/dev/null 2>&1; then
  echo "    credencial confere (autenticou como 'custodia' em '$CUSTODIA_DB_NAME' via rede '$PG_NETWORK')"
else
  echo "ERRO: a role 'custodia' foi provisionada mas NÃO autentica com a senha fornecida." >&2
  echo "      Rede testada: '$PG_NETWORK'. Isso normalmente significa que o valor de" >&2
  echo "      CUSTODIA_APP_PASSWORD difere entre quem provisiona e quem conecta." >&2
  exit 1
fi

echo "==> pronto: database '$CUSTODIA_DB_NAME' e role 'custodia' provisionados e verificados em '$PG_CONTAINER'"
