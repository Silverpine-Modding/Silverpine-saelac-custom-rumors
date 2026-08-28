# Changelog

Developed by Saelac with assistance from ChatGPT.

## 1.6.1

- Successful NPC-to-NPC rumor reactions are now stored in the sender's rumor
  memory as well as the recipient's, so the sender remembers how the recipient
  responded.
- Removed the one-sentence reaction restriction while retaining the private
  two-NPC context and observer/narration safeguards.
- NPC rumor reactions now use the same model-dependent, NPC-specific word cap
  as normal Silverpine conversation instead of a custom reaction length limit.

## 1.6.0

- Added the default-off `Reactions.NpcToNpcReactionsEnabled` toggle.
- When enabled, a successful normal or forced NPC-to-NPC rumor delivery asks
  the recipient for one short, in-character feeling about the rumor.
- NPC rumor reactions are stored only in the recipient's rumor memory and
  written to the BepInEx log. They do not create player notifications or
  floating text.
- Isolated reaction generation from the player-dialogue point of view and added
  narration stripping, validation, and one retry for observer-aware or
  multi-sentence model output. Reactions have no fixed word or character cap.

## 1.5.0

- Consolidated the settings and composer into one **Custom Rumors** button in
  each Modding Tools interface, with internal **Settings** and
  **Rumor Composer** tabs.
- Replaced the sender and recipient arrow cyclers with searchable dropdowns.
- Replaced the ambiguous queue-mode and submit button pair with one delivery
  mode dropdown and one **Create Rumor** button.
- Disabled horizontal scrolling and wrapped long content throughout the unified
  interface.
- Added **Add to Rumor Queue** mode alongside immediate forced delivery.
  Manually queued rumors use normal scheduling and saving without consuming
  the sender's automatic daily-generation cap.
- Added **Tell Rumor** to the inventory Actions menu and player-action radial
  wheel, using the same nearby-NPC targeting flow as Toggle Follower.
- Player-told rumors use Silverpine's native text prompt rather than dialogue.
  The selected NPC generates a one-line in-character reaction expressing their
  feeling about the matter; both the rumor and feeling are stored in rumor
  memory.

## 1.4.0

- Added **Force Rumor** to the main-menu and in-game Modding Tools interfaces.
- Added live sender and recipient selection plus a player-authored rumor text
  area with a 1,000-character limit.
- Forced delivery starts Silverpine's native pass-on-rumor routine immediately
  and bypasses the automatic chance, hour, visibility, distance, sleep, and
  following restrictions for that marked routine only.
- Forced deliveries retain travel, door handling, telling/listening activities,
  normal rumor memories, and save/load support while in progress.
- Manual rumors do not enter the automatic queue or consume the per-NPC daily
  generation cap.

## 1.3.0

- Changed `Generation.MaximumRumorsPerNpcPerDay` so `-1` means unlimited, `0`
  disables new rumor generation, and positive values remain the daily cap.
- Added a one-time semantics migration that converts version 1.2's unlimited
  value of `0` to `-1`, preventing an existing unlimited configuration from
  becoming disabled after updating.
- Updated both Modding Tools interfaces to label `-1` as **Unlimited** and `0`
  as **Disabled**.

## 1.2.0

- Replaced the multiple-rumor boolean with
  `Generation.MaximumRumorsPerNpcPerDay`, adjustable from `1` through `100`;
  `0` means unlimited and `1` preserves vanilla behavior.
- Daily counts follow the in-game day, survive normal save loading through the
  NPC's saved rumor-decision memories, and remain exact beyond the memory
  window while the NPC instance remains loaded.
- Migrates the former enabled boolean to the unlimited value.

## 1.1.0

- Added `Generation.AllowMultipleRumorsPerNpcPerDay`. When enabled, the same
  NPC can choose new rumors after multiple conversations on one day instead of
  being limited to one newly generated rumor.
- Added the setting to both live Modding Tools interfaces. It defaults off to
  preserve Silverpine behavior.

## 1.0.0

- Added live configuration for rumor generation, cross-faction recipients,
  delivery attempt chance, player-visibility avoidance, delivery hours,
  Darian's early-hour exception, and nearest-versus-random recipients.
- Added main-menu and in-game settings interfaces through Modding Tools 1.9.3.
- Migrates the former Dynamic NPC Relationships cross-faction CFG value on the
  first run.
- Uses Silverpine's process-lifetime patch lifecycle and preserves its Harmony
  hooks through bootstrap-host destruction.
