Relatorio de funcionamento do combate

Visao geral
- O combate e por turnos e segue uma sequencia de fases controladas pelo TurnManager.
- As escolhas dos jogadores (manobra, arma, perfil de missil) sao coletadas e o sistema avanca quando todos os pilotos vivos estao prontos.

Fases do turno (fluxo principal)
1) SelectManeuver
   - Jogadores escolhem manobra.
   - Sistema limpa misseis antigos e zera os pontos temporarios do turno anterior.

2) DeclareWeapon
   - Jogadores declaram arma (M para missil, X para nada).

3) RevealAndMoveFighters
   - Sistema executa as manobras e move as aeronaves.

4) ResolveCollisions
   - Sistema verifica colisao entre aeronaves usando raio de colisao atual (sem colisao por rastro).
   - Se houver colisao, espera um delay curto e marca as aeronaves como abatidas.

5) SelectMissileProfile
   - Somente quem declarou missil escolhe o perfil do missil.

6) SpawnMissilesAndResolveEvasion
   - O sistema instancia TODOS os misseis ao mesmo tempo.
   - Em seguida, verifica quais misseis acertaram seus alvos.
   - Cada acerto e resolvido um a um:
     - Rola a evasao do alvo.
     - Cada falha no dado gera 1 ponto temporario para o agressor (por alvo atingido).
     - O dano e aplicado sem destruir imediatamente o objeto.
   - Se houver misseis na rodada, aguarda 10s para inspecao e depois remove misseis e aeronaves abatidas.

7) ApplyDamageAndCheckVictory
   - Se nao houve fase de missil, o sistema calcula mortes e pontuacao aqui.

8) EndRoundAndAdvance
   - Avanca para a proxima rodada.

9) MatchEnded (quando houver vencedor ou empate)
   - O turno nao avanca mais.
   - O painel mostra "Team X wins" ou "Draw".

Pontuacao (temporarios e resolucao)
- Cada falha no dado de evasao gera 1 ponto temporario no alvo para o agressor.
- Pontos temporarios sao limpos no inicio de cada rodada.
- Somente quando o alvo morre, a pontuacao e calculada:
  - Quem tiver mais pontos temporarios no alvo ganha 1 abate.
  - Em caso de empate, todos os empatados ganham 1 abate.
  - Os demais participantes ganham assistencia.

Logs relevantes
- [Hit Check]: registra cada acerto detectado.
- [TEMP SCORE]: registra pontos temporarios por falhas.
- [TEMP RESOLUTION]: registra a resolucao final de pontos ao morrer o alvo.
- [TEMP CLEAR]: registra a limpeza dos pontos temporarios no inicio da rodada.
- [SCORE]: mostra a pontuacao total por piloto.

Observacoes
- Nao existe escolha de alvo; o sistema detecta automaticamente o primeiro alvo valido no caminho do missil.
- Colisao entre aeronaves usa raio de colisao atual, nao a trajetoria completa.
