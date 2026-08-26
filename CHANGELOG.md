# Changelog

Developed by Saelac with assistance from ChatGPT.

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
