# Changelog

## 0.12.1 (unreleased)
- Groups: the Units tab (B) now has BOTS and GROUPS. A group spawns as one squad: the leader first, every member following him, sharing one group and its enemies.
  - SPT presets: every boss group in the server's map data (Reshala and guards, Goons, Kaban, Kollontay, Raiders, Rogues, Cultists, …), with the counts the maps use; a range like 2–4 is rolled on each placement. The summary lists the members, their attitude toward the leader and the maps they come from.
  - Faction mods (MoreBotsAPI, e.g. RUAF Come Home, UNTAR Go Home, Black Division): their patrols, checkpoints and hunts are listed in a section per faction. All waves of one leader become one preset, so each placement rolls a make-up the mod could have spawned.
  - Custom groups: New group, Edit, or Edit copy of a preset opens the group editor next to the dock. Pick member types from the same faction list as BOTS, set each one's difficulty and count, and choose the leader yourself or let Perun pick (a boss if there is one, otherwise the first member). Members that are hostile to the leader are flagged; inside one group they still fight as allies. Saved groups are kept in `BepInEx\config\com.dmnv.perun.groups.json`. Esc closes the editor and asks first about unsaved changes.
  - New F12 option "Refuse hostile group members" (off by default) keeps hostile types out of the editor's list.
  - Difficulty per placement: as set in the group, or one for everyone.
  - Placing: groups stand in a wedge with the leader at the point, facing away from the camera. Hold the mouse button and drag to turn it (this also works for single bots), Ctrl+wheel changes the spacing (0.8–4 m). Dots show where each member will stand.
  - When a group's leader dies, Perun picks the next one (another boss if there is one) and the rest follow him.
  - Host and single player only for now; a Fika game master sees a notice.
- Moving a group's leader now brings his followers along: they wake up with him and walk to his destination in a wedge, keeping pace with him. Followers fight on their own again when they meet enemies, unless the order was forced (Ctrl+right-click).
- Your own character is a unit: click the magenta dot (where you return) or "You" in the unit list to select it. Heal and the Arsenal work on it, Teleport moves where you return, and Kill asks for a second press because it ends your raid. Move orders, Remove and Take control skip it.
- BOTS list: each type shows its attitude toward the selected unit: ally, neutral, warns (only toward players: warns first, then shoots), hostile with a chance, or hostile. "GROUP SIZE" is now "COUNT".
- Arsenal:
  - Bots given a loadout no longer keep switching weapons without shooting. With SAIN, its weapon list is refreshed after every change.
  - Bots get spare rounds for their new guns in their secure container after a loadout.
  - The secure container's contents are copied too. Bots get the whole container; players keep their own and the items are placed in it, then in the rig, pockets or backpack if it's full.
  - With Verbose logging, each loadout writes a step-by-step trace of the unit's hands and weapons.
- MoreBotsAPI: bots spawned by Perun no longer throw an error in its hunt manager as they appear. The error cut the game's spawn setup short, so some bots never fully activated and group members were left unattached.
- One release zip for single player and Fika: the Fika bridge is in it and is only loaded when Fika is installed, so BepInEx no longer reports a missing dependency and other mods no longer log errors about it.
- Known issue: a leader escorted by his followers sometimes jumps about a metre, a few times per walk, mostly right after the order.

## 0.12.0 (2026-11-19)
- Verbose logging also writes the game version, plugin list and Perun settings once per session (the password only as set or empty), and each raid's map, Fika role, your character and game time.
- Edge scrolling: moving the cursor to the screen edge pans the view, now on by default ("Edge scrolling" in F12; it replaces "Edge pan"). The cursor stays inside the game window in RTS mode, also in windowed mode.
- When the bot you control dies, the camera pulls back over its body instead of flying back to where you took control.
- Fika (bridge protocol 6):
  - From the moment a player's game sends its token to the host, it accepts only host messages that carry it. The host confirms the token (or a 0.11.2 host signs its first message with it), and checking stays on for the rest of the raid. The token is sent again until the host confirms it.
  - Every Perun message is read safely: a damaged or cut-off message is ignored instead of causing an error inside Fika.
  - Messages with values the game can't use are rejected and logged: clock speeds, weather, spawn and teleport points outside the map, spawn counts outside 1–10, unknown roles, difficulties and order kinds.
  - The host no longer spawns roles the spawn panel hides (the BTR gunner, test bots) when a game master asks for them.
  - Works with older versions as before: players with an older bridge get messages without the check, and against an older host the check stays off.
