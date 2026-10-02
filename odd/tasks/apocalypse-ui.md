# Feature: Apocalypse UI (HUD style B + cork-board shop style C)

## Objective
Restyle the player HUD and the shop with a post-apocalyptic theme, following the two mockups the user picked:
- HUD (mockup B, Higgsfield job `6955b55d-6b4c-4978-9b11-91d3aa1852eb`): spray-paint "RONDA N" with drips, "QUEDAN x / y" with a skull icon, money on a grey duct-tape strip (green stencil numbers), health on a red medical-tape strip with a heartbeat line and a medical cross, ammo in big stencil numbers with the active weapon silhouette and a row of bullet icons, thin crosshair, "[E] ABRIR TIENDA" prompt as a key badge, grime vignette on the screen edges.
- Shop (mockup C, job `c88d1b33-25f9-4c7a-bae5-486d1465163f`): the whole panel is a cork board; torn paper header "TIENDA" in red spray; category tabs as masking-tape labels with icons; item cards as polaroids with a white icon, name and marker price; locked items covered with a red tape strip "PROXIMAMENTE"; "TU PERSONAJE" stats on a notebook page; money on a cash envelope; LISTO on a green wooden sign with an arrow; close with the existing bone button.

## Constraints
- Keep the real shop catalog (`ShopCatalog.cs`) and gameplay logic; mockup items/stats are only visual references.
- Keep legacy `UnityEngine.UI.Text` (no TMP migration); use the downloaded TTF fonts.
- The Unity Editor is OPEN on the real project: never write scenes into the real project. Work and verify in the scratchpad clone; copy back only scripts and assets (each `.meta` before its asset). The user applies the UI to the scenes by running an Editor menu in the open editor and saves.
- Must work in both `SampleScene` and `Warehouse` (same Canvas hierarchy).
- Fix the known weapon icon bug (`WeaponSwitcher.weaponIconDisplay` unassigned) as part of the HUD weapon icon.

## Assets
- Fonts (Google Fonts, OFL/Apache, licenses included): `Assets/UI/Fonts/BlackOpsOne-Regular.ttf` (stencil numbers), `PermanentMarker-Regular.ttf` (marker text), `RubikWetPaint-Regular.ttf` (spray titles).
- Generated with Higgsfield (gpt_image_2_5, 1k medium, ~7 credits), raw PNGs in the session scratchpad `ui_raw/`: cork_board, paper_header, masking_tape_label, polaroid_frame, red_tape, notebook_page, cash_envelope, green_sign, tape_piece, hud_health_tape, hud_duct_tape, hud_grime_vignette, icons_sheet_items (4x4), icons_sheet_weapons (4x4).
- Imported copies live in `Assets/UI/Apocalypse/Textures` and `Assets/UI/Apocalypse/Icons`. Preparation (scratchpad `prep_ui.py`): alpha 254 -> 255 and <8 -> 0, icon sheets forced to pure white silhouettes, art cropped to its alpha bounds, cash envelope rotated 90 degrees (landscape), grime vignette darkened (x0.5) so it reads as dark smoke.

## Route and checks
- Route: delegated direct (one writer). Trigger: 2+ non-trivial files (HUD, shop scripts, applier) and asset preparation.
- TDD: off (no project/session TDD configuration). Checks: clean compile, applier run in batchmode on the clone for both scenes, rendered screenshots of the HUD and the open shop compared against mockups B and C, no missing references.
- RDD: off (decided by default).

## Tasks
- [x] T1 Import assets: sprites (copied raw PNGs), 4x4 icon sheets sliced into named sprites, fonts; an icon library mapping catalog ids, categories and weapons to sprites
- [x] T2 HUD style B (HudController + elements), weapon icon fixed, grime vignette
- [x] T3 Shop style C (ShopUI, ShopCard, tabs, notebook stats, envelope money, LISTO sign, locked red tape)
- [x] T4 Editor applier `Tools/Outbreak/Apply Apocalypse UI` (idempotent, wires serialized references in the open scene)
- [ ] T5 Verification in the clone done; the remaining step is the user applying the menu on both scenes in the open editor and saving (see Next step)

