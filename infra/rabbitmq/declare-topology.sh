#!/usr/bin/env bash
# Declara, de forma IDEMPOTENTE, a topologia do lado CONSUMIDOR no broker
# `plataforma-rabbitmq` (servico do hub-precos, ARQUITETURA §5/§9 item 3), e verifica
# o resultado de forma BLOQUEANTE antes do deploy seguir. Roda dentro do script de
# deploy (.github/workflows/ci.yml, job `deploy`), depois do `up -d` do custodia-app.
#
# NAO HA MOLDE PARA ISTO: nem o operacoes nem o hub-precos declaram topologia por
# script (eles so publicam e deixam quem consome declarar a propria fila). O molde
# `infra/postgres/provision-remote.sh` deste repo inspirou a FORMA (script versionado,
# idempotente, com verificacao bloqueante ao final) — nao o CONTEUDO, que e novo.
#
# DECISOES DESTA FASE (ROADMAP.md F2), resumidas aqui para quem le so o script:
#   - exchange `prices` (topico, do hub-precos) NUNCA e redeclarado se ja existir
#     divergente: redeclarar quebraria o publish do hub-precos/operacoes no proximo
#     boot deles. A declaracao e so quando ele esta AUSENTE (ex.: volume do broker
#     perdido — LEIA-ME-KIT, "Perder o volume do broker apaga fila e binding").
#   - `custodia.prices` (quorum) tem x-delivery-limit=20 (medido no rabbitmq:4.3.5 —
#     o DEFAULT tambem e 20, mas o valor fica EXPLICITO para nao depender do default
#     do broker mudar) e x-dead-letter-exchange=custodia.dlx. SEM x-max-length e SEM
#     x-message-ttl: um teto com reject-publish envenenaria o relay do hub-precos e do
#     operacoes (servicos de TERCEIROS), e TTL descartaria em silencio — exatamente o
#     defeito que esta fase existe para impedir.
#   - `custodia.prices.dlq` e `custodia.parked` sao FIM DE LINHA (nao ha para onde
#     dead-letrar depois delas): x-delivery-limit=-1 (ilimitado), para nao descartar
#     em silencio uma mensagem redistribuida pelo futuro consumidor do F4. SEGUNDA
#     RAZAO para o -1, que so aparece ao ler este script: a verificacao abaixo ESPIA
#     a cabeca dessas duas filas em toda execucao (safe_take_probe), e sem consumidor
#     de terceiros ali, o -1 garante que essa espiada NUNCA dead-letra por exaustao
#     de limite uma mensagem real que por ventura ali estivesse.
#   - `custodia.retry` nasce CLASSIC durable (nao quorum), com x-message-ttl=30000 e
#     x-dead-letter-exchange=custodia.retry.dlx, SEM CONSUMIDOR. A razao e ESTRATEGIA
#     DE DLX, nunca "quorum nao suporta TTL" (que e falso: TTL por fila e dead-letter
#     por expiracao funcionam em quorum queue). O default de dead-lettering de quorum
#     e `at-most-once`, que pode DESCARTAR a mensagem durante o dead-letter — e esta
#     fila e 100% trafego de dead-letter (tudo que entra sai por expiracao). A saida
#     quorum-segura (at-least-once + overflow reject-publish) nao e conferivel pela
#     management API do 4.3.5 (ela aceita HTTP 201 com e sem o reject-publish que essa
#     estrategia exige — MEDIDO pelo orquestrador em 2026-09-08 contra o broker
#     rabbitmq:4-management-alpine 4.3.5 real de producao, os dois PUTs devolveram
#     201 — ler os argumentos de volta prova o que PEDIMOS, nao o que o broker FAZ),
#     e obrigaria o F4 a nascer com o ramo persistente da Decisao A sob
#     pena de laco quente. Classic aqui e fila de ATRASO, sem consumidor e sem dado
#     proprio — o unico lugar deste desenho onde classic e aceitavel.
#   - Os QUATRO exchanges de infraestrutura (custodia.dlx, custodia.parking,
#     custodia.retry.in, custodia.retry.dlx) sao NOSSOS e FANOUT, nunca topic com
#     binding `#`: mensagem dead-letrada CONSERVA a routing key original
#     (trades.registered, corpactions.td, ...), e um binding que nao case tudo
#     descartaria a mensagem morta em silencio DENTRO da nossa propria infraestrutura
#     — o defeito que esta fase inteira existe para impedir, reproduzido do lado de
#     ca. Fanout ignora a routing key por construcao e nao tem como nao casar.
#
# DESFECHOS ASSIMETRICOS (LEIA-ME-KIT, "Reprovar o proprio deploy por causa de um
# servico que nao e seu"; PADROES §10.15):
#   'docker run' nao executou o container (nossa   -> EXIT_FERRAMENTA (reprova — NAO
#   ferramenta: imagem, rede docker local, daemon)     e diagnostico de broker nenhum;
#                                                       ver EXIT_TOPOLOGIA_AUSENTE
#                                                       abaixo, que e outro caso)
#   401 na management API                          -> EXIT_AUTH_401 (reprova, rapido)
#   broker inacessivel APOS o laco de espera        -> EXIT_BROKER_INACESSIVEL (so o
#                                                       CHAMADOR deste script decide se
#                                                       vira ::warning::+segue ou
#                                                       reprova — ver comentario no
#                                                       ci.yml sobre a regra
#                                                       `custodia-topologia-ausente`)
#   autenticou, mas fila/binding continua ausente   -> EXIT_TOPOLOGIA_AUSENTE (reprova
#                                                       — TAMBEM usado quando a fila/
#                                                       exchange desaparece NO MEIO de
#                                                       uma prova, depois de autenticar,
#                                                       INCLUSIVE quando o BROKER cai no
#                                                       meio — DECISAO DELIBERADA: a
#                                                       assimetria que autoriza o
#                                                       ::warning:: (linha acima) vale SO
#                                                       na janela de autenticacao. Depois
#                                                       do 200 em /api/whoami ja provamos
#                                                       que o broker estava de pe; uma
#                                                       queda DALI EM DIANTE deixa a
#                                                       topologia num estado que NINGUEM
#                                                       verificou, e reprovar e o desfecho
#                                                       certo — nao ha "o broker caiu, mas
#                                                       a declaracao/verificacao anterior
#                                                       continua valendo" para confiar)
#   exchange `prices` presente com props divergentes -> EXIT_EXCHANGE_PRICES_DIVERGENTE
#                                                       (reprova e NAO redeclara)
#   prova de fumaca/fanout/retry falhou              -> EXIT_SMOKE_FALHOU /
#                                                       EXIT_FANOUT_FALHOU /
#                                                       EXIT_RETRY_FALHOU (reprova —
#                                                       sao mecanismos NOSSOS)
#
# TUDO roda de containers cliente na rede docker (nunca por loopback, PADROES §10.3):
# `curlimages/curl` para HTTP, e `ghcr.io/jqlang/jq` para interpretar JSON — nenhum
# dos dois precisa estar instalado no host que roda este script (a VPS), seguindo a
# mesma convencao de todo script deste ecossistema que roda DENTRO da secao SSH do
# deploy (eles evitam depender de pacotes do host — ver o uso de grep/awk em vez de
# jq nos smoke tests de relay do operacoes/hub-precos, que rodam no MESMO lugar).
#
# `curl -sS -w '%{http_code}'` sempre — nunca `curl -s` sozinho, que transforma falha
# de conexao em saida vazia e apaga a diferenca entre "nao conectou" e "conectou e
# recusou" (LEIA-ME-KIT, PADROES §10.15).
set -euo pipefail

: "${RABBITMQ_USER:?RABBITMQ_USER e obrigatorio (usuario do broker plataforma-rabbitmq; usado SO por este script de deploy)}"
: "${RABBITMQ_PASSWORD:?RABBITMQ_PASSWORD e obrigatorio (senha do broker plataforma-rabbitmq; usada SO por este script de deploy)}"

# NORMALIZACAO NA ORIGEM (PADROES §10.4): o segredo chega aqui via GitHub Actions
# `env:`/`envs:` (preserva byte a byte) e pode ter sido colado com quebra de linha no
# cofre de secrets — normalizar aqui, uma vez, evita que a comparacao pareca igual em
# todo log e a autenticacao falhe silenciosamente na rede.
RABBITMQ_USER="$(printf '%s' "$RABBITMQ_USER" | tr -d '\r\n')"
RABBITMQ_PASSWORD="$(printf '%s' "$RABBITMQ_PASSWORD" | tr -d '\r\n')"
[ -n "$RABBITMQ_USER" ] || { echo "ERRO: RABBITMQ_USER ficou vazio apos remover quebras de linha." >&2; exit 1; }
[ -n "$RABBITMQ_PASSWORD" ] || { echo "ERRO: RABBITMQ_PASSWORD ficou vazio apos remover quebras de linha." >&2; exit 1; }