- Arsenal:
  - Applying a loadout that stops partway (the bot gets busy or dies, the raid ends, an error) puts back the old gear of every slot not changed yet. Slots already changed keep their new gear.
  - Replacing a rig's or backpack's contents keeps the old contents until the new ones are in, and puts them back if anything other than "doesn't fit" goes wrong. Items that don't fit are left out and named.
  - Gear that can't be put back is reported in bold red, also when pasting onto a group, and is dropped next to the bot instead of disappearing.
- The BTR can only be watched: selecting it and locking the camera on it (L) still work, but moving, halting, healing, teleporting, removing, killing, arming and taking control skip it. The host skips it too.
- ORBIT: its ghost mode no longer hides bots around the RTS camera or the bot you control, and ORBIT no longer steers, aims or unsticks the controlled bot. Taking control of a bot ORBIT has asleep flies the camera to it first.
- Realism (Fontaine): its stances now follow the bot you control instead of your parked character, and the bot starts without your character's high or low ready, so the gun no longer drops after every shot.
- Fika: a bot you gave back no longer loses its weapon; Fika's bots already have player hands, so Perun doesn't swap them.
- Fika: killing a bot from the game master no longer leaves it frozen and unselectable (Fika needs a killer for the corpse; the bot counts as its own).
- FOV Fix (Fontaine): its aim FOV and weapon FOV scale now follow the bot you control instead of your parked character.
- Take control in Fika (still experimental, host only): the bot you control is now "your player", as in single player, so its shots do damage, magnified scopes show the zoomed view, the gun sits where it should and reloading works. Its weapon is drawn again when you take control so it's set up for first person, and if it dies Fika's own death flow doesn't end your raid.
- BetterThermalNightVision: it no longer replaces the RTS thermal mode with its own palette (black hot showed white hot).
- Stance Overhaul (Fontaine): it keeps working on the bot you control, so stance keys work and pistols and stockless weapons no longer lock in place.
- Take control:
  - The weapon now always works on the first takeover: a weapon switch that never finishes its animation is completed after 1.5 s.
  - Giving a bot back during its weapon switch no longer lets it draw the weapon afterwards.
  - A bot woken from standby gets a moment to settle before you take it.
  - Your ear ringing and your character's pain breathing no longer carry over to the bot.
  - Indoor/outdoor sound, and how you hear through a headset or helmet, follow the bot you control.
- A magenta dot marks where your character returns when you leave RTS mode. Click it, then click the map to move that spot; right-click cancels.
- Taking control zooms the camera in to the bot (its arms load meanwhile), and giving it back zooms out to the view you had before.
- F12 is shorter: internal tuning options are gone (they keep their defaults), and the per-kind label colours are under "Show advanced settings".
- Smoothie: buildings no longer stay partly hidden after first entering RTS mode. The startup log lists which Smoothie hooks Perun found.
- A spawn being prepared when the raid ends no longer affects the next raid's progress or loading speed.

## 0.11.2
- Fika: messages from the host (pause, weather, time, heal, teleport, game master answers and status) now carry a per-session client token, a random number each player's game creates when joining, so another player can no longer fake them. Fika's server forwards any player's packet to everyone, so without this a modified client could pause or teleport other players. Bridge protocol 5; players with an older bridge still work as before, without the check.
- Fika: incoming packets are checked against size limits and dropped if they break them.
- The host ignores order types it doesn't know instead of removing the selected units, and ignores spawn or teleport points that aren't valid numbers.
- A spawn still loading when the raid ends is dropped instead of continuing into the next raid.

## 0.11.1
- Internal cleanup after a code review; no new features.
- Bot control is split into smaller parts, and the weapon switch on taking and giving back control is one clear sequence. The earlier workarounds for frozen hands are gone now that the Smoothie fix covers the cause; a stuck switch is still reported in the log.
- Perun checks at startup that the game internals it relies on exist and lists any that are missing.
- Delayed work (heal refresh, arsenal retries) stops when the raid ends.
- The arsenal crate panel stays usable if adding an item fails.
- Removed the arsenal test keys and the temporary control, footstep and order diagnostics.

