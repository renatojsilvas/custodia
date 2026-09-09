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
#   'docker run' (curl OU jq) nao executou (nossa  -> EXIT_FERRAMENTA (reprova — NAO
#   ferramenta: imagem, rede docker local, daemon);     e diagnostico de broker nenhum;
#   OU DNS de RABBITMQ_MANAGEMENT_HOST nao resolve      ver EXIT_TOPOLOGIA_AUSENTE
#   na rede docker (curl exit 6) — config NOSSA,        abaixo, que e outro caso.
#   nao melhora com espera, sai rapido feito o 401       DNS: sai rapido, sem
#                                                       esperar o laco inteiro)
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

# Prefixo da fila-sonda temporaria de `retry_cycle_test` — CONSTANTE COMPARTILHADA
# entre a criacao do nome (retry_cycle_test) e a varredura de orfas
# (sweep_orphan_sondas), para as duas nunca divergirem. Termina em "." de
# proposito: o sufixo real e "$$.$(date +%s)" (PID e epoch), entao o prefixo
# sozinho nunca casa por acaso com nenhum outro nome deste script.
CUSTODIA_RETRY_SONDA_PREFIXO="custodia.f2.retry.sonda."

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
# delas falhou. TAMBEM usado quando o DNS de RABBITMQ_MANAGEMENT_HOST nao resolve
# na rede docker (curl exit 6) — e config NOSSA (host/rede errados), nao do
# broker, e nao melhora com espera, igual ao 401 — e quando o container do `jq`
# falha ao executar (imagem errada), pela mesma razao do container do `curl`.
EXIT_FERRAMENTA=17
# Removemos uma mensagem que nao era nossa: `safe_take_probe` confere o payload
# ANTES do ack (espiada), mas ha uma janela de ~1s entre essa espiada e o ack
# destrutivo em que a cabeca pode mudar (outro consumidor — ex.: operador na
# management UI durante o deploy). Reconferir o payload NA RESPOSTA do proprio
# ack e a unica forma de nao afirmar "removi a nossa prova" quando removemos
# outra coisa. Codigo proprio porque isto NAO e "topologia ausente" nem
# "prova falhou" — e um efeito colateral destrutivo que ja aconteceu.
EXIT_LIMPEZA_INSEGURA=18

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
mgmt_get()    { mgmt_request GET    "$1" ; }
mgmt_put()    { mgmt_request PUT    "$1" "$2" ; }
mgmt_post()   { mgmt_request POST   "$1" "$2" ; }
mgmt_delete() { mgmt_request DELETE "$1" ; }

