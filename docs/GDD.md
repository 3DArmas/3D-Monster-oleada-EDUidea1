# Project Outbreak — Documento de Diseño (GDD)

> **Versión:** 1.1 · **Fecha:** 1 de octubre de 2026
> **Estado:** Fase 0 (diseño) **cerrada**. Listo para Fase 1.
> **Repo:** `3D-Monster-oleada-EDUidea1` (Unity 6000.3.9f1, URP, Input System)
> **Rama de trabajo:** `KEVIn` (repositorio compartido)

---

## 1. Concepto

FPS de supervivencia por rondas contra hordas de zombies, centrado en **movimiento rápido**, **combate**, **progresión durante la partida** y **administración de recursos**.

El jugador gana dinero eliminando enemigos y lo gasta en una **tienda física dentro del mapa** entre rondas. El objetivo de cada partida: **sobrevivir tantas rondas como sea posible**.

Frase guía del proyecto: **"Una ronda más."**

## 2. Pilares

```
MOVIMIENTO + COMBATE + PROGRESIÓN + SUPERVIVENCIA
```

El jugador debe poder sobrevivir tanto por su capacidad de combate como por su capacidad de moverse por el mapa.

## 3. Reglas de desarrollo

1. **Gameplay antes que contenido.** No crear 10 zombies si el zombie básico no es divertido.
2. **Cada fase termina en algo jugable.** Si una fase no se puede jugar, está mal cortada.
3. **Ninguna fase empieza sin criterios de aceptación** (ver `BACKLOG.md`).
4. **Si una característica no hace que jugar sea más divertido, no es prioridad.**

## 4. Bucle de juego

```
INICIAR PARTIDA → RONDA N → APARECEN ZOMBIES → MATAR (ganar dinero)
      ↑                                                  ↓
      └──── SIGUIENTE RONDA ←── LISTO ←── TIENDA ←── SOBREVIVIR LA RONDA
```

## 5. Decisiones cerradas (Fase 0)

| # | Decisión | Resolución |
|---|---|---|
| 1 | **Roguelite** | **Sí.** Mejoras permanentes, desbloqueos y camuflajes/skins comprables. **Fuera del MVP**, se añade en fases posteriores. |
| 2 | **Tienda** | **Física en el mapa**, gestionada por un personaje comerciante. El jugador pulsa **LISTO** para pasar a la siguiente ronda. **No pausa el juego. Sin temporizador** (a validar en playtest). |
| 3 | **Mapa** | **Blockout con primitivas** (cubos/plataformas) dimensionado según las habilidades. El detalle visual es futuro. |
| 4 | **Rendimiento** | Requisito de primer nivel: **60 fps a 1080p en PC de gama media**. |
| 5 | **Dificultad** | Escalado **progresivo estilo CoD Zombies**: no solo más zombies, también más fuertes, más rápidos y con más variedad. |
| 6 | **Habilidad signature** | **Slide + mantle** en el MVP. Wall run, dash y doble salto, más adelante. |
| 7 | **Zombie trepador** | Fase 4 (es lo más caro de implementar bien). |

### Tienda: implementación por etapas

- **MVP (Fase 1):** un **cubo interactuable** que se mueve/levita ligeramente, con interacción **[E]** que abre la interfaz de compra. Sin animaciones.
- **Fases 3–5:** sustituir el cubo por el **"aventurero asustado"**: aparece al terminar la ronda, monta su puesto en 2–3 s, vende y se marcha al pulsar LISTO.
- **Idea registrada — "el mercader cobarde":** si un zombie se acerca a menos de X metros mientras compras, entra en pánico, recoge y desaparece. Obliga a **limpiar la zona antes de comprar** y convierte la tienda en una decisión de riesgo. (Concepto, no MVP.)

---

## 6. Jugador

### Base (sin mejoras)