## 0.11.0
- Arsenal (I): a new tab for the selected units' gear. Single player for now.
  - Loadouts: copy one unit's gear and paste it onto any number of units, save loadouts by name and apply them in any raid, or give the selection random kits of a role (picked in the units browser). Covers headwear to weapons with all attachments, magazines and rig/backpack contents, plus the secure container's contents; pockets stay as they are. Units draw their new primary weapon afterwards.
  - Items: give any item to a slot or the inventory, or clear a slot.
  - Crate: fill a crate from the catalogue, take control of a bot and press K to open its inventory with the crate beside it, then edit its gear with the game's own drag and drop and weapon modding.
- Night vision and thermal for the RTS camera (N or the top-bar button): green, white and amber night vision, then white hot, black hot, fusion and rainbow thermal.
- Take control is much more reliable:
  - bots that Smoothie had slowed down or hidden no longer get locked or spinning hands, invisible weapons or missing footsteps;
  - the bot's own AI (BigBrain, SAIN, aiming, sprinting, going prone) no longer fights you while you drive it;
  - weapon handling sounds and footsteps keep working after several takeovers;
  - pain, bleeding and death effects no longer stay on the RTS view after you give a bot back;
  - a bot that dies while controlled stays visible, and the screen no longer goes black;
  - scopes, recoil and the HUD behave like your own character's.
- Heal also clears limping, lost sprint and the slower walk speed.
- Bots far from the player are woken up when you give them orders or take control, so they follow orders again.
- Performance: less per-frame work while controlling a bot and in the arsenal; detailed control and order traces only with Verbose logging.
- Fika: the bridge is now called DMNV-PerunFika (GUID com.dmnv.perunfika). The Fika release zip contains both DLLs.

## 0.10.0
- Take control: select one bot and press G (or Control on the card) to play it in first person with the game's own controls, HUD and inventory. Its AI stays off while you play; the RTS toggle key gives it back. Control ends if the bot dies or the world is paused. Single player; in Fika only the host, with the experimental "Allow take control in Fika" option on.
- Heal: the Heal button on the selection card fully heals the selected bots and players (health, destroyed limbs, bleeding, fractures, energy, hydration). In Fika the host heals its bots itself and asks players' games to heal them (needs the Fika bridge on the players, bridge protocol 4).
- Teleport: Alt+right-click (or Teleport on the selection card, then click) teleports the selected bots and players to the cursor, spread out like a move order. In Fika players' games move them (needs the Fika bridge on the players).

## 0.9.0
- Headless host: a player with the game master password (F12 › 6. Fika game master, same password on the headless host and the player) can run Perun. Spawning, orders, removing, pause, weather and time are carried out on the headless host; the loading card, ready counts, unit stars and order markers come back from it. One game master at a time; five wrong passwords lock a player out for a minute.
- Bots are recognised on Fika clients too (selection, labels, entity list).
- Bridge protocol 3; hosts and clients on older Perun keep working for pause, weather and time.

## 0.8.5
- Fika: weather and time changes made by the host now reach every client (needs the Fika bridge on everyone), including clients who join later.
- Changing the clock speed during a pause no longer restarts the clock; the new speed applies on resume.

## 0.8.4
- The interface only runs while there is something to show, so normal gameplay pays nothing for it.
- Long lists (entities, units) draw only the visible rows.
- Repeating errors are logged once instead of every frame.
- Interface text is cached instead of rebuilt every frame.
- Readable role names everywhere, from one shared cache.
- Key labels follow your key bindings.
- Perun keys and clicks are ignored while the F12 menu is open.
- Hidden raid timers are corrected too when a pause ends.
- The Fika bridge shares the mod's version.

## 0.8.3
- Saved weather: save the current or custom weather, reapply it (optionally with the time) in any raid.

## 0.8.2
- Loading card with progress bars for fetching profiles, loading gear and spawning; readiness shown in the units panel and at the cursor.

## 0.8.1
- Fika: Perun stays off if the host can't be confirmed.

## 0.8.0
- Zeus-style interface: top bar, entity list, selection card, units / environment dock, status strip, help overlay.

## 0.7.3
- Pause also stops SAIN bots and any bot shooting.

## 0.7.2
- Performance pass on the interface, selection and camera.

## 0.7.1
- Visible selection box and selection brackets; selection colour setting.

## 0.7.0
- Selection, box selection and move orders (requires BigBrain); hide the interface.

## 0.6.0
- Label colours per bot kind; loading speed setting.

## 0.5.x
- MoreBotsAPI factions; much faster bot loading; camera lock.

## 0.4.x
- World pause; Fika pause bridge; spawn diagnostics.

## 0.1.0 – 0.3.x
- RTS mode, god mode, AI ignore, RTS camera, time and weather panel, spawning.