RABBITMQ_MANAGEMENT_HOST="${RABBITMQ_MANAGEMENT_HOST:-plataforma-rabbitmq}"
RABBITMQ_MANAGEMENT_PORT="${RABBITMQ_MANAGEMENT_PORT:-15672}"
CUSTODIA_RABBITMQ_NETWORK="${CUSTODIA_RABBITMQ_NETWORK:-plataforma}"
CUSTODIA_RABBITMQ_CURL_IMAGE="${CUSTODIA_RABBITMQ_CURL_IMAGE:-curlimages/curl:8.11.0}"
CUSTODIA_RABBITMQ_JQ_IMAGE="${CUSTODIA_RABBITMQ_JQ_IMAGE:-ghcr.io/jqlang/jq:1.7.1}"

# Numeros do laco de espera de autenticacao: os mesmos do molde `operacoes`/`hub-precos`
# (36 tentativas x 5s = ~3min) — nao sao chute, sao o numero ja em uso neste
# ecossistema para "broker pode estar subindo" (PADROES §10.15/§10.9).
CUSTODIA_RABBITMQ_AUTH_WAIT_TRIES="${CUSTODIA_RABBITMQ_AUTH_WAIT_TRIES:-36}"
CUSTODIA_RABBITMQ_AUTH_WAIT_SLEEP="${CUSTODIA_RABBITMQ_AUTH_WAIT_SLEEP:-5}"

# x-message-ttl da `custodia.retry`: 30000ms, 6x o Messaging:Relay:IntervaloSegundos=5
# do operacoes (medido, nao de memoria — PADROES §10.9). CUSTODIA_RETRY_TTL_MS so
# existe como variavel para permitir teste local com TTL curto; o ci.yml de PRODUCAO
# nunca a define, entao o default de 30000 e o que sempre vale em deploy real.
CUSTODIA_RETRY_TTL_MS="${CUSTODIA_RETRY_TTL_MS:-30000}"
# Espera pela prova do ciclo de retry: tem que ter folga sobre o TTL (aqui, ate 3x).
CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES="${CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES:-18}"
CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP="${CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP:-5}"

BASE_URL="http://${RABBITMQ_MANAGEMENT_HOST}:${RABBITMQ_MANAGEMENT_PORT}"

EXIT_AUTH_401=10
EXIT_BROKER_INACESSIVEL=11
EXIT_TOPOLOGIA_AUSENTE=12
EXIT_EXCHANGE_PRICES_DIVERGENTE=13
EXIT_SMOKE_FALHOU=14
EXIT_FANOUT_FALHOU=15
EXIT_RETRY_FALHOU=16
# Codigo PROPRIO para "nossa ferramenta (docker) nao executou", distinto de
# EXIT_TOPOLOGIA_AUSENTE (12): 12 diz "autenticou, mas fila/binding ausente" — o
# diagnostico ERRADO para um caso em que nem chegou a autenticar. Mesmo raciocinio
# de dar codigo proprio a cada prova de fumaca/fanout/retry, para nao esconder qual
# delas falhou.
EXIT_FERRAMENTA=17

docker pull -q "$CUSTODIA_RABBITMQ_CURL_IMAGE" >/dev/null 2>&1 || true
docker pull -q "$CUSTODIA_RABBITMQ_JQ_IMAGE" >/dev/null 2>&1 || true

MGMT_STATUS=""
MGMT_BODY=""
MGMT_STDERR=""

# `curl -sS` so tem sentido se o `-S` (que reativa a mensagem de erro sob `-s`)
# chegar a algum lugar — as tres chamadas deste script jogavam o stderr do curl fora
# (`2>/dev/null`), que e exatamente o que o cabecalho do arquivo diz para NAO fazer.
# Aqui: stderr vai para um arquivo temporario (nao da para misturar com o stdout que
# guarda o corpo+codigo sem contaminar o parsing), lido de volta em MGMT_STDERR, e
# ecoado imediatamente quando nao-vazio — e por isso um diagnostico de transporte
# (DNS, timeout, conexao recusada) aparece no log em vez de desaparecer em silencio.
mgmt_request() {
  local method="$1" path="$2" body="${3:-}"
  local combined stderr_file docker_rc
  stderr_file=$(mktemp)
  set +e
  if [ -n "$body" ]; then
    combined=$(docker run --rm --network "$CUSTODIA_RABBITMQ_NETWORK" "$CUSTODIA_RABBITMQ_CURL_IMAGE" \
      -sS -w '\n%{http_code}' --max-time 20 -u "$RABBITMQ_USER:$RABBITMQ_PASSWORD" \
      -X "$method" -H 'content-type: application/json' -d "$body" \
      "${BASE_URL}${path}" 2>"$stderr_file")
  else
    combined=$(docker run --rm --network "$CUSTODIA_RABBITMQ_NETWORK" "$CUSTODIA_RABBITMQ_CURL_IMAGE" \
      -sS -w '\n%{http_code}' --max-time 20 -u "$RABBITMQ_USER:$RABBITMQ_PASSWORD" \
      -X "$method" \
      "${BASE_URL}${path}" 2>"$stderr_file")
  fi
  docker_rc=$?
  set -e
  MGMT_STDERR="$(cat "$stderr_file" 2>/dev/null)"
  rm -f "$stderr_file"
  MGMT_STATUS="${combined##*$'\n'}"
  MGMT_BODY="${combined%$'\n'*}"
  # MESMO discriminador de wait_for_auth (ver o comentario la): se o 'docker run'
  # nao executou o curl (imagem, rede docker local, daemon), nada escreve em
  # stdout e `combined` — logo `MGMT_STATUS` — vem VAZIO. Sem esta checagem, TODA
  # chamada deste script DEPOIS da autenticacao (dezenas delas, em declare/verify/
  # smoke/fanout/retry) cairia no `case ... *)` de quem a chamou com MGMT_STATUS="",
  # e sairia como EXIT_TOPOLOGIA_AUSENTE ("fila ou binding ausente, a declaracao e
  # NOSSA") quando a causa real e o DAEMON LOCAL ter caido NO MEIO do script — o
  # mesmo defeito que o EXIT_FERRAMENTA existe para impedir, um passo adiante do
  # laco de autenticacao.
  if [ -z "$MGMT_STATUS" ]; then
    echo "ERRO: 'docker run' nao produziu saida alguma para ${method} ${path}" >&2
    echo "      (exit do 'docker run': ${docker_rc}) — FALHA DA NOSSA FERRAMENTA" >&2
    echo "      (imagem '${CUSTODIA_RABBITMQ_CURL_IMAGE}', rede docker" >&2
    echo "      '${CUSTODIA_RABBITMQ_NETWORK}', ou o daemon local), NAO do broker." >&2
    echo "      Detalhe: ${MGMT_STDERR:-<sem stderr>}" >&2
    exit "$EXIT_FERRAMENTA"
  fi
  # `if/fi`, NAO `[ cond ] && echo` — esta e a ULTIMA instrucao da funcao. Um
  # `[ cond ] && echo` cujo `cond` de FALSO (o caso comum, MGMT_STDERR vazio) devolve
  # o status do teste (1) como retorno da FUNCAO INTEIRA, e como `mgmt_get`/`mgmt_put`/
  # `mgmt_post` sao chamadas como comando simples (nao dentro de `if`/`&&`) em TODO
  # lugar deste script, `set -e` mata o script ali, EM SILENCIO (sem ERR trap) — foi
  # medido: o script morria logo apos o primeiro `mgmt_get`, antes do `case` que le
  # `$MGMT_STATUS`. `if/fi` sem `else` devolve 0 quando a condicao e falsa, e o
  # `return 0` explicito blinda contra qualquer edicao futura que troque isto de novo.
  if [ -n "$MGMT_STDERR" ]; then
    echo "AVISO: curl relatou em ${method} ${path}: ${MGMT_STDERR}" >&2
  fi
  return 0
}
mgmt_get()  { mgmt_request GET  "$1" ; }
mgmt_put()  { mgmt_request PUT  "$1" "$2" ; }
mgmt_post() { mgmt_request POST "$1" "$2" ; }

