# Custom Rumors

Custom Rumors is a BepInEx 5 plugin for Silverpine 1.7.3 that makes the NPC
rumor system configurable. It owns the cross-faction rumor override formerly
included in Dynamic NPC Relationships.

**Current version:** 1.4.0

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
- Open **Force Rumor** to select a loaded sender and recipient, type the rumor
  yourself, and immediately start a delivery that bypasses the automatic
  scheduler's chance, time, visibility, distance, sleep, and following gates.
- Change every setting live through **Rumor Settings** in both Modding Tools'
  main-menu interface and its in-game **Mods** tab.

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
is in progress. A forced rumor is a manual action, so it does not create a
queued rumor or count against `Generation.MaximumRumorsPerNpcPerDay`.

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
4. Start Silverpine and open **Rumor Settings** or **Force Rumor** from Modding
   Tools.

Only one shared copy of `ModdingTools.dll` should be installed.

## Lifecycle

The plugin installs its Harmony patches and registers its settings and forced
delivery tools synchronously in BepInEx `Awake`. Silverpine destroys the initial
BepInEx host during its main-menu-to-game bootstrap, so the plugin deliberately
does not unpatch Harmony or unregister persistent callbacks from `OnDestroy`.
Both temporary windows are `ModToolBehaviour` instances and release their own
sessions normally when they close or are destroyed.

## Build

```powershell
dotnet build "Plugin Development/CustomRumors/CustomRumors.csproj" --configuration Release
```