| Stat | Valor | Fase | Nota |
|---|---|---|---|
| Vida | 100 | 1 | Sin regeneración: se compra |
| Velocidad caminar | 5.0 m/s | 1 | Ya implementado |
| Velocidad correr | 7.5 m/s | 2 | ×1.5 |
| Stamina | 100 | 2 | −20/s corriendo · +15/s tras 1 s de pausa |
| Salto | 1.2 m | 1 | Ya implementado |
| Gravedad | −20 | 1 | Ya implementado |
| Agachado | 2.5 m/s | 2 | |
| Slide | impulso 9 m/s · 0.7 s · cuesta 25 stamina · 1 por salto | 2 | Evita el spam |
| Mantle | subir obstáculos de hasta 1.2 m | 2 | |

### Control (conceptual)

```
WASD movimiento · SHIFT correr · SPACE saltar · CTRL agacharse/slide
Ratón cámara · LMB disparar · RMB apuntar · R recargar · 1/2/3 cambiar arma
E interactuar (tienda)
```

## 7. Armas

| Arma | Daño | Cadencia | Cargador | Reserva | Recarga | Fase |
|---|---|---|---|---|---|---|
| Cuchillo | 50 | 1.5/s | — | — | — | 1 |
| **Pistola** | 25 | 4/s | 12 | 96 | 1.8 s | 1 |
| Escopeta | 8×12 | 1/s | 6 | 36 | 2.5 s | 3 |
| Subfusil | 18 | 10/s | 30 | 180 | 2.0 s | 3 |
| Rifle | 45 | 3/s | 20 | 120 | 2.2 s | 3 |

- **Headshot: ×4 de daño** (25 × 4 = 100 → la pistola mata de un tiro en la cabeza al zombie normal).
- Alcance de apuntado de la pistola: 100 m.
- **Bug conocido a corregir (Fase 3):** el icono del arma actual no se muestra (`weaponIcons` / `weaponIconDisplay` en `WeaponSwitcher`).

## 8. Enemigos

| Tipo | HP | Velocidad | Daño | Recompensa | Aparece | Fase |
|---|---|---|---|---|---|---|
| Zombie normal | 100 | 2.2 m/s | 15 | $100 | Ronda 1 | 1 |
| Zombie rápido | 70 | 4.5 m/s | 15 | $150 | Ronda 3 | 4 |
| Zombie trepador | 120 | 2.5 m/s | 20 | $175 | Ronda 5 | 4 |
| Zombie tanque | 500 | 1.5 m/s | 35 | $300 | Ronda 8 | 4 |
| Mini boss | 2000 | 2.0 m/s | 50 | $1000 | Rondas 5/10/15 | 5 |

**Fuera del MVP:** explosivo, saltador, blindado, invisible, mutante, rompe-obstáculos, potenciador.

## 9. Rondas y dificultad

**Cantidad de zombies por ronda** (crecimiento ≈ +30 % y después lineal):

```
R1: 8      R2: 12     R3: 16 (+rápidos)   R4: 22
R5: 28 (+trepador +mini boss)             R6+: +30 % sobre la anterior
```

**Escalado** (a partir de la ronda indicada):

| Variable | Fórmula |
|---|---|
| Vida del zombie | `HP × (1 + 0.08 × (ronda − 6))` para ronda ≥ 6 |
| Daño del zombie | `daño × (1 + 0.05 × (ronda − 1))` |
| Velocidad | `+0.05 m/s` por ronda, tope **+30 %** |
| Intervalo de spawn | `0.5 s` bajando 0.02 s por ronda desde la 5, mínimo **0.25 s** |
| Zombies simultáneos | **máximo 24** (techo por rendimiento y dificultad) |

**Fin de ronda:** cuando no queda ningún zombie vivo. Entonces se abre la tienda y **la siguiente ronda no empieza hasta que el jugador pulsa LISTO**.

**Rondas especiales** (Fase 4–5): velocidad, tanques, trepadores, boss, recompensa doble.

## 10. Economía