# `jqn`: constroi JSON (modo -n, sem stdin) dentro de um container — usado para montar
# corpos de requisicao sem concatenar strings a mao (evita erro de escaping de aspas).
jqn() { docker run --rm "$CUSTODIA_RABBITMQ_JQ_IMAGE" -nc "$@" ; }
# `jqf`: le um JSON existente (via stdin) e aplica um filtro — usado para extrair
# campos das respostas da management API.
jqf() {
  local input="$1"; shift
  printf '%s' "$input" | docker run --rm -i "$CUSTODIA_RABBITMQ_JQ_IMAGE" -r "$@"
}

wait_for_auth() {
  echo "== aguardando o management do plataforma-rabbitmq confirmar a credencial (PADROES §10.2/§10.3/§10.15) =="
  local status="" docker_rc=0 err_txt="" stderr_file
  local tentativa
  for tentativa in $(seq 1 "$CUSTODIA_RABBITMQ_AUTH_WAIT_TRIES"); do
    stderr_file=$(mktemp)
    set +e
    status=$(docker run --rm --network "$CUSTODIA_RABBITMQ_NETWORK" "$CUSTODIA_RABBITMQ_CURL_IMAGE" \
      -sS -o /dev/null -w '%{http_code}' --max-time 10 \
      -u "$RABBITMQ_USER:$RABBITMQ_PASSWORD" \
      "${BASE_URL}/api/whoami" 2>"$stderr_file")
    docker_rc=$?
    set -e
    err_txt="$(cat "$stderr_file" 2>/dev/null)"
    rm -f "$stderr_file"

    # DISCRIMINADOR: NAO e a faixa de exit code do 'docker run' (125-127) — MEDIDO
    # pelo orquestrador em 2026-09-08, quatro cenarios reais:
    #   docker run --network rede-inexistente ...        -> exit 125
    #   docker run imagem:tag-inexistente ...             -> exit 125
    #   daemon inacessivel (DOCKER_HOST p/ socket morto)  -> exit 1
    #   daemon inacessivel (DOCKER_HOST p/ tcp morto)     -> exit 1
    #   curl com conexao recusada (docker/imagem/rede ok) -> exit 7, stdout tem "000"
    # "daemon fora" sai 1, a MESMA faixa de qualquer outra falha generica — uma
    # checagem por faixa 125-127 promete cobrir "o daemon local com problema" e NAO
    # cobre; o daemon fora cairia como EXIT_BROKER_INACESSIVEL, culpando o broker do
    # hub-precos por uma falha NOSSA, exatamente o que esta distincao existe para
    # impedir. O discriminador que nao depende de tabela de exit code: quando o
    # 'docker run' consegue EXECUTAR o curl, o curl SEMPRE escreve o `%{http_code}`
    # no stdout (aqui capturado em `status`), nem que seja "000" — e quando o docker
    # falha ANTES disso (rede/imagem/daemon), nenhum curl chega a rodar, entao nada
    # escreve em stdout e `status` vem VAZIO (o erro do docker CLI, medido, sai pelo
    # MESMO stderr que `2>"$stderr_file"` ja captura — por isso a mensagem abaixo
    # tem acesso a ele via `err_txt`, mesmo sem stdout nenhum). `status` vazio,
    # portanto, e FALHA DA NOSSA FERRAMENTA; `status` com qualquer coisa (incl.
    # "000") e "curl rodou e nao obteve resposta do broker".
    if [ -z "$status" ]; then
      echo "ERRO: 'docker run' nao produziu saida alguma para o container de" >&2
      echo "      verificacao (exit do 'docker run': ${docker_rc}) — FALHA DA NOSSA" >&2
      echo "      FERRAMENTA (imagem '${CUSTODIA_RABBITMQ_CURL_IMAGE}', rede docker" >&2
      echo "      '${CUSTODIA_RABBITMQ_NETWORK}', ou o daemon local), NAO do broker." >&2
      echo "      Detalhe: ${err_txt:-<sem stderr>}" >&2
      exit "$EXIT_FERRAMENTA"
    fi

    [ "$status" = "200" ] && break
    [ "$status" = "401" ] && break
    [ -n "$err_txt" ] && echo "    (tentativa ${tentativa}/${CUSTODIA_RABBITMQ_AUTH_WAIT_TRIES}: curl nao obteve resposta — ${err_txt})" >&2
    sleep "$CUSTODIA_RABBITMQ_AUTH_WAIT_SLEEP"
  done

  if [ "$status" = "401" ]; then
    echo "ERRO: management do plataforma-rabbitmq (${RABBITMQ_MANAGEMENT_HOST}:${RABBITMQ_MANAGEMENT_PORT})" >&2
    echo "      respondeu 401 para a credencial provisionada. O broker esta DE PE e" >&2
    echo "      recusou usuario/senha: RABBITMQ_USER/RABBITMQ_PASSWORD (secrets DESTE" >&2
    echo "      repositorio) divergem do que o broker aceitou no boot. E secret DESTE" >&2
    echo "      repositorio — acionavel aqui, e nao melhora com espera." >&2
    exit "$EXIT_AUTH_401"
  fi

  if [ "$status" != "200" ]; then
    echo "AVISO: management do plataforma-rabbitmq (${RABBITMQ_MANAGEMENT_HOST}:${RABBITMQ_MANAGEMENT_PORT})" >&2
    echo "       nao respondeu em ~$((CUSTODIA_RABBITMQ_AUTH_WAIT_TRIES * CUSTODIA_RABBITMQ_AUTH_WAIT_SLEEP))s" >&2
    echo "       (ultimo codigo: ${status:-vazio}; 000 = nao conectou; ultimo stderr do" >&2
    echo "       curl: ${err_txt:-<vazio>}). O plataforma-rabbitmq e servico do" >&2
    echo "       hub-precos, nao deste repositorio." >&2
    exit "$EXIT_BROKER_INACESSIVEL"
  fi

  echo "    credencial do broker confere (200 em /api/whoami via rede '${CUSTODIA_RABBITMQ_NETWORK}')"
}

handle_prices_exchange() {
  mgmt_get "/api/exchanges/%2F/prices"
  case "$MGMT_STATUS" in
    404)
      echo "    exchange 'prices' ausente — declarando (topic, durable, conforme o hub-precos declara hoje: RabbitMqConnectionProvider.cs)"
      local body
      body=$(jqn '{type:"topic", durable:true, auto_delete:false, internal:false, arguments:{}}')
      mgmt_put "/api/exchanges/%2F/prices" "$body"
      case "$MGMT_STATUS" in
        201|204) echo "    exchange 'prices' declarado" ;;
        *) echo "ERRO: falha ao declarar exchange 'prices' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2; exit "$EXIT_TOPOLOGIA_AUSENTE" ;;
      esac
      ;;
    200)
      local ok
      ok=$(jqf "$MGMT_BODY" '(.type=="topic") and (.durable==true) and (.auto_delete==false) and (.internal==false) and (.arguments=={})')
      if [ "$ok" != "true" ]; then
        echo "ERRO: exchange 'prices' JA EXISTE com propriedades DIVERGENTES do esperado" >&2
        echo "      (type=topic, durable=true, auto_delete=false, internal=false, arguments={})." >&2
        echo "      NAO REDECLARANDO: redeclarar quebraria o publish do hub-precos e do" >&2
        echo "      operacoes no proximo boot deles. A divergencia e do DONO do exchange" >&2
        echo "      (hub-precos); a correcao e no repo dele, nao aqui." >&2
        echo "      Atual: ${MGMT_BODY}" >&2
        exit "$EXIT_EXCHANGE_PRICES_DIVERGENTE"
      fi
      echo "    exchange 'prices' ja existe e confere (nao redeclarado)"
      ;;
    *)
      echo "ERRO: nao consegui consultar o exchange 'prices' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2
      exit "$EXIT_TOPOLOGIA_AUSENTE"
      ;;
  esac
}

declare_fanout_exchange() {
  local name="$1"
  local body
  body=$(jqn '{type:"fanout", durable:true, auto_delete:false, internal:false, arguments:{}}')
  mgmt_put "/api/exchanges/%2F/${name}" "$body"
  case "$MGMT_STATUS" in
    201|204) echo "    exchange '${name}' (fanout, nosso) declarado/confirmado" ;;
    *) echo "ERRO: falha ao declarar exchange '${name}' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2; exit "$EXIT_TOPOLOGIA_AUSENTE" ;;
  esac
}

