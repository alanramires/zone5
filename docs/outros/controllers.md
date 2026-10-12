Controllers And Panels

Overview
- MatchControllerMvp: fluxo oficial da partida; move unidades, resolve colisao e ataques; depende de TurnManager, TrailManager, MissileManager e ManeuverManager.
- TurnManager: controla fases do turno e sincroniza com UI.
- MissileManager: spawn e resolve misseis; usa MissilePathDatabase.
- ManeuverManager: resolve manobras via ManeuverDatabase (fallback para ManeuverProfileCatalog).
- TrailManager: desenha rastros (line renderers) para avioes e misseis.
- UnitSpawner: instancia unidades e posiciona no inicio.
- DebugManager: toggles globais de debug (hitboxes, cones etc).

Debug And Legacy
- DebugManeuverController: painel/fluxo de debug para mover 2 avioes fora do turno e disparar misseis (antes: ManeuverTrainController).
- TurnManeuverPanel: painel oficial de input por piloto/fase (antes: PanelManeuver).

Notes
- O "last end" oficial dos avioes fica no MatchControllerMvp. Controladores de debug devem sincronizar com ele.