Recompensa por eliminación: ver tabla de enemigos (100 / 150 / 175 / 300 / 1000).

**Precios de tienda (iniciales, a ajustar en playtest):**

| Categoría | Artículo | Precio |
|---|---|---|
| Munición | Cargador de pistola (12) | $50 |
| Munición | Caja de escopeta / subfusil / rifle | $80 / $120 / $150 |
| Supervivencia | +25 HP | $500 |
| Supervivencia | Botiquín | $250 |
| Movimiento | +10 % velocidad | $600 |
| Movimiento | +15 % stamina | $500 |
| Combate | Recarga rápida | $700 |
| Objetos | Granada | $300 |
| Armas | Escopeta | $1200 |
| Armas | Subfusil | $1800 |
| Armas | Rifle | $2500 |

**Ritmo económico validado:** ronda 1 = 8 × $100 = **$800** (alcanza para +25 HP o munición). Ronda 2 = $1200 → **escopeta justa**. La curva obliga a elegir entre munición y mejora.

## 11. HUD

Minimalista. Elementos: **HP**, **stamina** (Fase 2), **dinero**, **munición / arma actual**, **ronda**, **crosshair**, y avisos centrales de "RONDA N" / "TIENDA ABIERTA".

## 12. Muerte y fin de partida

Al morir el jugador: pantalla de resumen con **ronda alcanzada**, **zombies eliminados**, **dinero ganado**, **tiempo de partida** y botón **VOLVER A JUGAR**.

Duración objetivo de partida: corta 10–15 min · normal 20–30 min · avanzada 30–60+ min.

## 13. Mapa

- **Blockout** de aproximadamente **30 × 30 m** (probar también 20 × 20).
- Debe permitir: movimiento, escape, parkour, rodear enemigos, usar obstáculos, subir a posiciones elevadas y rutas alternativas.
- Elementos del blockout: cajas, paredes, barreras, plataformas, pasillos, zonas abiertas y estrechas, puntos elevados.
- **4–6 puntos de spawn** repartidos, con NavMesh horneado.
- El mapa actual `Map_v1` (industrial, 55 props) sirve como referencia visual y se detallará en el futuro.

## 14. Rendimiento

- Objetivo: **60 fps a 1080p** en PC de gama media.
- Techo de **24–30 zombies simultáneos** + jugador.
- Medición con el **Profiler** en cada fase; el techo de simultáneos es también una variable de dificultad.

## 15. Fuera de alcance (MVP)

Multiplayer, matchmaking, servidores, mundo abierto, campaña, historia cinemática, inventario complejo, crafting, vehículos, skins, battle pass, microtransacciones, ranking online, 20 mapas, decenas de armas.

## 16. Roadmap

| Fase | Contenido | Estado |
|---|---|---|
| **0** | Diseño cerrado (este documento) | ✅ Cerrada |
| **1** | **Vertical slice**: bucle completo (rondas, dinero, tienda, HUD, munición, muerte/reinicio) | ⏳ Siguiente |
| **2** | Feel de movimiento: sprint, stamina, agacharse, slide, mantle | Pendiente |
| **3** | Feel de combate: recoil, hitmarker, feedback, 2ª arma, arreglo del icono | Pendiente |
| **4** | Enemigos y dificultad: rápido, trepador, tanque, rondas especiales | Pendiente |
| **5** | Contenido y pulido: economía ampliada, bosses, más mapas, audio, VFX | Pendiente |

## 17. Abierto / a validar en playtest

- ¿La tienda sin pausa y sin temporizador genera acampada? Si sí: añadir presión (el mercader se marcha tras X segundos).
- Ajuste fino de precios y recompensas tras 5 partidas de prueba.
- Criterio de éxito del vertical slice: **3 partidas seguidas alcanzando ronda ≥ 5 sin aburrirse**.
- Velocidad de la horda frente a la velocidad del jugador (riesgo de que el juego se sienta injusto o demasiado fácil).