declare_queue_quorum() {
  local name="$1" delivery_limit="$2" dlx="${3:-}"
  local body
  if [ -n "$dlx" ]; then
    body=$(jqn --argjson dl "$delivery_limit" --arg dlx "$dlx" \
      '{durable:true, arguments:{"x-queue-type":"quorum","x-delivery-limit":$dl,"x-dead-letter-exchange":$dlx}}')
  else
    body=$(jqn --argjson dl "$delivery_limit" \
      '{durable:true, arguments:{"x-queue-type":"quorum","x-delivery-limit":$dl}}')
  fi
  mgmt_put "/api/queues/%2F/${name}" "$body"
  case "$MGMT_STATUS" in
    201|204) echo "    fila '${name}' (quorum) declarada/confirmada" ;;
    *) echo "ERRO: falha ao declarar fila '${name}' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2; exit "$EXIT_TOPOLOGIA_AUSENTE" ;;
  esac
}

declare_queue_classic_retry() {
  local name="$1" ttl_ms="$2" dlx="$3"
  local body
  body=$(jqn --argjson ttl "$ttl_ms" --arg dlx "$dlx" \
    '{durable:true, arguments:{"x-queue-type":"classic","x-message-ttl":$ttl,"x-dead-letter-exchange":$dlx}}')
  mgmt_put "/api/queues/%2F/${name}" "$body"
  case "$MGMT_STATUS" in
    201|204) echo "    fila '${name}' (classic, SEM CONSUMIDOR — fila de atraso) declarada/confirmada" ;;
    *) echo "ERRO: falha ao declarar fila '${name}' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2; exit "$EXIT_TOPOLOGIA_AUSENTE" ;;
  esac
}

declare_binding() {
  local exchange="$1" queue="$2" routing_key="$3"
  local body
  body=$(jqn --arg rk "$routing_key" '{routing_key:$rk}')
  mgmt_post "/api/bindings/%2F/e/${exchange}/q/${queue}" "$body"
  case "$MGMT_STATUS" in
    201) echo "    binding ${exchange} -[${routing_key:-<vazia>}]-> ${queue} declarado/confirmado" ;;
    *) echo "ERRO: falha ao declarar binding ${exchange}->${queue} (chave '${routing_key}', HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2; exit "$EXIT_TOPOLOGIA_AUSENTE" ;;
  esac
}

# Versao SEM `exit`: usada dentro de lacos (wait_for_messages_ready) onde um `exit`
# so mataria o SUBSHELL do `$( )` que envolve o laco, sem parar o script — o chamador
# veria so um retorno nao-zero e diagnosticaria errado: uma fila apagada ou o
# management caindo NO MEIO de uma prova de fumaca seria relatada como "binding nao
# esta roteando", mandando o operador investigar o lugar errado.
fetch_queue_soft() {
  local name="$1"
  mgmt_get "/api/queues/%2F/${name}"
  [ "$MGMT_STATUS" = "200" ]
}

fetch_queue() {
  local name="$1"
  if ! fetch_queue_soft "$name"; then
    echo "ERRO: fila '${name}' nao existe ou nao respondeu (HTTP ${MGMT_STATUS}) — a declaracao e nossa." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
}

assert_type_durable() {
  local name="$1" body="$2" expected_type="$3"
  local tipo durable
  tipo=$(jqf "$body" '.type')
  durable=$(jqf "$body" '.durable')
  if [ "$tipo" != "$expected_type" ] || [ "$durable" != "true" ]; then
    echo "ERRO: fila '${name}' tem type='${tipo}' durable='${durable}', esperado type='${expected_type}' durable=true." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    ${name}: type=${tipo} durable=${durable} (confere)"
}

assert_arg() {
  local ctx="$1" body="$2" argname="$3" expected="$4"
  local atual
  atual=$(jqf "$body" --arg a "$argname" '.arguments[$a] // "AUSENTE"')
  if [ "$atual" != "$expected" ]; then
    echo "ERRO: ${ctx}: argumento '${argname}' = '${atual}', esperado '${expected}'." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  # "confere" aqui prova so o que foi DECLARADO (.arguments e o eco do nosso PUT) —
  # nao o que o broker efetivamente APLICA. Ler o pedido de volta e vacuo por si so
  # (§10.8): quando existe campo de TOPO com o efeito real (ex.: delivery_limit),
  # assert_arg_effective o confere separadamente.
  echo "    ${ctx}: ${argname}=${atual} (declarado; confere com o esperado)"
}

# Le um campo de TOPO da resposta (nao .arguments) — o que o BROKER efetivamente
# aplica, distinto do que foi PEDIDO. MEDIDO pelo orquestrador em 2026-09-08 contra o
# rabbitmq:4-management-alpine 4.3.5 de producao: com x-delivery-limit:-1 declarado,
# o campo de topo `delivery_limit` responde a STRING "unlimited" (nao -1); com o
# default (sem declarar), responde ao NUMERO 20. Comparar `delivery_limit` contra a
# string "-1" falha sempre — o valor certo para o caso ilimitado e "unlimited".
assert_arg_effective() {
  local ctx="$1" body="$2" fieldname="$3" expected="$4"
  local atual
  atual=$(jqf "$body" --arg f "$fieldname" '.[$f] // "AUSENTE" | tostring')
  if [ "$atual" != "$expected" ]; then
    echo "ERRO: ${ctx}: campo EFETIVO '${fieldname}' = '${atual}', esperado '${expected}'" >&2
    echo "      (isto e o que o BROKER aplica, nao o eco do que foi pedido em .arguments)." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    ${ctx}: ${fieldname} (efetivo, PADROES §10.8) = ${atual} (confere)"
}

verify_exchange_type() {
  local name="$1" expected_type="$2"
  mgmt_get "/api/exchanges/%2F/${name}"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: exchange '${name}' nao existe ou nao respondeu (HTTP ${MGMT_STATUS}) — a declaracao e nossa." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  local tipo durable
  tipo=$(jqf "$MGMT_BODY" '.type')
  durable=$(jqf "$MGMT_BODY" '.durable')
  if [ "$tipo" != "$expected_type" ] || [ "$durable" != "true" ]; then
    echo "ERRO: exchange '${name}' tem type='${tipo}' durable='${durable}', esperado type='${expected_type}' durable=true." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    exchange '${name}': type=${tipo} durable=${durable} (confere)"
}

assert_arg_absent() {
  local ctx="$1" body="$2" argname="$3"
  local atual
  atual=$(jqf "$body" --arg a "$argname" '.arguments[$a] // "AUSENTE"')
  if [ "$atual" != "AUSENTE" ]; then
    echo "ERRO: ${ctx}: argumento '${argname}' presente com valor '${atual}', mas a decisao desta fase e NAO declarar este argumento." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    ${ctx}: ${argname} ausente (confere)"
}

