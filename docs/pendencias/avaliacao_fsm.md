# Avaliação da máquina de estados do Zone 5

Data: 11/10/2026.

## Escopo e limites

Investigação estática do código em `Assets/Scripts` e da configuração de `Assets/Scenes/SampleScene.unity`, cena habilitada em `EditorBuildSettings`. Não foram feitas alterações na implementação nem capturas do Unity Profiler. Os custos de CPU, memória e renderização descritos são candidatos a gargalos, não medições de desempenho.

Os principais pontos encontrados são esperas programadas no fluxo, reconstrução repetida da interface, alocações dos rastros durante a animação e transições aninhadas. A análise não indica que uma ferramenta do editor desatualizada seja a causa desses problemas.

## Organização atual

- `TurnManager` mantém a fase em `TurnSheet.phase`, verifica prontidão e executa transições.
- `MatchControllerMvp` reage a `OnStateChanged`, executa movimento, colisões e ataques e solicita novos avanços.
- `TurnStateManager` replica a fase e publica outro evento de mudança de estado.
- Os painéis escutam eventos de estado e de alteração da planilha; alguns também escutam o estado replicado.

O fluxo principal é:

`SelectManeuver → DeclareWeapon → RevealAndMoveFighters → ResolveCollisions → [SelectMissileProfile → SpawnMissilesAndResolveEvasion] → ApplyDamageAndCheckVictory → EndRoundAndAdvance → SelectManeuver`.

O trecho entre colchetes depende de existir declaração de míssil por um jogador vivo. A verificação de vitória pode levar a `MatchEnded`.

## Achados

### 1. Esperas programadas dominam a duração percebida do turno

**Evidência:** `MatchControllerMvp.cs:180–187`, `247–279`, `448–456`; `TurnManager.cs:212–221`; `SampleScene.unity:3846–3848` e `8804–8805`.

A cena configura animação de movimento de 2 segundos e `endRoundDelaySeconds` de 5 segundos. A resolução de colisões reutiliza esse mesmo parâmetro quando há vítimas. A fase de ataques espera 10 segundos fixos se pelo menos uma transação de míssil foi criada, mesmo sem acerto.

Um turno que passe por movimento, colisão, criação de míssil e encerramento, sem terminar a partida, pode acumular aproximadamente 22 segundos dessas esperas, além do processamento e da entrada dos jogadores. As esperas cedem execução pela corrotina; essa demora não demonstra saturação de CPU.

**Recomendação:** separar duração da animação, apresentação da colisão, apresentação dos ataques e encerramento. Tornar os 10 segundos configuráveis e vincular o avanço à conclusão efetiva da apresentação quando necessário.

### 2. O painel tático reconstrói toda a lista a cada atualização

**Evidência:** `Assets/Scripts/Panels/Panel Tactical.cs:48–174`.

O painel escuta `TurnManager.OnStateChanged`, `TurnManager.OnSheetChanged` e `TurnStateManager.OnStateChanged`. Todos levam a `RefreshFromTurnManager`, que busca aeronaves na cena, cria um dicionário, copia e ordena as linhas, destrói os objetos de UI existentes e instancia novos objetos para todos os pilotos.

Uma mudança de fase pode atualizar o painel pelo evento original e pelo evento replicado. Alterações da planilha também reconstruem a lista, incluindo registro e mudança de status de cada unidade. Isso concentra criação e destruição de objetos nas confirmações e transições, com potencial custo de layout, Canvas e coleta de lixo.

**Recomendação:** manter as linhas em um mapa por `playerId`, atualizar apenas os valores alterados e criar ou remover linhas somente quando a composição mudar. Consolidar notificações para evitar reconstruções repetidas na mesma transição.

### 3. Os rastros alocam e reprocessam dados durante a animação

**Evidência:** `Assets/Scripts/Units/AircraftView.cs:161–171`, `612–621` e `824–866`.

O caminho de atualização do rastro duplo reescreve todos os pontos visíveis de ambos os `LineRenderer`. `ApplyTrailWidthCurve` cria um array de `Keyframe` e uma `AnimationCurve`; no rastro duplo é chamada para cada lado. O custo cresce com a quantidade de aeronaves animadas e de pontos visíveis.

Esse é um candidato a engasgos durante `RevealAndMoveFighters`, principalmente por alocações recorrentes e envio repetido dos pontos ao renderer. A importância real depende do modo de rastro, da deformação e da densidade dos caminhos usados.

**Recomendação:** reutilizar buffers e curvas quando viável, atualizar somente os pontos ou trechos alterados e evitar reconstruir curvas sem mudança relevante. Medir `GC Alloc` e tempo da animação antes e depois.

### 4. Buscas globais e alocações repetidas na resolução

**Evidência:** `MatchControllerMvp.cs:199–205`, `357–366` e `393–399`; `Assets/Scripts/Systems/MissileManager.cs:257–286` e `583–587`.

`FindUnitByPlayerId` busca todas as aeronaves a cada chamada. É usado dentro dos loops de movimento e seleção dos atiradores. Cada míssil volta a buscar aeronaves e cria uma lista de alvos usando LINQ e ordenação por distância. O `DebugManager` também é procurado repetidamente, inclusive dentro do loop de alvos e do teste de trajetória.