# `jqn`: constroi JSON (modo -n, sem stdin) dentro de um container — usado para montar
# corpos de requisicao sem concatenar strings a mao (evita erro de escaping de aspas).
# GUARDA: sem o `set +e`/checagem de `rc`, uma imagem de jq invalida
# (CUSTODIA_RABBITMQ_JQ_IMAGE errada) faria o `docker run` falhar com o exit code
# CRU do docker (ex.: 125), que `set -e` propagaria direto para fora do script —
# reprova, mas com o codigo ERRADO: o cabecalho e a mensagem do ci.yml prometem
# que falha de ferramenta tem codigo PROPRIO (EXIT_FERRAMENTA=17), e essa promessa
# so cobria o container do curl (mgmt_request) ate aqui.
jqn() {
  local out rc
  set +e
  out=$(docker run --rm "$CUSTODIA_RABBITMQ_JQ_IMAGE" -nc "$@")
  rc=$?
  set -e
  if [ "$rc" -ne 0 ]; then
    echo "ERRO: 'docker run' (jq, construcao de JSON) falhou (exit ${rc}) — FALHA DA" >&2
    echo "      NOSSA FERRAMENTA (imagem '${CUSTODIA_RABBITMQ_JQ_IMAGE}')." >&2
    exit "$EXIT_FERRAMENTA"
  fi
  printf '%s' "$out"
}
# `jqf`: le um JSON existente (via stdin) e aplica um filtro — usado para extrair
# campos das respostas da management API. MESMA guarda de `jqn` acima.
jqf() {
  local input="$1"; shift
  local out rc
  set +e
  out=$(printf '%s' "$input" | docker run --rm -i "$CUSTODIA_RABBITMQ_JQ_IMAGE" -r "$@")
  rc=$?
  set -e
  if [ "$rc" -ne 0 ]; then
    echo "ERRO: 'docker run' (jq, leitura de JSON) falhou (exit ${rc}) — FALHA DA NOSSA" >&2
    echo "      FERRAMENTA (imagem '${CUSTODIA_RABBITMQ_JQ_IMAGE}')." >&2
    exit "$EXIT_FERRAMENTA"
  fi
  printf '%s' "$out"
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

    # DNS que NAO RESOLVE e um caso a parte, e NAO pode terminar em
    # EXIT_BROKER_INACESSIVEL (11, ::warning::+segue): numa rede docker, "nao
    # resolve o nome" significa que RABBITMQ_MANAGEMENT_HOST (ou
    # CUSTODIA_RABBITMQ_NETWORK) esta ERRADO — fato de CONFIGURACAO NOSSA, nao do
    # broker, e repetir o laco inteiro (ate 3 min) nao vai mudar isso. O
    # discriminador e o exit code DOCUMENTADO do proprio curl — CURLE_COULDNT_
    # RESOLVE_HOST e sempre 6, estavel entre versoes e locales (ao contrario de
    # fazer grep no texto de erro, que muda). O agravante que torna isto GRAVE, nao
    # cosmetico: o controle compensatorio (::warning:: + regra
    # custodia-topologia-ausente) vigia se a FILA existe — se um deploy anterior ja
    # criou a fila, ela continua existindo, a regra fica OK, e este erro de
    # configuracao pode passar despercebido, com o ci.yml verde, por MESES.
    # connect-refused (7) e timeout (28) continuam repetindo e terminando em 11 —
    # so a falha que NAO MELHORA COM ESPERA sai rapido aqui, exatamente como o 401.
    if [ "$docker_rc" -eq 6 ]; then
      echo "ERRO: DNS nao resolveu '${RABBITMQ_MANAGEMENT_HOST}' na rede docker" >&2
      echo "      '${CUSTODIA_RABBITMQ_NETWORK}' (curl exit 6 = CURLE_COULDNT_RESOLVE_HOST)." >&2
      echo "      Isto e CONFIGURACAO NOSSA (RABBITMQ_MANAGEMENT_HOST ou" >&2
      echo "      CUSTODIA_RABBITMQ_NETWORK errados) — acionavel aqui, e nao melhora" >&2
      echo "      com espera, entao nao repetimos o laco inteiro." >&2
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
    echo "ERRO: fila '${name}' nao existe ou nao respondeu (HTTP ${MGMT_STATUS}) — a declaracao e" >&2
    echo "      nossa, ou o broker caiu NO MEIO da declaracao — veja o stderr do curl acima" >&2
    echo "      (MGMT_STDERR/AVISO anteriores) antes de assumir que a fila nunca existiu." >&2
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

# `has($a)`, NAO `.arguments[$a] // "AUSENTE"`: em jq, `//` trata `false` E `null`
# como "nao-valor" e cai no lado direito — um argumento presente com o valor
# `false` (ou `null`) leria como AUSENTE. Nenhum dos nossos argumentos hoje usa
# esses valores, mas `has()` fecha a classe inteira em vez de confiar nisso.
# `(.arguments // {})`, NAO `.arguments` cru: `has()` em jq FALHA (erro, nao
# "false") quando o valor a esquerda e `null` em vez de um objeto — e uma
# resposta sem `arguments` e exatamente esse caso. Sem o `// {}`, essa falha sai
# de `jqf` como `EXIT_FERRAMENTA` (17) — "nossa ferramenta falhou" para o que e
# so uma resposta sem `arguments`, o mesmo padrao de guarda que
# `assert_no_poisoning_policy` ja usa em `(.effective_policy_definition // {})`.
assert_arg() {
  local ctx="$1" body="$2" argname="$3" expected="$4"
  local atual
  atual=$(jqf "$body" --arg a "$argname" 'if ((.arguments // {})|has($a)) then (.arguments // {})[$a] else "AUSENTE" end')
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
    echo "ERRO: exchange '${name}' nao existe ou nao respondeu (HTTP ${MGMT_STATUS}) — a declaracao e" >&2
    echo "      nossa, ou o broker caiu NO MEIO da declaracao — veja o stderr do curl acima" >&2
    echo "      (MGMT_STDERR/AVISO anteriores) antes de assumir que o exchange nunca existiu." >&2
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
  atual=$(jqf "$body" --arg a "$argname" 'if ((.arguments // {})|has($a)) then (.arguments // {})[$a] else "AUSENTE" end')
  if [ "$atual" != "AUSENTE" ]; then
    echo "ERRO: ${ctx}: argumento '${argname}' presente com valor '${atual}', mas a decisao desta fase e NAO declarar este argumento." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    ${ctx}: ${argname} ausente (confere)"
}

# PADROES §10.40: uma POLICY do broker aplica `max-length`, `max-length-bytes`,
# `message-ttl`, `overflow` e `expires` SEM TOCAR em `.arguments` — e policy e o
# caminho NORMAL de operacao num broker que e do hub-precos, nao nosso.
# `assert_arg_absent` le `.arguments` (o que NOS declaramos) e fica CEGO para
# isso: uma policy pode ligar exatamente as garantias que esta fase existe para
# manter desligadas (teto com reject-publish envenenando o relay de terceiros —
# ARQUITETURA/cabecalho deste arquivo —, TTL descartando em silencio, ou
# `expires` APAGANDO A FILA INTEIRA) sem que o script perceba, porque o broker
# aplica a policy POR CIMA da declaracao, sem alterar o que foi declarado. O
# estado resultante ja esta no MESMO corpo que `fetch_queue` devolve, em
# `.effective_policy_definition` — so faltava olhar para la, que e o que
# `assert_arg_effective` ja faz para `delivery_limit`, aplicado aqui aos campos
# que uma policy pode ligar.
#
# `expires` E PIOR QUE OS OUTROS QUATRO, e por isso entra na lista (§10.8
# corolario: fecha a CLASSE, nao colecione o EXEMPLO) — ele apaga a fila
# INTEIRA, com backlog e bindings juntos, o incidente que esta fase inteira
# existe para impedir. E a precondicao do broker para aplicar `expires`
# ("unused") esta PERMANENTEMENTE satisfeita nesta fase: `custodia.prices` nao
# tem consumidor ate o F4, entao ela e "unused" o tempo todo entre um deploy e o
# proximo, com o unico acesso sendo o `basic.get` desta propria verificacao. Sem
# esta checagem, o resultado so apareceria DEPOIS, pela regra
# `custodia-topologia-ausente` — o script teria aprovado com EXIT=0.
#
# CANDIDATAS AVALIADAS E EXCLUIDAS, com motivo (nao acrescente sem revisitar por
# que ficaram de fora): `delivery-limit` ja e coberta por `assert_arg_effective`
# (campo de topo, ja lido de volta separadamente); `dead-letter-exchange` tem
# PRECEDENCIA DE ARGUMENTO na `custodia.prices` (ver o efeito colateral abaixo) e
# nas filas terminais nao ha o que dead-letrar nesta fase — vira defeito do F4
# quando o consumidor existir, nao agora; `dead-letter-strategy` nao degrada
# ABAIXO do default que ja documentamos (`at-most-once`) — nao ha "pior" para uma
# policy impor aqui; `queue-mode` e `max-in-memory-*` afetam PAGINACAO (RAM x
# disco), nao DESCARTE de mensagem — fora do invariante que esta fase protege.
#
# EFEITO COLATERAL A NAO CONFUNDIR: a `custodia.retry` TEM `message-ttl` por
# ARGUMENTO nosso (declarado por nos, nao por policy) — esta checagem olha
# `effective_policy_definition`, nao `.arguments`, entao ela nao acusa o nosso
# proprio TTL. MAS: para um argumento que NOS declaramos (como este), o
# ARGUMENTO tem precedencia sobre a policy — uma policy de `message-ttl` que
# alcance `custodia.retry` apareceria em `effective_policy_definition` e faria
# esta checagem reprovar nomeando o dono do broker por algo que PODE NAO ESTAR
# EM VIGOR naquela fila especifica (o nosso argumento pode estar sobrepondo). A
# mensagem de erro abaixo registra isso para quem for investigar.
assert_no_poisoning_policy() {
  local ctx="$1" body="$2"
  local achadas
  achadas=$(jqf "$body" '(.effective_policy_definition // {}) as $d | ["max-length","max-length-bytes","message-ttl","overflow","expires"] | map(select(. as $k | $d | has($k))) | join(", ")')
  if [ -n "$achadas" ]; then
    # `.policy` (regular) e `.operator_policy` sao campos DIFERENTES, com
    # comandos de remocao DIFERENTES (/api/policies/... x
    # /api/operator-policies/...) — MEDIDO em producao em 2026-09-09: o
    # `effective_policy_definition` do 4.3.5 MESCLA as duas (operator policy tem
    # prioridade nas chaves que se sobrepoem), entao esta checagem cobre operator
    # policy tambem, mas so se o DIAGNOSTICO disser qual das duas (ou as duas)
    # e a fonte — nomear ".policy" quando quem aplicou foi so a operator policy
    # manda o operador procurar por uma policy que nao existe (medido: `.policy`
    # vem `null` e a mensagem antiga imprimia "policy 'AUSENTE'"). Operator
    # policy e o instrumento mais PROVAVEL aqui, nao o menos: e como o DONO do
    # broker impoe teto a filas alheias.
    local policy_nome operator_nome origem
    policy_nome=$(jqf "$body" '.policy // "AUSENTE"')
    operator_nome=$(jqf "$body" '.operator_policy // "AUSENTE"')
    origem=""
    if [ "$policy_nome" != "AUSENTE" ]; then
      origem="policy '${policy_nome}' (ajuste/remova via /api/policies/%2F/${policy_nome})"
    fi
    if [ "$operator_nome" != "AUSENTE" ]; then
      if [ -n "$origem" ]; then
        origem="${origem} E operator policy '${operator_nome}' (via /api/operator-policies/%2F/${operator_nome})"
      else
        origem="operator policy '${operator_nome}' (ajuste/remova via /api/operator-policies/%2F/${operator_nome})"
      fi
    fi
    if [ -z "$origem" ]; then
      origem="uma policy sem .policy/.operator_policy capturados (os dois vieram nulos, mas effective_policy_definition tem a definicao mesmo assim — investigue direto no broker, nao so por estes dois campos)"
    fi
    echo "ERRO: ${ctx}: ${origem} aplica [${achadas}]" >&2
    echo "      por cima da declaracao (via effective_policy_definition), sem tocar" >&2
    echo "      em .arguments. Isto liga exatamente as garantias que esta fase" >&2
    echo "      existe para manter desligadas nesta fila (teto com reject-publish" >&2
    echo "      envenenando trafego de terceiro, TTL descartando em silencio, ou" >&2
    echo "      'expires' apagando a fila inteira)." >&2
    echo "      Para argumentos que NOS declaramos nesta fila (ex.: x-message-ttl na" >&2
    echo "      custodia.retry), o ARGUMENTO tem precedencia sobre a policy — confira" >&2
    echo "      '.arguments' na resposta para saber se a policy esta REALMENTE em" >&2
    echo "      vigor ou so presente e sobreposta." >&2
    echo "      A CORRECAO E NO REPO DONO DO BROKER (hub-precos) — nao aqui." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
}

# CHAMADA NO INICIO DA FASE DE VERIFICACAO, antes de qualquer `verify_binding_exists`
# (ver o comentario de `retry_cycle_test` sobre por que os dois arquivos/pontos tem
# que ser mantidos juntos). `retry_cycle_test` binda uma fila-sonda TEMPORARIA em
# `custodia.retry.dlx` e a apaga explicitamente no fim — mas se aquela execucao
# morrer NO MEIO (entre bindar e apagar), a sonda fica ORFA, ainda bindada. Como o
# R3 tornou `verify_binding_exists` uma comparacao de CONJUNTO EXATO (correcao que
# NAO se relaxa), uma sonda orfa faz `custodia.retry.dlx` ter DOIS destinos em vez
# de um, e o deploy SEGUINTE reprovaria por LIXO NOSSO — com uma mensagem que
# aponta para "binding diverge do esperado", diagnostico ENGANOSO (a causa real e
# uma execucao anterior morta, nao a topologia). O `x-expires` da sonda cura
# sozinho, mas so depois de 120s: qualquer deploy dentro dessa janela falharia sem
# causa real. Esta varredura torna a execucao AUTO-CURAVEL em vez de refem do
# crash anterior, e devolve ao `x-expires` o papel que o comentario dele sempre
# disse que era o certo: REDE DE SEGURANCA, nao mecanismo principal.
#
# DUAS GUARDAS: (1) apaga SO o que casa o PREFIXO EXATO da sonda
# (CUSTODIA_RETRY_SONDA_PREFIXO, a MESMA constante que retry_cycle_test usa para
# nomea-la — nunca um padrao mais amplo: `custodia.prices` esta a um glob de
# distancia, e apagar fila errada neste script e o pior dano possivel); (2) LOGA
# cada sonda orfa apagada, com o NOME — varredura silenciosa esconde que a
# execucao anterior morreu no meio, que e informacao que o operador quer.
#
# GARANTIA IMPLICITA DE QUE ESTA VARREDURA DEPENDE, E QUE PRECISA SER PROCURADA
# ANTES DE MEXER NELA: ela so e segura porque o `concurrency.group` do `ci.yml`
# SERIALIZA os runs de deploy em push (nunca duas execucoes deste script ao mesmo
# tempo). Duas execucoes SIMULTANEAS fariam a varredura de UMA apagar a sonda
# VIVA da OUTRA (o prefixo casa qualquer sonda, viva ou orfa — nao ha como
# distinguir pela idade sem reintroduzir a corrida contra `x-expires`). Hoje o CI
# garante isso; alguem rodando este script A MAO durante um deploy em andamento,
# nao. Se um dia o `concurrency.group` for removido ou relaxado, esta varredura
# precisa ser revisitada primeiro.
sweep_orphan_sondas() {
  mgmt_get "/api/queues/%2F"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: nao consegui listar as filas do vhost para varrer sondas orfas do" >&2
    echo "      ciclo de retry (HTTP ${MGMT_STATUS})." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  local orfas
  orfas=$(jqf "$MGMT_BODY" --arg p "$CUSTODIA_RETRY_SONDA_PREFIXO" \
    '[.[] | select(.name | startswith($p)) | .name] | .[]')
  if [ -z "$orfas" ]; then
    return 0
  fi
  local nome
  while IFS= read -r nome; do
    [ -n "$nome" ] || continue
    echo "AVISO: sonda ORFA do ciclo de retry encontrada e removida: '${nome}'" >&2
    echo "       (prefixo '${CUSTODIA_RETRY_SONDA_PREFIXO}') — uma execucao anterior" >&2
    echo "       morreu entre bindar e apagar a sonda; o x-expires so a apagaria em" >&2
    echo "       ate 120s. Esta varredura evita reprovar o deploy por lixo nosso." >&2
    mgmt_delete "/api/queues/%2F/${nome}" || true
  done <<< "$orfas"
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

# COMPARACAO DE CONJUNTO, como a irma `verify_prices_bindings` — nao mera
# existencia. Um `achou>0` (a versao anterior) passa verde com um binding EXTRA
# na frente do esperado: MEDIDO pelo revisor — acrescentar
# `custodia.dlx -> custodia.prices` (alem do `custodia.dlx -> custodia.prices.dlq`
# correto) cria `custodia.prices --DLX--> custodia.dlx --> custodia.prices`, um
# LACO QUENTE onde uma mensagem envenenada roda para sempre, queimando 20
# tentativas de x-delivery-limit por volta — e o script aprovava (EXIT=0). E a
# METADE PERMISSIVA do mesmo invariante que `verify_prices_bindings` ja prova nas
# duas direcoes por CONJUNTO; aqui era so metade porque cada um dos quatro
# exchanges nossos tem EXATAMENTE UM destino pretendido — o conjunto esperado e
# sempre um singleton, e "nem a mais, nem a menos" fecha os dois lados.
verify_binding_exists() {
  local exchange="$1" queue="$2"
  mgmt_get "/api/exchanges/%2F/${exchange}/bindings/source"
  if [ "$MGMT_STATUS" != "200" ]; then
    echo "ERRO: nao consegui ler os bindings de origem do exchange '${exchange}' (HTTP ${MGMT_STATUS})." >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  local atuais esperadas
  atuais=$(jqf "$MGMT_BODY" '[.[] | select(.destination_type=="queue") | .destination] | sort | .[]')
  esperadas=$(printf '%s\n' "$queue" | LC_ALL=C sort)
  if [ "$atuais" != "$esperadas" ]; then
    echo "ERRO: bindings de '${exchange}' divergem do esperado — o conjunto de destinos" >&2
    echo "      tem que ser EXATAMENTE '${queue}', nem a mais nem a menos (comparacao" >&2
    echo "      de CONJUNTO, mesma forma estrita de verify_prices_bindings)." >&2
    echo "--- esperado ---" >&2
    echo "$esperadas" >&2
    echo "--- atual ---" >&2
    echo "$atuais" >&2
    exit "$EXIT_TOPOLOGIA_AUSENTE"
  fi
  echo "    bindings de '${exchange}' conferem (destino unico: '${queue}')"
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
  # RECONFERE o payload NA RESPOSTA DO PROPRIO ACK — nao confia que a espiada
  # acima ainda descreve a cabeca no instante do ack. Entre os dois POSTs (a
  # espiada e este) ha DOIS `docker run` separados, ~1s cada, e nesse intervalo a
  # cabeca PODE MUDAR se outro consumidor existir (hoje nao ha, mas
  # `fanout_control_test` chama esta funcao, SEM esta guarda, tambem em
  # `custodia.prices.dlq` e `custodia.parked` — filas que existem justamente para
  # o OPERADOR drenar a mao, e um operador na management UI durante o deploy e
  # exatamente essa janela; o F4 tambem poe um consumidor de verdade na
  # `custodia.prices`, e este script continua rodando a cada deploy depois disso).
  # Um `ack_requeue_false` remove O QUE ESTIVER LA, cego — sem esta reconferencia,
  # o dano (mensagem REAL destruida) e SILENCIOSO. Falhar ALTO aqui e a unica forma
  # de nao afirmar "removi a nossa prova" quando removemos outra coisa.
  local removido_payload
  removido_payload=$(jqf "$MGMT_BODY" '.[0].payload // "AUSENTE"')
  if [ "$removido_payload" != "$marker" ]; then
    echo "ERRO: removi de '${queue}' uma mensagem que NAO ERA a nossa prova (payload" >&2
    echo "      removido: '${removido_payload}', esperado marcador '${marker}'). A" >&2
    echo "      cabeca mudou entre a espiada (reject_requeue_true) e o ack" >&2
    echo "      (ack_requeue_false) — algo mais consumiu desta fila durante o deploy." >&2
    echo "      Isto JA ACONTECEU: uma mensagem real foi destruida por este script." >&2
    exit "$EXIT_LIMPEZA_INSEGURA"
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
    echo "      A mensagem de prova fica na fila. COM BACKLOG, no MAXIMO UMA fica" >&2
    echo "      retida por deploy (a do ciclo de retry — com backlog," >&2
    echo "      smoke_test_prices nem publica mais nada). DUAS so aconteceriam se a" >&2
    echo "      fila estivesse VAZIA no inicio do deploy E trafego real chegasse na" >&2
    echo "      janela (fazendo a limpeza da PROPRIA prova de fumaca tambem abortar)." >&2
    echo "      NAO afirmamos que ela e inocua: 'prices.smoke' nao e payload de" >&2
    echo "      contrato nenhum da §5.1, e cabe ao F4 decidir o que fazer ao" >&2
    echo "      encontra-la — o F4 herda em torno de UMA 'custodia-f2-retry-*' por" >&2
    echo "      deploy do F2, e e esse o numero que ele vai contar, nao esta fase." >&2
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

  # COM BACKLOG, ESTA PROVA E VACUA — pule, nao publique. MEDIDO pelo revisor
  # (B9): com uma fila de TERCEIRO tambem bindada em 'prices.#' e trafego real na
  # janela, a prova passa MESMO com o binding 'prices.#' -> 'custodia.prices'
  # REMOVIDO — o crescimento que ela mede e satisfeito por qualquer coisa que
  # cresca a contagem, nao especificamente pelo NOSSO binding. E a limpeza seria
  # IMPOSSIVEL (a cabeca ja nao e nossa — ver maybe_take_probe_prices), deixando
  # lixo sem nenhuma prova em troca. Com fila vazia (antes==0) o comportamento
  # desta funcao continua IGUAL ao de sempre; a mudanca so entra quando ha
  # backlog. A prova de ROTEAMENTO deste deploy, nesse caso, e a comparacao de
  # CONJUNTO dos bindings (verify_prices_bindings, ja rodada na fase de
  # verificacao, ANTES desta funcao) — ela e estrita nas duas direcoes e le a
  # mesma fonte de verdade (a management API), entao nada se perde ao pular a
  # parte COMPORTAMENTAL da prova quando ela nao pode ser feita com seguranca.
  if [ "$antes" != "0" ]; then
    echo "    prova de fumaca (comportamental) PULADA de proposito: '${queue}' ja tem" >&2
    echo "      ${antes} mensagem(ns) de backlog REAL — publicar e inspecionar" >&2
    echo "      comportamento so agrega valor com a fila vazia (ver comentario acima)." >&2
    echo "      O roteamento ja foi provado por conjunto em verify_prices_bindings." >&2
    return 0
  fi

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
# custodia.retry.dlx -> custodia.prices), a exigencia do Pronto (b) do ROADMAP: a
# mensagem tem que SAIR da custodia.retry E CHEGAR na custodia.prices.
#
# TECNICA TROCADA (a anterior era FLAKY e, num caso, VACUA — as duas medidas pelo
# revisor):
#   VACUA (a checagem final era "custodia.prices cresceu"): com o binding
#   `custodia.retry.dlx -> custodia.prices` REMOVIDO, a mensagem expira, e
#   dead-letrada para um exchange SEM aquele binding, e o broker a descarta — mas
#   as DUAS metades "DETERMINISTICAS" (custodia.retry sobe, depois esvazia) ainda
#   passavam, porque elas so olham a PROPRIA custodia.retry, nunca a chegada. E
#   com trafego real coincidente, a checagem final ("custodia.prices cresceu")
#   tambem passava, com o texto "o dead-letter chegou" — FALSO, EXIT=0. Provar que
#   a mensagem SAIU da custodia.retry nao prova que ela CHEGOU em algum lugar.
#   FLAKY (a metade "custodia.retry sobe para antes+1"): e uma corrida entre o TTL
#   e a defasagem do contador `messages_ready`. Em producao, o broker roda com
#   collect_statistics_interval=60000 — O DOBRO do TTL de 30000ms — entao e
#   plausivel que a PRIMEIRA emissao de estatisticas so aconteca DEPOIS que a
#   mensagem ja tiver expirado e saido: medir a "decolagem" e estruturalmente
#   flaky, nao um artefato de TTL curto de teste local.
#
# A CORRECAO: uma FILA-SONDA temporaria, bindada SO ao `custodia.retry.dlx`
# (NUNCA a exchange de terceiro). Ele e FANOUT: quando o TTL expira, entrega a
# TODAS as filas bindadas na MESMA operacao atomica do broker — a `custodia.prices`
# real E a sonda. Isso resolve as duas falhas de uma vez:
#   - SEM CORRIDA: nao precisamos observar o estado INTERMEDIARIO (a contagem
#     subindo antes do TTL expirar) — so esperamos o TTL e conferimos o RESULTADO
#     final por `basic.get`, uma operacao direta na fila, sem a defasagem de
#     estatisticas que afeta `messages_ready`;
#   - SEM VACUIDADE: a sonda so recebe TRAFEGO NOSSO (nada mais publica em
#     `custodia.retry.in` nesta fase) — a prova e por MARCADOR (payload lido de
#     volta), nao por uma contagem que trafego de terceiro tambem satisfaz;
#   - SEM GASTAR tentativa de entrega da `custodia.prices` real: a sonda e nossa,
#     isolada, e o `basic.get`+ack nela nao arrisca remover mensagem alheia.
# `x-expires` na sonda e REDE DE SEGURANCA, nao o mecanismo PRINCIPAL de limpeza:
# se o script morrer no meio (matando o DELETE explicito do fim), a sonda fica
# ORFA, bindada a `custodia.retry.dlx`, e o `x-expires` so a apaga sozinha depois
# de 120s de ociosidade. NESSA JANELA, a sonda ORFA PARTICIPA DO CONJUNTO que
# `verify_binding_exists("custodia.retry.dlx", "custodia.prices")` confere —
# desde o R3, essa checagem exige o conjunto EXATO {custodia.prices}, e uma sonda
# orfa ainda bindada faz o conjunto ter DOIS destinos, reprovando por LIXO NOSSO
# (com uma mensagem que aponta para "binding diverge do esperado", diagnostico
# enganoso — a causa real e uma sonda morta, nao a topologia). Por isso o
# mecanismo PRINCIPAL de limpeza e `sweep_orphan_sondas`, chamada no INICIO da
# fase de verificacao em `main()`, ANTES de qualquer `verify_binding_exists` —
# QUEM MEXER NUM DOS DOIS (o nome/prefixo da sonda aqui, ou a comparacao de
# conjunto la) PRECISA VER O OUTRO. O `x-expires` continua existindo para o caso
# raro de a propria varredura falhar (ex.: o script morrer ANTES de chegar la).
#
# A asserção "custodia.retry esvaziou de volta a antes" e MANTIDA como REFORCO
# (NAO reprova sozinha: e exatamente a metade sujeita a corrida contra
# collect_statistics_interval=60000, entao uma falha dela vira AVISO, nunca
# EXIT_RETRY_FALHOU) — o VEREDITO da prova passa a ser o marcador na sonda.
retry_cycle_test() {
  fetch_queue "custodia.retry"
  local retry_antes
  retry_antes=$(read_messages_ready "custodia.retry" "$MGMT_BODY")

  local sonda="${CUSTODIA_RETRY_SONDA_PREFIXO}$$.$(date +%s)"
  local sonda_body
  sonda_body=$(jqn --argjson expira 120000 '{durable:true, arguments:{"x-expires":$expira}}')
  mgmt_put "/api/queues/%2F/${sonda}" "$sonda_body"
  case "$MGMT_STATUS" in
    201|204) : ;;
    *)
      echo "ERRO: falha ao declarar a fila-sonda '${sonda}' (HTTP ${MGMT_STATUS}): ${MGMT_BODY}" >&2
      exit "$EXIT_RETRY_FALHOU"
      ;;
  esac
  local bind_body
  bind_body=$(jqn '{routing_key:""}')
  mgmt_post "/api/bindings/%2F/e/custodia.retry.dlx/q/${sonda}" "$bind_body"
  if [ "$MGMT_STATUS" != "201" ]; then
    echo "ERRO: falha ao bindar a fila-sonda '${sonda}' a 'custodia.retry.dlx' (HTTP" >&2
    echo "      ${MGMT_STATUS}): ${MGMT_BODY}" >&2
    mgmt_delete "/api/queues/%2F/${sonda}" || true
    exit "$EXIT_RETRY_FALHOU"
  fi

  local marker="custodia-f2-retry-$(date +%s)-$$"
  publish_probe "custodia.retry.in" "prices.smoke" "$marker" "$EXIT_RETRY_FALHOU"
  echo "    publiquei em 'custodia.retry.in' (routed=true ja confere que ha binding" \
       "ativo para 'custodia.retry' — a prova ESTRUTURAL e exata de que e o UNICO" \
       "destino vem de verify_binding_exists, ja rodada na fase de verificacao)"

  local budget=$((CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES * CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP))
  echo "    aguardando o TTL (${CUSTODIA_RETRY_TTL_MS}ms) com folga, ate ${budget}s, pela" \
       "fila-sonda '${sonda}' (bindada so a custodia.retry.dlx) receber o marcador —" \
       "PADROES §10.31: completude x limite"

  # `count:10` + `any(.[]; .payload == $marker)`, NAO `count:1` + `.[0].payload`:
  # `reject_requeue_true` numa fila CLASSIC devolve a mensagem para a CABECA (nao
  # reordena) — entao `count:1` sempre rele a MESMA cabeca em toda iteracao do
  # laco. Se uma mensagem de uma EXECUCAO ANTERIOR abortada (que morreu entre
  # publicar no custodia.retry.in e limpar) ainda estiver na `custodia.retry` e
  # expirar DURANTE a janela desta execucao, ela chega a esta sonda (o fanout de
  # custodia.retry.dlx entrega para quem estiver bindado, nao so para quem
  # publicou) ANTES da nossa, e fica na cabeca — o laco com `count:1` nunca
  # avancaria para ver o NOSSO marcador atras dela, e reprovaria (EXIT_RETRY_FALHOU)
  # um deploy sadio. `sweep_orphan_sondas` nao cobre este caso: o lixo esta na
  # PROPRIA custodia.retry, nao numa sonda. Espiar ate 10 mensagens e checar se
  # ALGUMA delas e o nosso marcador resolve sem se importar com a ordem.
  local achou="nao" achado="" tentativa
  for tentativa in $(seq 1 "$CUSTODIA_RABBITMQ_RETRY_WAIT_TRIES"); do
    local peek_body
    peek_body=$(jqn --argjson count 10 --arg ackmode "reject_requeue_true" \
      '{count:$count, ackmode:$ackmode, encoding:"auto", truncate:50000}')
    mgmt_post "/api/queues/%2F/${sonda}/get" "$peek_body"
    if [ "$MGMT_STATUS" = "200" ]; then
      local n
      n=$(jqf "$MGMT_BODY" 'length')
      if [ "$n" != "0" ]; then
        local tem_marcador
        tem_marcador=$(jqf "$MGMT_BODY" --arg m "$marker" 'any(.[]; .payload == $m)')
        if [ "$tem_marcador" = "true" ]; then
          achou="sim"
          achado="$marker"
          break
        fi
        achado=$(jqf "$MGMT_BODY" '.[0].payload')
      fi
    fi
    sleep "$CUSTODIA_RABBITMQ_RETRY_WAIT_SLEEP"
  done

  # Limpeza da sonda: SEMPRE, passe ou falhe a prova — ela e inteiramente nossa e
  # descartavel (DELETE remove a fila e qualquer conteudo junto). Best-effort: o
  # `x-expires` acima ja e a rede de seguranca se isto falhar.
  mgmt_delete "/api/queues/%2F/${sonda}" || true

  if [ "$achou" != "sim" ]; then
    echo "ERRO: PARADA POR LIMITE — a fila-sonda nao recebeu o marcador dentro de" >&2
    echo "      ${budget}s apos o TTL declarado de ${CUSTODIA_RETRY_TTL_MS}ms (ultimo" >&2
    echo "      payload visto: '${achado:-<fila vazia>}'). Ou o TTL nao expirou a" >&2
    echo "      tempo, ou 'custodia.retry.dlx' nao esta entregando." >&2
    exit "$EXIT_RETRY_FALHOU"
  fi
  echo "    PARADA POR COMPLETUDE — a fila-sonda recebeu o marcador: o dead-letter" \
       "chegou de verdade (custodia.retry.dlx e FANOUT, entrega a TODAS as filas" \
       "bindadas — inclusive custodia.prices, na MESMA operacao atomica do broker)"

  # REFORCO, nao reprova: a mesma metade sujeita a corrida contra
  # collect_statistics_interval=60000 que motivou trocar a tecnica. O veredito ja
  # foi dado pela sonda; se a estatistica de custodia.retry ainda nao acompanhou,
  # so avisamos.
  local retry_final rc
  retry_final=$(wait_for_messages_ready "custodia.retry" "$retry_antes" eq 5 3) && rc=0 || rc=$?
  if [ "$rc" -eq 0 ]; then
    echo "    reforco confere: 'custodia.retry' esvaziou de volta a ${retry_antes}"
  else
    echo "AVISO: reforco nao confirmado — 'custodia.retry' nao esta em ${retry_antes}" >&2
    echo "       (ultimo valor: ${retry_final:-desconhecido}, rc=${rc}). NAO reprova:" >&2
    echo "       o veredito desta prova e o marcador na sonda, ja confirmado acima." >&2
  fi

  # A mensagem TAMBEM chegou na custodia.prices real (fanout entrega a TODAS as
  # bindadas). Mesma regra de limpeza que ja existe para a prova de fumaca: so
  # remove se a fila tiver EXATAMENTE 1 mensagem agora — com backlog, fica retida
  # (ver o comentario de maybe_take_probe_prices sobre "ate duas por deploy").
  fetch_queue "custodia.prices"
  local prices_agora
  prices_agora=$(read_messages_ready "custodia.prices" "$MGMT_BODY")
  maybe_take_probe_prices "custodia.prices" "$marker" "$prices_agora" || true
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
  sweep_orphan_sondas
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
  assert_no_poisoning_policy "custodia.prices" "$MGMT_BODY"

  fetch_queue "custodia.prices.dlq"
  assert_type_durable "custodia.prices.dlq" "$MGMT_BODY" "quorum"
  assert_arg "custodia.prices.dlq" "$MGMT_BODY" "x-delivery-limit" "-1"
  assert_arg_effective "custodia.prices.dlq" "$MGMT_BODY" "delivery_limit" "unlimited"
  assert_no_poisoning_policy "custodia.prices.dlq" "$MGMT_BODY"

  fetch_queue "custodia.parked"
  assert_type_durable "custodia.parked" "$MGMT_BODY" "quorum"
  assert_arg "custodia.parked" "$MGMT_BODY" "x-delivery-limit" "-1"
  assert_arg_effective "custodia.parked" "$MGMT_BODY" "delivery_limit" "unlimited"
  assert_no_poisoning_policy "custodia.parked" "$MGMT_BODY"

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
  assert_no_poisoning_policy "custodia.retry" "$MGMT_BODY"

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
