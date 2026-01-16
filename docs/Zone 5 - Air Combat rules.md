# Zone 5 - Air Combat
Durante a guerra fria, os F-14 decolam de seus porta aviões para interceptar os Mig-29 antes que os Bombardeiros Bear se aproxime da frota...

## Visão Geral

Zone 5: Air Combat é um jogo de combate aéreo simultâneo, sem grid, baseado em planejamento secreto, leitura espacial e resolução simultânea.  
Não há compra de cartas nem RNG de iniciativa: todos os jogadores têm acesso a todas as manobras, e o limite vem de recursos, posicionamento e risco assumido.

O MVP foca em:
- combate por mísseis
- 1 HP por avião
- resolução simultânea
- times EUA x Rússia  (F-14 vs Mig-29)

Sistemas avançados (metralhadora, afterburner, dano variável) **não fazem parte do MVP**, mas
já são previstos arquiteturalmente.

---

## Componentes Conceituais

### Avião
- Cada jogador controla 1 avião
- HP (MVP): 1
- HP (Avançado): 3
- Orientação e posição importam
- Não existe altitude no MVP

### Cartas de Manobra
- Todos os jogadores têm acesso a todas as cartas
- Não há compra nem descarte
- Cada carta define:
  - trajetória
  - nível de G (define esquiva / dodge)
- No MVP: escolhe-se **1 carta**
- No Avançado: pode-se escolher **2 cartas** (afterburner)
	- *As cartas não podem ser acrobacias e no máximo 3G*

### Mísseis
- Cada avião começa com **6 mísseis**
	- AIM-54 Phoenix para F-14 americanos
	- R-27 para Mig-29 russos.
- Apenas mísseis ar-ar de longo alcance
- No MVP:
  - todos os mísseis são equivalentes em dano
  - diferem apenas na trajetória
- Cada míssil:
  - vive **exatamente 1 rodada**
  - possui trajetória própria
  - possui área de ameaça e ranhura central

---

## Setup Inicial

- O tabuleiro é um plano retangular ou quadrado, sem grid
- Os times (EUA x Rússia) começam em lados opostos
- A distância inicial entre os times é fixa:
  - equivalente a **4 comprimentos de míssil**
- Jogadores entram em **formação diamante**, conforme ordem de entrada:
  1. nariz do diamante
  2. ala esquerda
  3. ala direita
  4. centro
  5. segundo ala esquerda
  6. segundo ala direita
  - e assim por diante

---

## Estrutura de Rodada (Fluxo Oficial)

### Fase 1 — Planejamento de Manobra (Secreto)
1. Cada jogador escolhe:
   - 1 carta (MVP)
   - 1 ou 2 cartas (Avançado)
2. A escolha é secreta

> A manobra escolhida define risco, esquiva e consequências futuras.

---

### Fase 2 — Confirmação de Planejamento
3. O sistema aguarda todos os jogadores confirmarem suas cartas

---

### Fase 3 — Declaração de Armas
4. Cada jogador declara:
   - **Míssil**
   - **Nada**
   - (**Metralhadora**, apenas no sistema avançado)
5. Nenhum alvo é escolhido ainda

> Esta mudança em relação ao jogo original elimina o “gasto bobo” do 9G
> e melhora drasticamente o equilíbrio e a leitura tática do jogo.

---

### Fase 4 — Confirmação de Armas
6. O sistema aguarda todos declararem

---

### Fase 5 — Revelação e Movimento
7. Todos revelam suas cartas
8. O sistema:
   - conecta o fim da trajetória anterior
   - ao início da nova trajetória
   - desliza os aviões pelo espaço aéreo de acordo com a trajetória da carta escolhida
9. A orientação e posição final do avião é atualizada

---

### Fase 6 — Colisão de Aviões
10. O sistema aguarda todos finalizarem o movimento
11. Colisões avião × avião são verificadas **simultaneamente**

---

### Fase 7 — Seleção de Alvo e Míssil
12. Jogadores que declararam arma escolhem:
   - um alvo
   - um token/sprite de míssil
13. Limite: **1 míssil por rodada por jogador**
	1. A metralhadora utiliza metade da trajetória de um míssil reto, considerando apenas a ranhura central.

---

### Fase 8 — Confirmação de Disparo
14. O sistema aguarda todos confirmarem suas escolhas

---

### Fase 9 — Disparo e Resolução de Mísseis
15. Todos os mísseis são colocados no tabuleiro **simultaneamente**
16. Para cada possível impacto:
   - verifica-se a interseção com o avião alvo
   - aplica-se o teste de esquiva (dodge)
   - O míssil tenta atingir o alvo declarado, mas se acontecer de mais aviões estarem na trajetória do alcance máximo, ele pode atingir outro no caminho

---

### Fase 10 — Dano e Eliminações
17. Todos os danos são aplicados simultaneamente

- MVP:
  - qualquer falha no dodge → avião destruído
- Avançado:
  - míssil causa **–3 HP**
  - metralhadora causa **–1 HP por acerto**
  - falhas de dodge são acumuladas

Resultados de esquiva são **registrados** para:
- dano futuro
- degradação de manobrabilidade e velocidade por dano de metralhadora
- critério de kill / assistência

---

### Fase 11 — Encerramento
18. Aviões destruídos são removidos
19. Verifica-se condição de vitória:
   - time adversário eliminado
   - ou último sobrevivente (Battle Royale)
20. Inicia-se nova rodada

---

## Sistema de Míssil

### Vida Útil
- O míssil existe por **apenas uma rodada**
- Ao atingir o alcance máximo:
  - perde eficiência
  - não causa dano automático
- Se o avião não estiver na área de ameaça ou ranhura:
  - está salvo

---

### Detecção de Impacto

#### Modo Básico
- Se **qualquer parte do avião** estiver dentro da **área colorida**:
  - ocorre um **quase-hit**
  - exige teste de esquiva

#### Modo Avançado
- Se **qualquer parte do avião** aparecer na **ranhura central**:
  - ocorre um **quase-hit**
  - exige teste de esquiva

---

## Sistema de Esquiva (Dodge)

A esquiva não é reativa: ela é consequência direta da manobra escolhida.

### Probabilidades
- 9G → 100% esquiva (nenhum teste)
- Outras manobras:
  - 50%
  - 25%
  - 12,5%

### Testes
- Cada tentativa gera um “check”
- Falhas são registradas
- No MVP:
  - qualquer falha = destruição
- No Avançado:
  - cada falha pode gerar dano ou efeitos

---

## Princípios de Design

- Não existe iniciativa
- Não existe ordem de resolução
- Não existe RNG de movimento
- Eliminações simultâneas são válidas
- Empates são válidos
- O caos é desejado

Zone 5: Air Combat recompensa:
- leitura geométrica
- antecipação
- posicionamento
- coragem de assumir risco


---
