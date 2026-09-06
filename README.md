# Custom Rumors

Custom Rumors is a BepInEx 5 plugin for Silverpine 1.7.3 that makes the NPC
rumor system configurable. It owns the cross-faction rumor override formerly
included in Dynamic NPC Relationships.

**Current version:** 1.6.2

## Features

- Enable or disable new rumor generation without deleting queued rumors.
- Set a per-NPC daily cap for newly chosen rumors, use `-1` for unlimited, or
  use `0` to disable new rumors,
  allowing multiple distinct same-day deliveries after multiple conversations.
- Allow newly generated rumors to target NPCs in other factions.
- Configure the chance that an NPC schedules a rumor-delivery routine.
- Configure how strongly NPCs avoid delivering rumors while they can see the
  player.
- Configure inclusive delivery hours, including overnight windows.
- Keep or remove Silverpine's early-hour exception for Darian.
- Choose the nearest eligible recipient, as in vanilla, or a random eligible
  recipient.
- Optionally let receiving NPCs privately react to NPC-delivered rumors. Their
  one-line feeling is stored in rumor memory and written to the BepInEx log,
  without showing a player notification.
- Open the single **Custom Rumors** Modding Tools entry and switch between its
  **Settings** and **Rumor Composer** tabs.
- Select a loaded sender and recipient from searchable dropdowns, type the
  rumor yourself, then either add it to the normal rumor queue or force its
  delivery immediately.
- Use **Tell Rumor** from the inventory Actions menu or the player-action radial
  wheel, select a nearby NPC, and type a rumor without opening dialogue. The
  NPC gives one short in-character response expressing how they feel, and both
  the rumor and that feeling are saved to the NPC's rumor memory.

The master `General.Enabled` switch restores vanilla behavior while retaining
the other values. Settings apply immediately and are stored in:

`BepInEx/config/renegadex.silverpine.customrumors.cfg`

Cross-faction targeting affects only rumors generated after the setting is
enabled. It does not rewrite recipient lists already stored in a save. Acacia
uses the same faction rule as the other NPCs.

Silverpine already permits an NPC to deliver multiple queued rumors in one day;
its actual one-per-day restriction is on choosing a new rumor after a
conversation. `Generation.MaximumRumorsPerNpcPerDay` replaces that gate with an
adjustable cap from `1` through `100`; `-1` means unlimited and `0` disables new
rumors. It defaults to `1` so the initial behavior remains vanilla. Counts reset
with the in-game day and are reconstructed from the NPC's saved same-day
rumor-decision memories after a save is loaded.

Forced rumors use Silverpine's own serializable pass-on-rumor routine. The
selected sender travels to the selected recipient, performs the normal visible
telling/listening activities, and writes the normal rumor memory to both NPCs.
The stable forced-routine marker survives saving and loading while a delivery
is in progress. Queue mode instead creates a normal saved rumor for the chosen
sender and recipient and lets the configured scheduler deliver it. Both are
manual actions and do not count against
`Generation.MaximumRumorsPerNpcPerDay`.

`Reactions.NpcToNpcReactionsEnabled` defaults to `false`. When enabled, each
successful NPC-to-NPC delivery asks the receiving NPC for one short,
in-character feeling about the rumor and adds that reaction to both the
recipient's and sender's rumor memories. The generated response is also written
to the BepInEx log. Unlike player-told rumors, NPC-to-NPC reactions never
display a notification or floating text to the player. Reaction generation
uses a private two-NPC context that excludes the player and bystanders.
Narrated or observer-aware output is stripped or rejected and retried instead
of being written unfiltered to memory. Reactions may contain multiple
sentences. When Silverpine applies an NPC-specific word cap to normal
conversation for the active model, the same cap is applied to that NPC's rumor
reaction; no separate character cap is imposed.

## Migration from Dynamic NPC Relationships

Dynamic NPC Relationships 1.19.0 no longer patches rumor generation and no
longer owns `Rumors.AllowCrossFactionRumors`. On its first run, Custom Rumors
copies that legacy value from Dynamic NPC Relationships' CFG when the new
Custom Rumors CFG does not yet exist.

Update Dynamic NPC Relationships to 1.19.0 or later when installing this
plugin. Version 1.18.0 still contains the old cross-faction transpiler and must
not be used alongside Custom Rumors.

Dynamic NPC Relationships remains optional. When it is installed, successful
rumor deliveries can still award its configured `RumorExchangeGain`; it no
longer controls how rumors are generated or scheduled.

## Installation

1. Install BepInEx 5 and Modding Tools Menu 1.9.3 or later.
2. Place `CustomRumors.dll` in `BepInEx/plugins/CustomRumors/`.
3. If Dynamic NPC Relationships is installed, update it to 1.19.0 or later.
4. Start Silverpine and open **Custom Rumors** from Modding Tools.

Only one shared copy of `ModdingTools.dll` should be installed.

## Lifecycle

The plugin installs its Harmony patches and registers its unified tool
synchronously in BepInEx `Awake`. Silverpine destroys the initial BepInEx host
during its main-menu-to-game bootstrap, so the plugin deliberately does not
unpatch Harmony or unregister persistent callbacks from `OnDestroy`. The
temporary unified window is a `ModToolBehaviour` and releases its session
normally when it closes or is destroyed. The **Tell Rumor** ability uses
Silverpine's native action-menu, targeting, and text-input lifecycles.

## Build

```powershell
dotnet build "Plugin Development/CustomRumors/CustomRumors.csproj" --configuration Release
```