verify_prices_bindings() {
  mgmt_get "/api/exchanges/%2F/prices/bindings/source"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: nao consegui ler os bindings de origem do exchange 'prices' (HTTP ${MGMT_STATUS})." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi

  local atuais
  atuais=$(jqf "$MGMT_BODY" --arg d "custodia.prices" \
    '[.[] | select(.destination==$d and .destination_type=="queue") | .routing_key] | sort | .[]')

  local esperadas_arr=(corpactions.# eod.ready prices.# trades.registered)
  # MODO DE TESTE NEGATIVO (PADROES §10.8: asserção negativa precisa de controle
  # positivo, e o corolário é que a checagem tem que SABER dizer "não"). Rodar este
  # script manualmente com CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO=<chave inexistente>
  # injeta uma routing key que ninguém binda na lista ESPERADA — contra um broker com
  # a topologia CORRETA — e prova que esta comparação sai diferente de zero quando o
  # conjunto não bate. É o mesmo mecanismo que pegaria um binding real removido à mão.
  if [ -n "${CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO:-}" ]; then
    echo "    MODO DE TESTE NEGATIVO: injetando routing key inexistente '${CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO}' na lista esperada" >&2
    esperadas_arr+=("$CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO")
  fi
  # `LC_ALL=C` no lado do host para casar com a ordem de `sort` do jq (codepoint),
  # usada em `atuais` acima — sort dependente de locale no host x codepoint no jq e
  # inocuo para as quatro chaves de hoje, mas frageis para a chave ARBITRARIA que
  # CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO injeta: o controle negativo podia "funcionar"
  # por ordenacao divergente, nao por deteccao de fato.
  local esperadas
  esperadas=$(printf '%s\n' "${esperadas_arr[@]}" | LC_ALL=C sort)

  if [ "$atuais" != "$esperadas" ]; then
    echo "ERRO: bindings de 'prices' -> 'custodia.prices' divergem do esperado (PADROES §10.8)." >&2
    echo "--- esperado ---" >&2
    echo "$esperadas" >&2
    echo "--- atual ---" >&2
    echo "$atuais" >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    bindings de 'prices' -> 'custodia.prices' conferem (4 routing keys: prices.#, corpactions.#, eod.ready, trades.registered)"
}

verify_binding_exists() {
  local exchange="$1" queue="$2"
  mgmt_get "/api/exchanges/%2F/${exchange}/bindings/source"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: nao consegui ler os bindings de origem do exchange '${exchange}' (HTTP ${MGMT_STATUS})." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  local achou
  achou=$(jqf "$MGMT_BODY" --arg d "$queue" '[.[] | select(.destination==$d and .destination_type=="queue")] | length')
  if [ "$achou" = "0" ]; then
    echo "ERRO: binding '${exchange}' -> '${queue}' ausente." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    binding '${exchange}' -> '${queue}' confere"
}

# `exit_code`: cada chamador tem seu proprio codigo de saida (fumaca/fanout/retry) —
# sem o parametro, toda falha de publish saia com EXIT_SMOKE_FALHOU mesmo quando
# quem chamou foi a prova de fanout ou de retry, escondendo QUAL prova falhou.
publish_probe() {
  local exchange="$1" routing_key="$2" payload="$3" exit_code="${4:-$EXIT_SMOKE_FALHOU}"
  local body
  body=$(jqn --arg rk "$routing_key" --arg p "$payload" '{properties:{}, routing_key:$rk, payload:$p, payload_encoding:"string"}')
  mgmt_post "/api/exchanges/%2F/${exchange}/publish" "$body"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: falha ao publicar mensagem de prova em '${exchange}' (routing_key='${routing_key}', HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2
    exit "$exit_code"
  fi
  local routed
  routed=$(jqf "$MGMT_BODY" '.routed')
  if [ "$routed" != "true" ]; then
    echo "ERRO: exchange '${exchange}' aceitou o publish mas 'routed=false' para routing_key='${routing_key}' — nenhum binding casou." >&2
    exit "$exit_code"
  fi
}

# Remove, de forma SEGURA, uma mensagem de prova identificada por MARCADOR de uma
# fila. NUNCA remove as cegas: a `custodia.prices` acumula trafego REAL desde o
# instante em que o binding existe (LEIA-ME-KIT, "Perder o volume do broker apaga
# fila e binding" e "Teste manual em tabela append-only deixa lixo que nao sai"), e um
# `basic.get` as cegas removeria a CABECA da fila (FIFO) — que pode ser um
# trades.registered de verdade. Por isso: PRIMEIRO espia com
# ackmode=reject_requeue_true (nao remove nada, so olha e devolve), e SO remove
# (ackmode=ack_requeue_false) se o payload da cabeca bater com o marcador que ESTE
# script publicou. Se nao bater, ha trafego real na frente da prova na fila FIFO — a
# limpeza e abortada, sem tocar em nada, e a mensagem de prova (inocua) fica ate o F4
# processa-la. Retorna 0 se removeu, 1 se nao (nao removeu NADA em nenhum dos casos).
safe_take_probe() {
  local queue="$1" marker="$2"
  local peek_body
  peek_body=$(jqn --argjson count 1 --arg ackmode "reject_requeue_true" \
    '{count:$count, ackmode:$ackmode, encoding:"auto", truncate:50000}')
  mgmt_post "/api/queues/%2F/${queue}/get" "$peek_body"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "AVISO: nao consegui espiar '${queue}' para limpar a mensagem de prova (HTTP ${MGMT_STATUS})." >&2
    return 1
  fi
  local n
  n=$(jqf "$MGMT_BODY" 'length')
  if [ "$n" = "0" ]; then
    echo "AVISO: '${queue}' ficou vazia antes da limpeza — nada a remover." >&2
    return 1
  fi
  local head_payload
  head_payload=$(jqf "$MGMT_BODY" '.[0].payload')
  if [ "$head_payload" != "$marker" ]; then
    echo "AVISO: a cabeca de '${queue}' NAO e a mensagem de prova (esperava marcador" >&2
    echo "       '${marker}', encontrei '${head_payload}'). Ha trafego REAL na frente" >&2
    echo "       dela na fila FIFO — a limpeza e ABORTADA para nao remover uma mensagem" >&2
    echo "       real. A mensagem de prova, inocua, fica na fila ate o F4 processa-la." >&2
    return 1
  fi
  local ack_body
  ack_body=$(jqn --argjson count 1 --arg ackmode "ack_requeue_false" \
    '{count:$count, ackmode:$ackmode, encoding:"auto", truncate:50000}')
  mgmt_post "/api/queues/%2F/${queue}/get" "$ack_body"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "AVISO: encontrei a mensagem de prova na cabeca de '${queue}' mas o ack falhou (HTTP ${MGMT_STATUS})." >&2
    return 1
  fi
  echo "    mensagem de prova removida de '${queue}' (basic.get + ack da mensagem especifica)"
  return 0
}

# Em `custodia.prices` (a UNICA fila deste script com trafego de TERCEIRO — as
# outras tres so recebem mensagem nossa nesta fase), cada espiada de `safe_take_probe`
# conta uma tentativa de entrega (x-delivery-limit) contra QUALQUER MENSAGEM que
# esteja na cabeca. No estado NORMAL desta fase — que e "ha backlog real" assim que o
# binding trades.registered existir — a cabeca NUNCA e a nossa prova, entao a limpeza
# SEMPRE aborta (ve o "AVISO: a cabeca... NAO e a mensagem de prova" em
# safe_take_probe) e a espiada paga o custo total sem entregar benefico nenhum: dez
# deploys seguidos gastariam dez das vinte tentativas da MESMA mensagem real que
# estiver na cabeca, ate ela ser dead-letrada para a DLQ POR CULPA da nossa
# verificacao (nao se perde — a DLQ e -1 — mas sai da fila principal sem que o F4
# jamais a processe).
#
# O GUARDA E PELA CONTAGEM DE DEPOIS do publish (`$depois`/`$prices_atual`, ja
# calculada por wait_for_messages_ready), NUNCA pela de ANTES: "antes==0" parece
# provar fila vazia, mas antes vem de um contador com defasagem de ate ~10s (ver o
# comentario de wait_for_messages_ready) — um trade real pode chegar DEPOIS da
# leitura de "antes" e ANTES da nossa publicacao, e a cabeca seria dele mesmo assim.
# `depois == 1` TORNA IMPROVAVEL, NAO IMPOSSIVEL, que a mensagem na fila seja de
# terceiro — nao "fecha por construcao". `wait_for_messages_ready` retorna na
# PRIMEIRA leitura que satisfaz `>= antes+1`, e `messages_ready` tem a mesma
# defasagem de ate ~10s ja citada acima: e perfeitamente possivel um trade real
# entrar e a NOSSA publicacao ainda nao ter sido contabilizada nessa leitura,
# fazendo `depois==1` mesmo com a mensagem da cabeca sendo a REAL (a nossa,
# atrasada na contagem, apareceria depois). Por isso a checagem de PAYLOAD em
# `safe_take_probe` nao e redundante: e ELA que fecha o caso, comparando o
# conteudo de verdade — `depois==1` so torna esse caminho menos frequente de
# precisar abortar a limpeza.
maybe_take_probe_prices() {
  local queue="$1" marker="$2" depois="$3"
  if [ "$depois" != "1" ]; then
    echo "    limpeza da mensagem de prova em '${queue}' PULADA de proposito: DEPOIS" >&2
    echo "      do nosso publish a fila tinha ${depois} mensagem(ns), nao 1 — mais de" >&2
    echo "      uma so pode significar trafego REAL coexistindo com a nossa prova" >&2
    echo "      nesta fila FIFO, e espiar a cabeca gastaria uma tentativa de entrega" >&2
    echo "      (x-delivery-limit) de uma mensagem REAL sem nenhum beneficio (a" >&2
    echo "      limpeza abortaria de qualquer forma)." >&2
    echo "      A mensagem de prova, inocua, fica na fila ate o F4 processa-la." >&2
    return 1
  fi
  safe_take_probe "$queue" "$marker"
}

# Leitura UNICA (sem laco) de `.messages_ready`, com a MESMA guarda numerica de
# `wait_for_messages_ready` (ver o comentario dela abaixo): a management API OMITE
# o campo numa fila recem-declarada, antes da primeira emissao de estatisticas — e
# o broker de producao roda com collect_statistics_interval=60000. Sem esta guarda,
# `jq -r` devolve a string "null", e `$((antes + 1))` sob `set -u` morre com
# "unbound variable"/erro aritmetico em vez de uma mensagem que diz o que houve —
# e essa janela (broker recem-recriado, filas recem-declaradas, prova rodando
# segundos depois) e EXATAMENTE a que esta fase existe para cobrir.
read_messages_ready() {
  local ctx="$1" body="$2"
  local atual
  atual=$(jqf "$body" '.messages_ready // "AUSENTE"')
  if [[ ! "$atual" =~ ^[0-9]+$ ]]; then
    echo "ERRO: ${ctx}: campo 'messages_ready' ausente ou nao-numerico ('${atual}') na" >&2
    echo "      resposta da management API. Isto acontece quando a fila foi" >&2
    echo "      DECLARADA AGORA MESMO e o broker ainda nao emitiu a primeira" >&2
    echo "      estatistica (producao roda com collect_statistics_interval=60000 —" >&2
    echo "      ver o comentario de wait_for_messages_ready abaixo). NAO e falha de" >&2
    echo "      topologia nem de credencial." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "$atual"
}

# A contagem `messages_ready` da management API NAO e instantanea. MEDIDO pelo
# orquestrador em 2026-09-08 contra o `plataforma-rabbitmq` de PRODUCAO: cinco
# publicacoes seguidas numa fila-sonda, cronometrando ate a contagem mudar, deram
# 7s/8s/10s/9s/10s — duas de cinco bateram no teto antigo de 10s. O broker de
# producao roda com `collect_statistics_interval=60000` (../hub-precos/infra/rabbitmq/
# conf.d/20-limits.conf:23; confirmado com `rabbitmqctl eval
# 'application:get_env(rabbit, collect_statistics_interval).'` -> `{ok,60000}`). OS
# DOIS NUMEROS NAO SE CONTRADIZEM: a defasagem OBSERVADA (~10s) e bem menor que o
# intervalo de estatisticas CONFIGURADO (60s) — quem ler isto depois nao precisa
# "corrigir" um dos dois para bater com o outro. O orcamento abaixo e um TIMEOUT, nao
# um sleep fixo: alarga-lo nao custa nada no caminho feliz (a condicao de saida do
# laco e a PRIMEIRA vez que o valor bate, nao o teto), entao ele sobra folga (~90s)
# em vez de tentar acertar o numero exato.
#
# Dentro do laco, se a fila nunca respondeu 200 em NENHUMA tentativa, isso e
# "topologia sumiu no meio da prova" — NAO e "o binding nao esta roteando" (o
# diagnostico que os chamadores emitiam antes de saberem distinguir os dois casos).
# `fetch_queue` normal sairia direto (exit dentro do `$( )` do chamador so mata o
# SUBSHELL, nao o script — o erro escaparia calado), entao o laco usa a variante
# `_soft` e devolve um codigo PROPRIO (2) para esse caso, distinto do timeout comum
# (1), para o chamador escolher a mensagem certa.
#
# `cmp`: "ge" (crescimento, >= alvo — o normal para delta em fila com trafego de
# terceiro, ver os chamadores abaixo) ou "eq" (igualdade exata — usado so onde nao ha
# trafego de terceiro possivel, ou onde o resultado ja e so informativo).
wait_for_messages_ready() {
  local queue="$1" alvo="$2" cmp="${3:-ge}" tries="${4:-30}" sleep_s="${5:-3}"
  local atual="" respondeu_200=0 campo_numerico_ok=0
  local tentativa
  for tentativa in $(seq 1 "$tries"); do
    if fetch_queue_soft "$queue"; then
      respondeu_200=1
      # Guarda numerica (o `assert_arg` ja se protege com `// "AUSENTE"` — aqui nao
      # havia guarda nenhuma): se `.messages_ready` vier ausente/nulo/nao-numerico
      # por qualquer motivo transiente, `[ "$atual" -ge "$alvo" ]` sairia com status
      # 2 e a mensagem "integer expression expected" no stderr, em vez de so
      # tentar de novo na proxima iteracao do laco. O campo vem AUSENTE numa fila
      # recem-declarada, antes da primeira emissao de estatisticas (producao roda
      # com collect_statistics_interval=60000) — respondeu_200 e campo_numerico_ok
      # sao FLAGS SEPARADAS de proposito: "a fila nao respondeu" e "a fila respondeu
      # mas a estatistica ainda nao existe" sao diagnosticos DIFERENTES, e o
      # chamador precisa saber qual dos dois aconteceu.
      atual=$(jqf "$MGMT_BODY" '.messages_ready // "AUSENTE"')
      if [[ "$atual" =~ ^[0-9]+$ ]]; then
        campo_numerico_ok=1
        # `if/fi`, NAO `[ cond ] && { …; return 0; }`: a mesma classe de armadilha
        # do `mgmt_request` (comentario acima) — hoje nao morde porque ha um `sleep`
        # depois do `fi` do `if fetch_queue_soft`, mas fica a uma edicao de morder.
        if [ "$cmp" = "ge" ]; then
          if [ "$atual" -ge "$alvo" ]; then
            echo "$atual"
            return 0
          fi
        elif [ "$cmp" = "eq" ]; then
          if [ "$atual" = "$alvo" ]; then
            echo "$atual"
            return 0
          fi
        fi
      fi
    fi
    sleep "$sleep_s"
  done
  echo "$atual"
  # Tres desfechos de falha, cada um com diagnostico proprio (o chamador escolhe a
  # mensagem certa por eles): 2 = nunca respondeu 200 (fila/management sumiu);
  # 3 = respondeu 200 sempre, mas o campo nunca ficou numerico (estatistica nao
  # emitida — NAO e "binding quebrado"); 1 = respondeu com numero, mas nunca
  # atingiu o alvo (o timeout comum, isto sim pode ser binding quebrado).
  if [ "$respondeu_200" = "0" ]; then
    return 2
  fi
  if [ "$campo_numerico_ok" = "0" ]; then
    return 3
  fi
  return 1
}

smoke_test_prices() {
  local queue="custodia.prices"
  fetch_queue "$queue"
  local antes
  antes=$(read_messages_ready "$queue" "$MGMT_BODY")

  local marker="custodia-f2-smoke-$(date +%s)-$$"
  publish_probe "prices" "prices.smoke" "$marker" "$EXIT_SMOKE_FALHOU"

  # CRESCIMENTO (>= antes+1), nao igualdade. A decisao se justifica pelo FUTURO, nao
  # pelo passado: o operacoes esta no ar desde 2026-09-06 e pode publicar
  # trades.registered a qualquer instante — MEDIDO em 2026-09-08: ate essa data ele
  # NAO tinha publicado nada (outbox com 0 linhas, tabela operacoes com 0 registros;
  # o relay dele MARCA publicado_em em vez de apagar, entao a ausencia e conferivel).
  # Uma exigencia de igualdade quebraria no PRIMEIRO trade real que chegasse entre
  # "antes" e "depois" (levaria a contagem a antes+2), e esta fase existe justamente
  # para que ele chegue. A prova de que O NOSSO binding roteou nao depende dessa
  # igualdade: publish_probe ja confere `routed==true` na resposta do publish,
  # evidencia direta e imune a trafego de terceiro; o crescimento aqui e reforco.
  local esperado=$((antes + 1))
  local depois rc
  # `X=$(f) && rc=0 || rc=$?`, NAO `X=$(f)` seguido de `rc=$?` em linha separada: a
  # ATRIBUICAO via substituicao de comando E o comando simples que `set -e` avalia,
  # entao quando `f` devolve nao-zero, o errexit mata o script NA PROPRIA atribuicao
  # — a linha seguinte com `rc=$?` NUNCA executa. MEDIDO: `v=$(f); rc=$?` com `f`
  # devolvendo 1 sob `set -e` termina o script antes do `rc=$?`, em silencio. A forma
  # `A && B || C` e uma lista AND-OR, isenta de errexit por definicao — e o `$?` que
  # `rc=$?` le dentro do `||` e o da PROPRIA atribuicao (A), preservado corretamente.
  depois=$(wait_for_messages_ready "$queue" "$esperado" ge) && rc=0 || rc=$?
  if [ "$rc" -eq 2 ]; then
    echo "ERRO: nao consegui LER '${queue}' em NENHUMA tentativa apos publicar (fila pode" >&2
    echo "      ter sido apagada ou o management caiu no meio da prova) — isto NAO e" >&2
    echo "      falha de roteamento do binding." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -eq 3 ]; then
    echo "ERRO: '${queue}' respondeu 200 em toda tentativa, mas 'messages_ready' nunca" >&2
    echo "      ficou numerico — o broker ainda nao emitiu a primeira estatistica desta" >&2
    echo "      fila (producao roda com collect_statistics_interval=60000). Isto NAO e" >&2
    echo "      falha de roteamento do binding." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -ne 0 ]; then
    echo "ERRO: PARADA POR LIMITE — prova de fumaca falhou: publiquei em 'prices' com" >&2
    echo "      routing key 'prices.smoke' e messages_ready de '${queue}' nao cresceu a" >&2
    echo "      pelo menos ${esperado} a tempo (ficou em ${depois}, partindo de ${antes})." >&2
    echo "      O binding 'prices.#' -> '${queue}' nao esta roteando." >&2
    exit "$EXIT_SMOKE_FALHOU"
  fi
  echo "    PARADA POR COMPLETUDE — delta confere: messages_ready ${antes} -> ${depois} (publicado prices.smoke, NUNCA trades.registered — LEIA-ME-KIT)"

  if maybe_take_probe_prices "$queue" "$marker" "$depois"; then
    local final
    final=$(wait_for_messages_ready "$queue" "$antes" eq) && rc=0 || rc=$?
    if [ "$rc" -eq 0 ]; then
      echo "    limpeza confere: messages_ready voltou a ${antes}"
    else
      echo "AVISO: apos remover a mensagem de prova, nao confirmei que messages_ready de" >&2
      echo "       '${queue}' voltou a ${antes} (ultimo valor lido: ${final:-desconhecido})." >&2
      echo "       Isso e esperado se trafego REAL (trades.registered de producao) chegou" >&2
      echo "       durante a janela do teste — nao e falha do mecanismo, que ja foi" >&2
      echo "       provado pelo delta acima." >&2
    fi
  fi
}

fanout_control_test() {
  local exchange="$1" queue="$2"
  fetch_queue "$queue"
  local antes
  antes=$(read_messages_ready "$queue" "$MGMT_BODY")

  local marker="custodia-f2-fanout-$(date +%s)-$$-${queue}"
  publish_probe "$exchange" "chave.que.ninguem.binda" "$marker" "$EXIT_FANOUT_FALHOU"

  # Crescimento (>=), nao igualdade — mesma razao de smoke_test_prices, aplicada
  # aqui por uniformidade mesmo estas duas filas nao tendo trafego de terceiro nesta
  # fase (nada mais publica nelas ainda).
  local esperado=$((antes + 1))
  local depois rc
  # Ver o comentario em smoke_test_prices sobre por que e `A && B || C`, nunca
  # `A; rc=$?` em linhas separadas, sob `set -e`.
  depois=$(wait_for_messages_ready "$queue" "$esperado" ge) && rc=0 || rc=$?
  if [ "$rc" -eq 2 ]; then
    echo "ERRO: nao consegui LER '${queue}' em NENHUMA tentativa apos publicar — fila pode" >&2
    echo "      ter sido apagada ou o management caiu no meio da prova." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -eq 3 ]; then
    echo "ERRO: '${queue}' respondeu 200 em toda tentativa, mas 'messages_ready' nunca" >&2
    echo "      ficou numerico — o broker ainda nao emitiu a primeira estatistica desta" >&2
    echo "      fila (producao roda com collect_statistics_interval=60000). Isto NAO e" >&2
    echo "      falha do fanout." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -ne 0 ]; then
    echo "ERRO: PARADA POR LIMITE — prova de fanout falhou: publiquei em '${exchange}'" >&2
    echo "      com routing key arbitraria 'chave.que.ninguem.binda' e messages_ready de" >&2
    echo "      '${queue}' nao cresceu a pelo menos ${esperado} a tempo (ficou em" >&2
    echo "      ${depois}, partindo de ${antes}). Um exchange NOSSO declarado como" >&2
    echo "      fanout deveria ignorar a routing key e entregar sempre." >&2
    exit "$EXIT_FANOUT_FALHOU"
  fi
  echo "    PARADA POR COMPLETUDE — fanout '${exchange}' -> '${queue}' confere: mensagem com routing key arbitraria chegou (delta ${antes} -> ${depois})"

  safe_take_probe "$queue" "$marker" || true
}

