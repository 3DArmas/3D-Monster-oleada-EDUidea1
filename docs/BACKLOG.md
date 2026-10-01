# Project Outbreak — Backlog por fases

> Cada unidad de trabajo (WU) debe ser un **commit pequeño y revisable**.
> Ninguna WU se da por terminada sin cumplir sus **criterios de aceptación**.
> Las escenas y prefabs se tocan en commits propios (son archivos propensos a conflictos en un repo compartido).

---

## Fase 1 — Vertical slice (bucle completo)

**Objetivo de la fase:** poder jugar desde la ronda 1 hasta morir, con el ciclo **ronda → matar → dinero → tienda [E] → LISTO → ronda+1**, aunque el aspecto sea feo.

**Criterios de aceptación de la fase:**
- [ ] Se puede jugar el ciclo completo sin que nada se rompa.
- [ ] Cada ronda tiene número visible y aparece con dificultad creciente.
- [ ] El HUD muestra HP, dinero, munición y ronda.
- [ ] Comprar cambia de verdad (munición, curación) y descuenta el dinero.
- [ ] Al morir se muestra el resumen y se puede reiniciar.
- [ ] 60 fps con 24 zombies simultáneos en el blockout.

**Estado real (1 oct 2026):**
- ✅ **WU-1.1** Núcleo de partida · ✅ **WU-1.2** Recompensa por eliminación · ✅ **WU-1.4** Munición y recarga
- 🟡 **WU-1.7** Game over: **lógica terminada y verificada**, falta la pantalla de resumen
- ⏳ Pendientes: **WU-1.3** HUD · **WU-1.5** Cuchillo · **WU-1.6** Tienda (cubo + E) · **WU-1.8** Blockout
- Verificado en Play mode: ronda 1 con 8 zombies, **$100 por baja** (matar 2 → $200), vivos 8→6, el jugador
  recibe daño real (100→10→0) y al morir el estado pasa a `GameOver` con la ronda detenida.

### WU-1.1 — Núcleo de partida
**Entregable:** `GameManager` (estados: `Preparando`, `Ronda`, `Tienda`, `GameOver`), `RoundManager` (número de ronda, composición, detección de fin de ronda), `MoneySystem` (dinero, sumar/gastar, eventos).
**Aceptación:**
- [ ] La ronda avanza sola cuando mueren todos los zombies.
- [ ] El estado del juego es consultable y cambia correctamente.
- [ ] `MoneySystem` emite evento al cambiar el saldo.

### WU-1.2 — Recompensa por eliminación
**Entregable:** al morir un zombie, se notifica al `RoundManager` (baja del recuento) y al `MoneySystem` (+recompensa según tipo).
**Aceptación:**
- [ ] Matar un zombie normal suma $100.
- [ ] El recuento de vivos baja correctamente (sin zombies "fantasma").
- [ ] La ronda termina solo cuando el recuento llega a 0.

### WU-1.3 — HUD mínimo
**Entregable:** HUD sobre el Canvas existente: HP, dinero, munición + arma, ronda, avisos centrales ("RONDA N", "TIENDA ABIERTA"), crosshair.
**Aceptación:**
- [ ] Todos los valores se actualizan en tiempo real.
- [ ] El aviso de ronda aparece al empezar cada ronda.
- [ ] Legible a 1080p sin tapar el centro de la pantalla.

### WU-1.4 — Munición y recarga
**Entregable:** sistema de munición por arma (cargador + reserva), recarga con **R**, click seco sin balas, consumo al disparar.
**Aceptación:**
- [ ] Pistola: cargador 12, reserva 96, recarga 1.8 s.
- [ ] No se puede disparar sin balas ni recargar con el cargador lleno.
- [ ] El HUD refleja cargador/reserva.

### WU-1.5 — Cuchillo
**Entregable:** slot 3 con cuchillo (daño 50, 1.5 golpes/s, alcance 2 m, sin munición).
**Aceptación:**
- [ ] Se cambia con la tecla 3 y con la rueda.
- [ ] Mata a un zombie normal en 2 golpes.
- [ ] El HUD muestra "CUCHILLO" sin contador de munición.

### WU-1.6 — Tienda: cubo interactivo
**Entregable:** cubo flotante con interacción **[E]** (radio 3 m) que abre la UI de compra; botón **LISTO** que empieza la siguiente ronda.
**Aceptación:**
- [ ] Sin pausa: el jugador puede moverse con la tienda abierta.
- [ ] Se compran: munición, +25 HP, botiquín. El saldo y el efecto se aplican de verdad.
- [ ] No se puede comprar sin dinero (aviso claro).
- [ ] LISTO cierra la tienda y arranca la ronda siguiente.

### WU-1.7 — Muerte del jugador, game over y reinicio
**Entregable:** al morir: pantalla de resumen (ronda, bajas, dinero ganado, tiempo) + botón VOLVER A JUGAR.
**Aceptación:**
- [ ] La partida se detiene limpiamente (sin zombies ni spawns residuales).
- [ ] El resumen muestra datos reales de la partida.
- [ ] Reiniciar deja el juego en ronda 1, 100 HP y $0.

### WU-1.8 — Mapa blockout
**Entregable:** blockout ~30 × 30 m con primitivas (suelo, cajas, paredes, 2–3 plataformas), 4–6 spawn points y NavMesh horneado.
**Aceptación:**
- [ ] Se puede rodear, escapar y subir a las plataformas.
- [ ] Los zombies llegan al jugador sin quedarse atascados.
- [ ] 60 fps con 24 zombies vivos.

---

## Fase 2 — Feel de movimiento

**Objetivo:** que moverse sea divertido por sí mismo (es el pilar nº1).
**WU:** 2.1 sprint + stamina · 2.2 agacharse · 2.3 slide · 2.4 mantle · 2.5 tuning de cámara (FOV, head bob suave, inclinación) · 2.6 ajuste del mapa para parkour.
**Aceptación de la fase:**
- [ ] Sprint con consumo y recuperación de stamina visible en el HUD.
- [ ] Slide encadenable con salto y con la sensación de "impulso".
- [ ] Mantle sin teletransportes ni quedarse enganchado.
- [ ] El mapa permite 2 rutas alternativas claras.

## Fase 3 — Feel de combate

**WU:** 3.1 recoil y dispersión · 3.2 hitmarker + feedback de impacto · 3.3 daño recibido (viñeta/indicador) · 3.4 recarga con animación simple · 3.5 arreglo del icono del arma · 3.6 segunda arma comprable (escopeta).
**Aceptación:** disparar "se siente bien" (criterio subjetivo del autor) · el arma comprada se integra en la tienda y el cambio de arma.

## Fase 4 — Enemigos y dificultad

**WU:** 4.1 zombie rápido · 4.2 escalado por ronda (HP/daño/velocidad/intervalo) · 4.3 **trepador** (riesgo alto) · 4.4 tanque · 4.5 rondas especiales.
**Aceptación:** la ronda 10 se siente distinta de la ronda 2, no solo "con más zombies".

## Fase 5 — Contenido y pulido

**WU:** 5.1 mercader animado (aventurero asustado) + variante "cobarde" · 5.2 mini boss y boss · 5.3 economía ampliada y mejoras · 5.4 audio (armas, zombies, ambiente, música) · 5.5 segundo mapa · 5.6 progresión permanente (roguelite: desbloqueos y camuflajes).

---

## Registro

| Fecha | Cambio |
|---|---|
| 2026-10-01 | Fase 0 cerrada. GDD v1.1 y backlog creados. Fase 1 lista para empezar. |