Com vários jogadores e mísseis, essas operações acumulam trabalho síncrono no início das fases de resolução. O teste de trajetória ainda percorre segmentos para cada alvo testado.

**Recomendação:** manter registro de aeronaves por `playerId`, obter um conjunto estável de candidatos para a resolução e guardar referências dos managers. Preservar a regra atual de prioridade dos alvos ao otimizar a ordenação.

### 5. Transições aninhadas e corrotinas sem validação de fase

**Evidência:** `TurnManager.cs:111–130`, `188–228` e `260–264`; `MatchControllerMvp.cs:171–175`, `180–187`, `247–279` e `383–466`.

Ao receber `ApplyDamageAndCheckVictory`, o controlador chama `AdvanceButton()` dentro da execução de `OnStateChanged`. Assim, uma nova transição pode ocorrer antes de terminar a notificação anterior. Como o evento não transporta a fase e os ouvintes consultam `sheet.phase`, os ouvintes seguintes podem observar uma fase posterior à que iniciou a notificação.

Se o atraso de encerramento for zero, `EnterState(EndRoundAndAdvance)` também chama `NextState()` antes de publicar seu evento, podendo gerar notificações repetidas do estado seguinte.

As corrotinas avançam a fase vigente ao terminar, sem conferir se ainda pertencem à mesma fase e ao mesmo turno. `AdvanceButton` também não verifica se uma rotina de resolução está em andamento. Uma chamada adicional ou uma mudança externa de fase pode permitir que uma rotina antiga avance o fluxo errado.

**Impacto:** risco de atualizações duplicadas, reinicialização da interface e avanços indevidos. Trata-se sobretudo de integridade do fluxo; não há reprodução em execução nesta avaliação.

**Recomendação:** serializar solicitações de transição, publicar a fase como argumento do evento e validar fase, turno e identidade da execução antes de concluir uma corrotina. Separar confirmação de entrada do jogador de conclusão de uma fase automática.

### 6. Comparação inconsistente na lista de seleção de mísseis

**Evidência:** `Assets/Scripts/Panels/TurnManeuverPanel.cs:63–77` e `143–174`.

Na fase `SelectMissileProfile`, `pendingUnits` inclui somente aeronaves que declararam míssil. Entretanto, `HandleSheetChanged` compara seu tamanho com o total de aeronaves vivas. Quando apenas parte dos pilotos declara míssil, a diferença de tamanho dispara reconstruções desnecessárias da lista.

`SetupRound` também redefine o índice; `RefreshListStayingSafe` tenta restaurar o piloto atual e chama novamente a apresentação. Isso acrescenta buscas, ordenação e atualizações de UI em cada notificação relevante.

**Recomendação:** comparar o conjunto de participantes elegíveis para a fase, usando o mesmo filtro aplicado na montagem da lista, e reconstruir somente quando esse conjunto mudar.

### 7. Colisões entre aeronaves crescem quadraticamente

**Evidência:** `MatchControllerMvp.cs:303–354`.

A resolução ativa compara cada par de aeronaves vivas. `BoundsIntersect` procura `Collider2D` nos filhos para cada par e, no fallback, procura `SpriteRenderer`. O número de pares é `N × (N − 1) / 2`.

Isso provavelmente tem menor prioridade com poucas aeronaves, mas pode pesar ao aumentar a escala. O método de colisão entre trajetórias também existe, porém não é chamado pela rotina de resolução analisada; seu custo não deve ser atribuído ao fluxo ativo.

**Recomendação:** guardar referências dos colliders e renderers. Considerar filtragem espacial somente se uma medição com a quantidade esperada de aeronaves justificar.

## Ordem recomendada de intervenção

1. Proteger as transições e a conclusão das corrotinas para garantir um fluxo previsível.
2. Reutilizar as linhas do painel tático e consolidar suas atualizações.
3. Reduzir alocações e reescrita de pontos dos rastros durante a animação.
4. Substituir buscas repetidas por registro de unidades e referências em cache.
5. Corrigir o filtro de participantes no painel de seleção de mísseis.
6. Separar e parametrizar as esperas de apresentação conforme o ritmo desejado.
7. Otimizar a comparação de colisões se o número esperado de aeronaves justificar.

## Validação em execução proposta

Capturar um turno sem mísseis, um com vários mísseis e um com colisões, usando a quantidade de aeronaves esperada para uma partida. Separar duração total da fase de tempo efetivo de CPU.

No Profiler, observar tempo e `GC Alloc` da atualização dos rastros, reconstrução do painel tático e resolução de ataques, além de layout e reconstruções do Canvas. Comparar uma captura no editor com uma em Development Build para avaliar a influência da instrumentação e dos logs.

Para a integridade do fluxo, verificar que cada fase é iniciada uma única vez, que uma rotina antiga não avança um turno novo e que o encerramento da partida impede novos avanços. Exercitar atraso de encerramento zero e positivo, somente parte dos jogadores declarando míssil e término da partida durante a resolução.

Não há números de milissegundos ou ganho percentual nesta avaliação, pois essas medições ainda não foram realizadas.
