# docs — Custódia

Este diretório é ponteiro, não fonte. O que é canônico mora fora daqui.

## Onde está cada coisa

| O quê | Onde | Observação |
|---|---|---|
| Desenho da Custódia | `../plataforma-docs/ARQUITETURA.md` §7 | modelo de dados (§7.1), identidade e onboarding (§7.2), worker de snapshots (§7.4) |
| Ordem de implementação | `../plataforma-docs/ARQUITETURA.md` §9 | os itens **3, 4 e 5** são desta casa |
| As 12 ADRs | `../plataforma-docs/ARQUITETURA.md` §10 | também no grafo MCP `memoria`, como `ADR-N · <título>` |
| Padrões de código | `../PADROES.md` (raiz deste repo) | a §10 é aprendida por incidente; leia antes de criar estrutura nova |
| Critério de pronto e erros de condução | `../LEIA-ME-KIT.md` (raiz) | inclusive "O que o F1 tem que alcançar" |
| Fases e critério de pronto por fase | `ROADMAP.md` (neste diretório) | nove fases, F1 a F9; abra-o antes de qualquer código |

**Nada sincroniza automaticamente.** Se a busca por `ADR` no grafo vier vazia, ou se a §10 da
ARQUITETURA tiver ADRs que o grafo não tem, recarregue a partir dela — não conclua que não há
decisões registradas.

## O `ROADMAP.md`, e por que ele veio antes do código

Ele é a **primeira entrega** deste repo: traduz os itens 3, 4 e 5 da §9 em nove fases com
critério de pronto verificável, no formato do `../operacoes/docs/ROADMAP.md`. Nenhuma fase
está fechada.

Isso é regra, não preferência. O `LEIA-ME-KIT.md` registra que uma etapa do `hub` quase saiu pela
metade porque a fila de tarefas só existia no contexto de uma sessão e nunca foi commitada: **a
próxima sessão lê o escopo do repo, não reconstrói do contexto.**

Ele é mais denso que o do `operacoes`, e isso é deliberado: **não existe molde de consumidor em
lugar nenhum da plataforma** — `hub-precos` e `operacoes` publicam, nenhum dos dois tem
`basic.consume`. Quatro desenhos inéditos são decididos ali, com a alternativa rejeitada escrita,
em vez de deixados para a fase que esbarrar neles: evento fora de ordem, mensagem-veneno,
a convenção de `ref_externa` e o calendário de dias úteis. Todos aterrissam numa tabela
append-only com trigger, onde descobrir depois custa migration.

Duas coisas que nasceram no roadmap **não** ficaram nele, porque o `CLAUDE.md` manda o contrário:
o procedimento de publicar alerta na nuvem foi para o `LEIA-ME-KIT.md`, e a doutrina das cinco
listas de configuração virou `PADROES.md` §10.33. São esses dois os arquivos que o próximo repo
lê — o roadmap não é.

## Estado do repo

Esqueleto de scaffolding copiado e adaptado do `../operacoes` em 2026-09-07. **Ainda não há código
de negócio, nem `.sln`, nem projetos** — o CI vai falhar até o F1, e isso é esperado.

Os limites de recurso no `docker-compose.prod.yml` foram **medidos** na VPS nesta data (1 núcleo,
1967 MB totais, 859 MB disponíveis), não herdados por cópia. O alias DNS `custodia-app` estava
livre; confirme com `docker ps` antes do primeiro deploy, porque a lista envelhece — colisão de
alias já derrubou um vizinho em produção (`PADROES.md` §10.1).
