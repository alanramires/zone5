# Changelog

## 0.0.8 - Colisões e movimentos

- Movimento animado para aeronaves com trilha progressiva (AircraftView) e sincronismo de tempo
- Colisão de aeronaves baseada em Collider2D real (Distance/isOverlapped), com ajuste por UnitProfile
- Fim de partida como estado explícito (MatchEnded) e painel tático atualizado
- Pontuação temporária por falhas de evasão, resolução de empate e limpeza por rodada
- Debug: friendly fire toggle, trail hitbox laranja restaurado e defaults de debug desligados

## 0.0.5 - evasao, dano e colisao entre aeronaves

- Rolagem de evasao (d4) por manobra e aplicacao de dano no hit
- Colisao entre aeronaves (MVP) com morte simultanea

## 0.0.4 - debug center, colisao de misseis e hitbox tools

- DebugManager centralizado com toggles de hitboxes, trails e modos de espessura
- Debug trail separado do trail visual (laranja com ajuste em tempo real)
- Checagens de colisao do missil por caminho e no ponto final
- HitboxOverlay/CollisionDebugView simplificados para uso em runtime

## 0.0.3 - misseis: MissileProfile/Unit, MissilePathCatalog e trilha curva suave

## 0.0.2 - curvas em arco + heading final + magnet de exhaust e cameras

## 0.0.1 - primeira versao do Zone 5: posicoes iniciais e rastro
