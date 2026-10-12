# Resumo — Zone 5

Handoff para a próxima sessão. Leia este arquivo primeiro. Atualizado em 11/10/2026, depois do z0.0.2.

## Estado

- **Versão:** z0.0.2. A série z começou em 11/10/2026; a série antiga, v0.0.x, terminou na v0.0.15 em 06/02/2026.
- **Unity:** 6000.5.3f1, com URP 17.5 e o Input System novo.
- **Jogo:** a partida em hotseat roda no fluxo antigo, em que a manobra é escolhida por código. As cartas de stance e direção (estilo Ticket to Ride) estão na tela, mas **não selecionam manobra**: só disparam eventos visuais.
- **Refactor:** nenhuma fase do `REFACTOR_PLAN.md` foi iniciada.

## Onde eu parei

- **Ainda não confirmado no Editor depois do z0.0.2:**
  - se o projeto compila sem erros no Console;
  - se uma partida roda até o fim;
  - se o `PanelGroupController` perdeu alguma referência na cena.
- **Decisão pendente:** ir direto para a Fase 3 (ligar as cartas ao `ManeuverProfile`, o que deixa o jogo jogável com a UI nova) ou fazer antes a Fase 0 (interfaces e FSM em pilha).
  - Arquivos da Fase 3: `Assets/Scripts/Panels/PanelStanceController.cs` (`OnStanceChanged`), `PanelDirectionsController.cs` (`ApplyStance`) e `PanelStanceCardClick.cs`.
  - A entrada da escolha no jogo é `MatchControllerMvp.SubmitManeuver` / `SubmitManeuverList`.
- **Ganhos rápidos** apontados em `docs/pendencias/avaliacao_fsm.md`:
  - as esperas fixas somam cerca de 22 s por turno; o pior caso é a espera de 10 s na fase de ataques, que nem é configurável;
  - o painel tático reconstrói a lista inteira a cada evento.

## Armadilhas

| armadilha | o que fazer |
|---|---|
| `git add -A` com o Unity aberto captura trabalho paralelo do autor, como upgrades de versão ou correções em andamento | Seguir a skill `fechamento-do-dia`: stage arquivo por arquivo e `git add .` só para churn. |
| As tags `v0.0.1` a `v0.0.15` já existem | Usar só a série `z`. |
| Apagar um script sem procurar referências quebra a compilação. O `ButtonSelectionGroup` foi apagado, mas o `PanelGroupController` ainda o usava | Antes de apagar um script, procurar o nome da classe em `Assets/Scripts` e `Assets/Editor`. |
| Abrir o projeto num Unity mais novo migra `ProjectSettings/*.asset` e o URP Global Settings sozinho | Tratar como frente "engine" no fechamento, não como churn. |
| Os PSDs das cartas (`Assets/acervo/cartas`, ~147 MB) vão pelo LFS | O push é pesado; o do z0.0.2 enviou 172 MB. |