# Prova que o retry fecha o ciclo (custodia.retry.in -> custodia.retry -> TTL ->
# custodia.retry.dlx -> custodia.prices) EM DUAS METADES, a exigencia do Pronto (b) do
# ROADMAP: a mensagem tem que SAIR da custodia.retry E CHEGAR na custodia.prices. A
# versao anterior so provava a segunda metade monitorando o crescimento de
# custodia.prices — verdadeiro TAMBEM com custodia.retry.dlx quebrado e um trade real
# chegando por coincidencia (a metade PERMISSIVA, nao a estrita).
#
# A CORRECAO NAO ESPIA A CABECA DE NADA A MAIS (o custo da espiada continua sendo so o
# de safe_take_probe, uma vez, no fim): `custodia.retry` NAO TEM CONSUMIDOR e NAO TEM
# OUTRO PUBLICADOR alem deste script nesta fase (nasceu classic, sem
# x-delivery-limit, e ninguem mais escreve nela) — TRAFEGO DE TERCEIRO NUNCA a
# atravessa. Por isso a contagem DELA e um sinal DETERMINISTICO, sem ambiguidade
# nenhuma, para as duas pontas do ciclo:
#   1. sobe para antes+1 IMEDIATAMENTE apos o publish em custodia.retry.in (prova que
#      o binding custodia.retry.in -> custodia.retry esta roteando);
#   2. volta a "antes" depois do TTL (prova que a mensagem SAIU — TTL expirou e o
#      broker dead-letrou).
# So DEPOIS das duas confirmadas, o codigo confere que custodia.prices tambem cresceu
# — reforco de que o dead-letter chegou la (custodia.retry.dlx -> custodia.prices) e
# nao se perdeu no caminho. Este ultimo passo AINDA e a metade permissiva (mesmo
# LIMITE HONESTO de antes: um trade real chegando no mesmo instante seria lido, por
# engano, como "o retry chegou") — mas agora e reforco sobre uma prova ja
# deterministica, nao a UNICA evidencia do ciclo.
retry_cycle_test() {
  fetch_queue "custodia.retry"
  local retry_antes
  retry_antes=$(read_messages_ready "custodia.retry" "$MGMT_BODY")

  fetch_queue "custodia.prices"
  local prices_antes
  prices_antes=$(read_messages_ready "custodia.prices" "$MGMT_BODY")

  local marker="custodia-f2-retry-$(date +%s)-$$"
  publish_probe "custodia.retry.in" "prices.smoke" "$marker" "$EXIT_RETRY_FALHOU"

  local retry_esperado=$((retry_antes + 1))
  local retry_subiu rc
  # Ver o comentario em smoke_test_prices sobre por que e `A && B || C`, nunca
  # `A; rc=$?` em linhas separadas, sob `set -e`.
  retry_subiu=$(wait_for_messages_ready "custodia.retry" "$retry_esperado" ge) && rc=0 || rc=$?
  if [ "$rc" -eq 2 ]; then
    echo "ERRO: nao consegui LER 'custodia.retry' apos publicar em 'custodia.retry.in'" >&2
    echo "      — a fila pode ter sido apagada ou o management caiu no meio da prova." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -eq 3 ]; then
    echo "ERRO: 'custodia.retry' respondeu 200 em toda tentativa, mas 'messages_ready'" >&2
    echo "      nunca ficou numerico — o broker ainda nao emitiu a primeira estatistica" >&2
    echo "      desta fila (producao roda com collect_statistics_interval=60000). Isto" >&2
    echo "      NAO e falha de roteamento do binding." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -ne 0 ]; then
    echo "ERRO: PARADA POR LIMITE — publiquei em 'custodia.retry.in' e messages_ready de" >&2
    echo "      'custodia.retry' nao cresceu a pelo menos ${retry_esperado} a tempo" >&2
    echo "      (ficou em ${retry_subiu}). O binding 'custodia.retry.in' ->" >&2
    echo "      'custodia.retry' nao esta roteando." >&2
    exit "$EXIT_RETRY_FALHOU"
  fi
  echo "    PARADA POR COMPLETUDE — messages_ready de 'custodia.retry' subiu (${retry_antes} -> ${retry_subiu}, DETERMINISTICO: fila sem consumidor e sem outro publicador)"

  echo "    aguardando o TTL (${CUSTODIA_RETRY_TTL_MS}ms) com folga, ate $((CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES * CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP))s, para 'custodia.retry' esvaziar (PADROES §10.31: completude x limite)"

  local retry_final
  retry_final=$(wait_for_messages_ready "custodia.retry" "$retry_antes" eq "$CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES" "$CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP") && rc=0 || rc=$?
  if [ "$rc" -eq 2 ]; then
    echo "ERRO: nao consegui LER 'custodia.retry' durante a espera do TTL." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -eq 3 ]; then
    echo "ERRO: 'custodia.retry' respondeu 200 em toda tentativa, mas 'messages_ready'" >&2
    echo "      nunca ficou numerico durante a espera do TTL — estatistica nao emitida" >&2
    echo "      (producao roda com collect_statistics_interval=60000). Isto NAO e falha" >&2
    echo "      de dead-letter." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -ne 0 ]; then
    echo "ERRO: PARADA POR LIMITE — 'custodia.retry' nao esvaziou de volta a" >&2
    echo "      ${retry_antes} apos $((CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES * CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP))s" >&2
    echo "      (ficou em ${retry_final}; TTL declarado: ${CUSTODIA_RETRY_TTL_MS}ms). O TTL" >&2
    echo "      nao expirou a tempo, ou nao esta dead-letrando." >&2
    exit "$EXIT_RETRY_FALHOU"
  fi
  echo "    PARADA POR COMPLETUDE — 'custodia.retry' esvaziou de volta a ${retry_antes} (DETERMINISTICO: TTL expirou e dead-letrou)"

  local prices_esperado=$((prices_antes + 1))
  local prices_atual
  prices_atual=$(wait_for_messages_ready "custodia.prices" "$prices_esperado" ge) && rc=0 || rc=$?
  if [ "$rc" -eq 2 ]; then
    echo "ERRO: nao consegui LER 'custodia.prices' apos 'custodia.retry' esvaziar." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -eq 3 ]; then
    echo "ERRO: 'custodia.prices' respondeu 200 em toda tentativa, mas 'messages_ready'" >&2
    echo "      nunca ficou numerico — estatistica nao emitida (producao roda com" >&2
    echo "      collect_statistics_interval=60000). Isto NAO e mensagem perdida." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  elif [ "$rc" -ne 0 ]; then
    echo "ERRO: 'custodia.retry' esvaziou mas 'custodia.prices' nao cresceu — a mensagem" >&2
    echo "      foi perdida entre 'custodia.retry.dlx' e 'custodia.prices'." >&2
    exit "$EXIT_RETRY_FALHOU"
  fi
  echo "    reforco — 'custodia.prices' cresceu (${prices_antes} -> ${prices_atual}): o dead-letter chegou (custodia.retry.dlx -> custodia.prices)"

  maybe_take_probe_prices "custodia.prices" "$marker" "$prices_atual" || true
}

