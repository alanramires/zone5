# Changelog

## 0.0.9 - 0.0.9 Scanner de alvo e identificacao do aviao

- HUD de piloto no prefab (callsign + cor do time)
- MissilePathProfile/Database com aliases e preview no editor
- Debug: Missile Aim Cone visivel no Game/Scene com hatch e escala em FU
- MissileManager usando profiles com fallback no catalogo antigo
- ManeuverProfile com DB/Manager, resolver com fallback no catalogo legado
- Preview de manobras no Editor com arco fisico + controle Bezier e nodes de debug
- Heading final por curva (sem override manual) e ajuste de animacao do AircraftView
- Sincronizacao de lastEnd entre debug e fluxo oficial

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
