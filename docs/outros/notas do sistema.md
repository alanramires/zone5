Notas Do Sistema (Assets/Scripts)
Data: 16/01/2025
Versão do Git: 0.0.10

Game
- Assets/Scripts/GameEnum.cs: enums e constantes globais (TurnState, WeaponType) + cores de time usadas por UI e units.
- Assets/Scripts/Game/TurnStateManager.cs: espelho de estado do TurnManager; emite OnStateChanged e observa TurnManager.
- Assets/Scripts/Game/TurnSheet.cs: dados do turno (rows, ready flags); aplicado pelo TurnManager e lido por UI/MatchController.
- Assets/Scripts/Game/TurnManager.cs: maquina de estados do turno; registra AircraftUnit, aplica defaults, avanca fases; emite eventos.
- Assets/Scripts/Game/MvpRules.cs: sanitiza input MVP (manobra/arma) e define flags MVP.
- Assets/Scripts/Game/MatchControllerMvp.cs: orquestra match (move avioes, trilhas, colisao, misseis, pontuacao); usa TurnManager, TrailManager, MissileManager, ManeuverManager, MovementCore.

Profiles
- Assets/Scripts/Profiles/UnitProfile.cs: dados de aeronave (sprite, HP, fuel, collider, token length).
- Assets/Scripts/Profiles/MissileProfile.cs: dados de missil (range, dano, sprites, alias por time).
- Assets/Scripts/Profiles/MissilePathProfile.cs: path do missil (pointsNorm + end heading), usado por MissilePathDatabase/Manager.
- Assets/Scripts/Profiles/MissilePathDatabase.cs: resolve path por id/aliases com default; usado por MissileManager.
- Assets/Scripts/Profiles/ManeuverProfile.cs: dados da manobra (stats, movimento, path build); usado por ManeuverManager e preview.
- Assets/Scripts/Profiles/ManeuverProfileCatalog.cs: resolve profiles via Resources (fallback).
- Assets/Scripts/Profiles/ManeuverDatabase.cs: banco de manobras (id/aliases + default).

Units
- Assets/Scripts/Units/AircraftUnit.cs: runtime do aviao (ids, HP, anchors, trails, damage); usado por controllers e views.
- Assets/Scripts/Units/AircraftView.cs: anima path e orientacao do aviao, desenha trail ao vivo; usado pelo MatchController.
- Assets/Scripts/Units/MissileUnit.cs: runtime do missil (anchors, trails, ids); usado pelo MissileManager/MissileView.
- Assets/Scripts/Units/MissileView.cs: anima path do missil e desenha trail progressiva; usado pelo MissileManager.
- Assets/Scripts/Units/UnitSpawner.cs: spawna aeronaves e forma layout; usa UnitProfile, MovementCore, TrailManager, TurnManager.
- Assets/Scripts/Units/PilotNameHUD.cs: label sobre o aviao; usa AircraftUnit + cores do GameEnum.

Systems
- Assets/Scripts/Systems/CameraManager.cs: pan/zoom da camera (mouse).
- Assets/Scripts/Systems/TrailManager.cs: cria segmentos de trilha para aeronaves.
- Assets/Scripts/Systems/MovementCore.cs: utilitarios de movimento (rotate, align, magnet).
- Assets/Scripts/Systems/CollisionSystem.cs: geometria 2D (segmento/segmento, distancia).
- Assets/Scripts/Systems/MissileManager.cs: spawn/animacao de misseis, resolve path (DB/catalog), colisao com avioes, debug trails; usa MissileView, TrailManager, DebugManager.
- Assets/Scripts/Systems/ManeuverManager.cs: resolve manobras via ManeuverDatabase, fallback no catalogo de profiles.
- Assets/Scripts/Systems/DebugManeuverController.cs: painel/debug de manobras fora do turno; usa ManeuverManager, MatchController, TrailManager, MissileManager, ManeuverCatalog fallback.

Legacy Catalogs
- Assets/Scripts/Game/Missile/MissilePathDef.cs: definicao legacy de path de missil.
- Assets/Scripts/Game/Missile/MissilePathCatalog.cs: resolve path legacy por aliases.
- Assets/Scripts/Game/Maneuvers/ManeuverDef.cs: definicao legacy de manobra.
- Assets/Scripts/Game/Maneuvers/ManeuverCatalog.cs: resolve manobras legacy por aliases.

Debug Views
- Assets/Scripts/Debug/MissileConeDebugView.cs: desenha cone de mira do missil (mesh + hatch).
- Assets/Scripts/Debug/MissileAimRectDebugView.cs: desenha retangulo de mira/alcance do missil.
- Assets/Scripts/Debug/ManeuverPreviewDebugView.cs: desenha path de manobra (scene/game) usando BuildWorldPoints.
- Assets/Scripts/Game/Debug/DebugManager.cs: toggles globais de debug; controla hitbox/aim/trail debug.
- Assets/Scripts/Game/Debug/CollisionDebugView.cs: sprite de colisao (raio), ligado pelo DebugManager.
- Assets/Scripts/Game/Debug/HitboxOverlay.cs: liga/desliga sprite de hitbox.

UI Panels
- Assets/Scripts/Panels/TurnManeuverPanel.cs: UI de input por fase (manobra/arma/missil); chama MatchControllerMvp e TurnManager.
- Assets/Scripts/Panels/PilotRowView.cs: item de UI para listar pilotos no painel tactical.
- Assets/Scripts/Panels/Panel Tactical.cs: painel de status do turno + lista de pilotos; observa TurnManager/TurnStateManager.