## Progress / evidence
- 2026-10-02: feature document created; fonts downloaded; 14 Higgsfield assets generated and reviewed (icon sheets aligned on a clean 4x4 grid).
- T1 (commit `75a47ed`): 12 textures + 2 sheets imported as FullRect sprites (UI quality: no mipmaps, CompressedHQ, alpha is transparency). Sheets sliced into 32 named sprites (`icon_*`, `weapon_*`, `hud_*`, `cat_*`; gun silhouettes trimmed to alpha bounds). 9-slice borders on paper_header, masking_tape_label, red_tape, green_sign, hud_health_tape, hud_duct_tape. `ApocalypseIconLibrary` asset maps 20 shop ids, 5 categories, 6 weapon alias groups (`m1911|pistola`, `ak74|ak47`, ...) and 5 HUD keys. `ApocalypseAssetSetup` is idempotent: second run logged `reconfigured: 0, library unchanged`.
- T2/T3 (commit `30d2703`): HudController adds optional refs (health fill, health block, weapon icon, bullet row/template, key badge) with a heartbeat pulse under 30 % HP and a punch-in on "RONDA N". ShopCard is a polaroid (icon, red tape PROXIMAMENTE + padlock when locked, COMPRADO stamp at max level, green/red price, tilt per id, hover scale). ShopUI themes tabs (tint + scale) and shows the money on the envelope. Everything is null-guarded: scenes without the new refs keep the legacy behaviour.
- Weapon icon fix: HudController now picks the silhouette from the library by `WeaponAmmo.WeaponName` (scene names are `AK74` and `M1911`; no knife exists in the scenes) every time the active weapon changes. `WeaponSwitcher.weaponIconDisplay` is left untouched (still unassigned, now unused).
- T4 (commit `ad8c146`): `ApocalypseUIApplier` (menu + `ApplyBatch`). Restyles/creates HUD (GrimeVignette, RondaBlock, DineroTape, VidaBlock, MunicionBlock, AvisoFila) and shop (Fondo, Tablon, HeaderPaper, tab icons, Sobre, notebook icons, sign subtitle, card template children) and wires HudController, ShopUI and ShopCard through SerializedObject. Legacy Text objects are re-parented, never deleted.
- T5 verification (clone `scratchpad/proj`, Unity 6000.3.9f1, batchmode, graphics on):
  - Clean compile: no `error CS` in any run (`ui_compile1.log`, `ui_apply5.log`).
  - Applier on both scenes: `applied to 'SampleScene' with 0 warning(s)` and `applied to 'Warehouse' with 0 warning(s)`; every HudController/ShopUI/ShopCard reference logged `ok` or set (10 + 10 legacy refs, 8 + 1 new HUD/ShopUI refs, 10 ShopCard refs, tabButtons/tabFondos 5, statusValues 6), no NULL.
  - Idempotence: pristine scenes copied from the repo, applier run once and twice, Canvas dump diff empty (135 objects per scene, no duplicates); dump also identical to a scene that went through several earlier iterations. All 18 original Canvas objects still present.
  - Screenshots at 1920x1080 (scratchpad `ui_shots/`, Canvas switched to Screen Space - Camera only inside the clone, never saved): `hud_warehouse.png`, `hud_lowhp_warehouse.png`, `hud_sample.png`, `shop_movilidad.png`, `shop_armas.png` (locked cards), `shop_municion.png`, `shop_combate.png`, `shop_objetos.png`, `shop_movilidad_sample.png`. Three layout iterations: label clipping fix (vertical overflow), full-screen cork board over a dark backdrop (the HUD no longer peeks out), two-line item names shrink to fit.
  - Play mode was not entered; runtime paths (events, weapon icon, bullet row, key badge parsing) were exercised in edit mode through reflection on sample values.

## Deviations from the mockups / known issues
- Cards show a flat black photo area (the generated polaroid frame has no photo); no per-item photographs.
- The cash envelope is the portrait generated asset rotated 90 degrees, so the banknotes poke out on the left instead of the top.
- "QUEDAN" pill is a plain translucent rectangle (no rounded sprite); the key badge uses the 9-sliced duct tape.
- The shop board covers the whole screen on a dark backdrop (works at any aspect ratio) instead of floating as a smaller board.
- Moving between tabs destroys and recreates cards (existing ShopUI behaviour), so hover scale resets after a purchase.
- `GameOverScreen` and `PauseMenu` were not restyled (optional); they still use the old look and work unchanged.
- The Engram mirror `odd/apocalypse-ui/tasks` must be refreshed by the parent (not touched from the writer).

## Next step
User, in the open editor: open `SampleScene`, run `Tools > Outbreak > Apply Apocalypse UI`, save (Ctrl+S); repeat for `Warehouse`. Then play one round to confirm the HUD and open the shop at the red cube.
