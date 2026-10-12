# Resumo — Zone 5

Handoff para a próxima sessão. Leia este arquivo primeiro. Atualizado em 12/10/2026, depois da z0.1.0.

## Estado

- **Versão:** z0.1.0. A série z começou em 11/10/2026; a série antiga, v0.0.x, terminou na v0.0.15.
- **Unity:** 6000.5.3f1, com URP 17.5 e o Input System novo.
- **Cenas:**
  - `Tela de Entrada`: ainda vazia.
  - `Batalha`: era a `SampleScene`.
  - A partida passa de uma para a outra pelo `PartidaConfig` (8 slots, `teamId = slotId/4`, `playerId = slotId+1`).
- **Jogo:** a partida em hotseat ainda roda no fluxo antigo (`TurnManager` + `MatchControllerMvp`). As cartas de stance e direção só disparam eventos visuais.
- **A descoberta que organiza o que vem:** a FSM nova é **centrada no tabuleiro**, e o jogador é só um contribuinte. O desenho completo, com todas as regras decididas, está em `docs/fsm/fsm_tabuleiro.md`. Ele é a **fonte da verdade**; `docs/outros/` está defasado em 6 meses.
- **Refactor:** a FSM do tabuleiro substitui a parte de FSM do `REFACTOR_PLAN.md` (0.2 e 3.3). Nenhuma fase foi implementada.

## Onde eu parei

1. **Confirmar no Editor**, porque nada da z0.0.2 nem da z0.1.0 foi aberto no Unity:
   - o Console sem erros;
   - Build Settings com `Tela de Entrada` (índice 0) e `Batalha`, já que ainda aponta para a `SampleScene` apagada;
   - `PartidaConfigDebugSeed` na Batalha: devem aparecer 2 F-14 (P1 e P2) contra 1 MiG-29 (P5).
2. **Próximo código: o esqueleto da FSM do tabuleiro.** Os passos, o `k/N`, as intenções de qualquer origem, a IA provisória de escolha fixa e o modo solo contra IA para testar. Seguir os requisitos de "Transições à prova de reentrada" no doc.
3. **`PartidaConfig`:** falta o tipo de controle do slot (`Human/Local`, `Human/Remote`, `AI`, como o `isAI`/`isLocal` do Map Room). O `Validate` precisa recusar o hotseat híbrido.
4. **Tela de Entrada:** o `panel_players` (slotID, time, callsign) vai ser desenhado pelo autor. O botão Iniciar chama `PartidaConfig.Set` e depois `LoadScene("Batalha")`.
5. **Divergências do código com as regras**, listadas no fim do doc da FSM. As mais graves:
   - 0 testes de esquiva = falha automática (invertido);
   - a esquiva lê o `ManeuverCatalog` legado;
   - o alvo do míssil é escolhido pela distância ao atirador, e não pela ordem na trajetória.
6. **Catálogo de manobras:**
   - faltam os códigos A e as acrobacias (Looping, Immelmann, Tesoura, Parafuso);
   - os 7G estão com `gForce: 3`;
   - fica para as ferramentas de manutenção da base.
7. **Fora do commit, a pedido do autor:**
   - a pasta `Assets/acervo/.../Verde/Acrobacias/`, com cópias das cartas;
   - o `Verde/Nova pasta.meta`, órfão;
   - o `Assets/img/logo/Kit de Interface Aérea Cinematográfica.png`.

## Armadilhas

| armadilha | o que fazer |
|---|---|
| `git add -A` com o Unity aberto captura trabalho paralelo do autor | Seguir a skill `fechamento-do-dia`: stage arquivo por arquivo e `git add .` só para churn. |
| As tags `v0.0.1` a `v0.0.15` já existem | Usar só a série `z`. |
| Apagar um script sem procurar referências quebra a compilação (aconteceu com o `ButtonSelectionGroup`) | Antes de apagar, procurar o nome da classe em `Assets/Scripts` e `Assets/Editor`. |
| Abrir o projeto num Unity mais novo migra `ProjectSettings/*.asset` e o URP Global Settings | Tratar como frente "engine" no fechamento, não como churn. |
| Os PSDs das cartas (`Assets/acervo/cartas`, ~147 MB) vão pelo LFS | O push pode ser pesado. |
| Não dá para compilar pelo Unity sem o Editor | `dotnet build` numa cópia temporária do `Assembly-CSharp.csproj`, com os `<Compile Include>` dos scripts novos e a saída fora do repo (detalhes no CLAUDE.md). |
| Os docs de `docs/outros/` contradizem as regras atuais (6 mísseis, alvo declarado, 1 HP) | Valem as regras de `docs/fsm/fsm_tabuleiro.md`. |
| Renomear pasta pelo Explorer deixa `.meta` órfão (o "Nova pasta.meta") | Renomear assets e pastas sempre pelo Unity. |
| Ler as regras como fórmula quando são tabela (o combustível é o **degrau** da velocidade, não velocidade ÷ 0,3 − 1) | Perguntar ao autor de onde veio o número antes de generalizar. |