main() {
  wait_for_auth

  echo "== declarando topologia (idempotente) =="
  handle_prices_exchange
  declare_fanout_exchange "custodia.dlx"
  declare_fanout_exchange "custodia.parking"
  declare_fanout_exchange "custodia.retry.in"
  declare_fanout_exchange "custodia.retry.dlx"

  declare_queue_quorum "custodia.prices" 20 "custodia.dlx"
  declare_queue_quorum "custodia.prices.dlq" -1
  declare_queue_quorum "custodia.parked" -1
  declare_queue_classic_retry "custodia.retry" "$CUSTODIA_RETRY_TTL_MS" "custodia.retry.dlx"

  declare_binding "prices" "custodia.prices" "prices.#"
  declare_binding "prices" "custodia.prices" "corpactions.#"
  declare_binding "prices" "custodia.prices" "eod.ready"
  declare_binding "prices" "custodia.prices" "trades.registered"
  declare_binding "custodia.dlx" "custodia.prices.dlq" ""
  declare_binding "custodia.parking" "custodia.parked" ""
  declare_binding "custodia.retry.in" "custodia.retry" ""
  declare_binding "custodia.retry.dlx" "custodia.prices" ""

  echo "== verificacao bloqueante (PADROES §10.8: leitura de volta, com controle negativo exercitavel via CUSTODIA_TOPOLOGIA_TESTE_NEGATIVO) =="
  verify_prices_bindings

  fetch_queue "custodia.prices"
  assert_type_durable "custodia.prices" "$MGMT_BODY" "quorum"
  assert_arg "custodia.prices" "$MGMT_BODY" "x-delivery-limit" "20"
  # O `delivery_limit` EFETIVO de custodia.prices e 20 tanto com o argumento
  # aplicado quanto com o DEFAULT do broker (§10.9) — esta linha, sozinha, passaria
  # nos dois casos. Quem carrega a informacao que importa e o PAR com o
  # `assert_arg` acima (o que foi declarado) mais os dois `unlimited` de
  # custodia.prices.dlq/custodia.parked abaixo, que so existem PORQUE declaramos
  # -1 (o default lá seria 20, nao unlimited) — nao ha assinatura nova a inventar
  # aqui so para este caso especifico.
  assert_arg_effective "custodia.prices" "$MGMT_BODY" "delivery_limit" "20"
  assert_arg "custodia.prices" "$MGMT_BODY" "x-dead-letter-exchange" "custodia.dlx"
  assert_arg_absent "custodia.prices" "$MGMT_BODY" "x-max-length"
  assert_arg_absent "custodia.prices" "$MGMT_BODY" "x-message-ttl"

  fetch_queue "custodia.prices.dlq"
  assert_type_durable "custodia.prices.dlq" "$MGMT_BODY" "quorum"
  assert_arg "custodia.prices.dlq" "$MGMT_BODY" "x-delivery-limit" "-1"
  assert_arg_effective "custodia.prices.dlq" "$MGMT_BODY" "delivery_limit" "unlimited"

  fetch_queue "custodia.parked"
  assert_type_durable "custodia.parked" "$MGMT_BODY" "quorum"
  assert_arg "custodia.parked" "$MGMT_BODY" "x-delivery-limit" "-1"
  assert_arg_effective "custodia.parked" "$MGMT_BODY" "delivery_limit" "unlimited"

  fetch_queue "custodia.retry"
  assert_type_durable "custodia.retry" "$MGMT_BODY" "classic"
  assert_arg "custodia.retry" "$MGMT_BODY" "x-message-ttl" "$CUSTODIA_RETRY_TTL_MS"
  assert_arg "custodia.retry" "$MGMT_BODY" "x-dead-letter-exchange" "custodia.retry.dlx"
  # SEM x-max-length e SEM x-overflow: e essa ausencia que faz o exemplo motivador
  # do laco quente (ROADMAP, Decisao A) deixar de existir. Ate agora so estava
  # protegida de lado, pelo 400 que uma redeclaracao divergente produziria — nao
  # lida de volta, diferente da propriedade analoga da custodia.prices acima.
  assert_arg_absent "custodia.retry" "$MGMT_BODY" "x-max-length"
  assert_arg_absent "custodia.retry" "$MGMT_BODY" "x-overflow"

  verify_exchange_type "custodia.dlx" "fanout"
  verify_exchange_type "custodia.parking" "fanout"
  verify_exchange_type "custodia.retry.in" "fanout"
  verify_exchange_type "custodia.retry.dlx" "fanout"

  verify_binding_exists "custodia.dlx" "custodia.prices.dlq"
  verify_binding_exists "custodia.parking" "custodia.parked"
  verify_binding_exists "custodia.retry.in" "custodia.retry"
  verify_binding_exists "custodia.retry.dlx" "custodia.prices"

  echo "== prova de fumaca (DELTA, nunca valor absoluto) em custodia.prices =="
  smoke_test_prices

  echo "== prova de que o fanout nao engole nada (custodia.dlx->dlq, custodia.parking->parked) =="
  fanout_control_test "custodia.dlx" "custodia.prices.dlq"
  fanout_control_test "custodia.parking" "custodia.parked"

  echo "== prova de que o retry fecha o ciclo (retry.in -> retry -> TTL -> prices) =="
  retry_cycle_test

  echo "== topologia declarada e verificada com sucesso =="
}

main
