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
| Fases e critério de pronto por fase | `ROADMAP.md` (ainda **não existe**) | é a primeira entrega deste repo |

**Nada sincroniza automaticamente.** Se a busca por `ADR` no grafo vier vazia, ou se a §10 da
ARQUITETURA tiver ADRs que o grafo não tem, recarregue a partir dela — não conclua que não há
decisões registradas.

## O que ainda não existe

O `ROADMAP.md` deste repo. Ele é a **primeira entrega**, antes de qualquer código: traduz os itens
3, 4 e 5 da §9 em fases com critério de pronto verificável, no formato do
`../operacoes/docs/ROADMAP.md`.

Isso é regra, não preferência. O `LEIA-ME-KIT.md` registra que uma etapa do `hub` quase saiu pela
metade porque a fila de tarefas só existia no contexto de uma sessão e nunca foi commitada: **a
próxima sessão lê o escopo do repo, não reconstrói do contexto.**

## Estado do repo

Esqueleto de scaffolding copiado e adaptado do `../operacoes` em 2026-09-07. **Ainda não há código
de negócio, nem `.sln`, nem projetos** — o CI vai falhar até o F1, e isso é esperado.

Os limites de recurso no `docker-compose.prod.yml` foram **medidos** na VPS nesta data (1 núcleo,
1967 MB totais, 859 MB disponíveis), não herdados por cópia. O alias DNS `custodia-app` estava
livre; confirme com `docker ps` antes do primeiro deploy, porque a lista envelhece — colisão de
alias já derrubou um vizinho em produção (`PADROES.md` §10.1).
