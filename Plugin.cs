#nullable enable

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Silverpine.ModdingTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(
    Silverpine.ModdingTools.Plugin.PluginGuid,
    "1.9.3")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "renegadex.silverpine.customrumors";
    public const string PluginName = "Custom Rumors";
    public const string PluginVersion = "1.6.0";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigFile Settings { get; private set; } = null!;

    internal static ConfigEntry<bool> CustomizationsEnabled { get; private set; }
        = null!;
    internal static ConfigEntry<bool> RumorGenerationEnabled { get; private set; }
        = null!;
    internal static ConfigEntry<int> MaximumRumorsPerNpcPerDay
        { get; private set; } = null!;
    internal static ConfigEntry<int> DailyLimitSemanticsVersion
        { get; private set; } = null!;
    internal static ConfigEntry<bool> AllowCrossFactionRumors { get; private set; }
        = null!;
    internal static ConfigEntry<int> RoutineAttemptChancePercent { get; private set; }
        = null!;
    internal static ConfigEntry<int> AvoidPlayerVisibilityChancePercent
        { get; private set; } = null!;
    internal static ConfigEntry<int> EarliestDeliveryHour { get; private set; }
        = null!;
    internal static ConfigEntry<int> LatestDeliveryHour { get; private set; }
        = null!;
    internal static ConfigEntry<bool> DarianIgnoresEarliestHour
        { get; private set; } = null!;
    internal static ConfigEntry<bool> PreferNearestRecipient { get; private set; }
        = null!;
    internal static ConfigEntry<bool> NpcRumorReactionsEnabled
        { get; private set; } = null!;

    private static readonly FieldInfo ThingsThatHappenedField =
        AccessTools.Field(typeof(NeuralNPC), "thingsThatHappened");
    private static readonly ConditionalWeakTable<
        NeuralNPC, DailyRumorGenerationState> DailyRumorCounts = new();

    private void Awake()
    {
        Log = Logger;
        Settings = Config;
        bool newConfiguration = !File.Exists(Config.ConfigFilePath);
        bool migrateLegacyUnlimited =
            TryReadLegacyMultipleRumorSetting(out bool legacyUnlimited)
            && legacyUnlimited;
        BindConfiguration();
        MigrateDailyLimitSemantics(migrateLegacyUnlimited);
        MigrateLegacyCrossFactionSetting(newConfiguration);

        ModdingToolsMenu.RegisterSession(
            PluginGuid + ".main",
            "Custom Rumors",
            (_, session) => CustomRumorsWindow.Open(session),
            order: 330);
        InventoryModTools.RegisterSession(
            PluginGuid + ".game",
            "Custom Rumors",
            (_, session) => CustomRumorsWindow.Open(session),
            order: 330);

        Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginGuid);
        Log.LogInfo(
            "Custom Rumors initialized and registered its Modding Tools "
            + "unified interface and Tell Rumor player action.");
    }

    private void BindConfiguration()
    {
        CustomizationsEnabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Master switch for Custom Rumors. Disabled restores Silverpine's "
            + "vanilla rumor generation, targeting, scheduling, and recipient "
            + "selection behavior.");
        RumorGenerationEnabled = Config.Bind(
            "Generation",
            "Enabled",
            true,
            "Allow NPC conversations to generate new rumors. Existing queued "
            + "rumors can still be delivered when this is disabled.");
        MaximumRumorsPerNpcPerDay = Config.Bind(
            "Generation",
            "MaximumRumorsPerNpcPerDay",
            1,
            new ConfigDescription(
                "Maximum new rumors each NPC may choose per in-game day. "
                + "Use -1 for unlimited, 0 to disable new rumors, or 1 for "
                + "Silverpine's vanilla limit.",
                new AcceptableValueRange<int>(-1, 100)));
        DailyLimitSemanticsVersion = Config.Bind(
            "Migration",
            "DailyLimitSemanticsVersion",
            0,
            "Internal migration marker for daily rumor-limit values. Do not "
            + "edit this setting.");
        AllowCrossFactionRumors = Config.Bind(
            "Generation",
            "AllowCrossFactionRumors",
            false,
            "Allow newly generated rumors to retain recipients from another "
            + "faction. Disabled preserves Silverpine's same-faction rule.");
        RoutineAttemptChancePercent = Config.Bind(
            "Delivery",
            "RoutineAttemptChancePercent",
            10,
            new ConfigDescription(
                "Chance that an eligible NPC tries to schedule a queued rumor "
                + "when Silverpine evaluates its multi-rumor routine. Vanilla "
                + "is 10 percent.",
                new AcceptableValueRange<int>(0, 100)));
        AvoidPlayerVisibilityChancePercent = Config.Bind(
            "Delivery",
            "AvoidPlayerVisibilityChancePercent",
            95,
            new ConfigDescription(
                "Chance to postpone a rumor attempt when the source NPC can "
                + "see the player. Vanilla is 95 percent.",
                new AcceptableValueRange<int>(0, 100)));
        EarliestDeliveryHour = Config.Bind(
            "Delivery",
            "EarliestHour",
            8,
            new ConfigDescription(
                "First inclusive hour when normal rumor delivery can begin. "
                + "Set later than LatestHour for an overnight window.",
                new AcceptableValueRange<int>(0, 23)));
        LatestDeliveryHour = Config.Bind(
            "Delivery",
            "LatestHour",
            22,
            new ConfigDescription(
                "Last inclusive hour when rumor delivery can begin. Set "
                + "earlier than EarliestHour for an overnight window.",
                new AcceptableValueRange<int>(0, 23)));
        DarianIgnoresEarliestHour = Config.Bind(
            "Delivery",
            "DarianIgnoresEarliestHour",
            true,
            "Let Darian deliver before EarliestHour when the configured hours "
            + "do not form an overnight window. This is vanilla behavior.");
        PreferNearestRecipient = Config.Bind(
            "Delivery",
            "PreferNearestRecipient",
            true,
            "Choose the closest currently eligible rumor recipient. Disabled "
            + "chooses randomly from the eligible recipients.");
        NpcRumorReactionsEnabled = Config.Bind(
            "Reactions",
            "NpcToNpcReactionsEnabled",
            false,
            "Generate one private, in-character feeling when an NPC receives "
            + "a rumor from another NPC. The feeling is stored in the "
            + "recipient's rumor memory and written to the BepInEx log, but "
            + "is never shown to the player.");
    }

    private static void MigrateLegacyCrossFactionSetting(
        bool newConfiguration)
    {
        if (!newConfiguration
            || !TryReadLegacyCrossFactionSetting(out bool legacyValue))
        {
            return;
        }

        AllowCrossFactionRumors.Value = legacyValue;
        Settings.Save();
        Log.LogInfo(
            "Migrated Rumors.AllowCrossFactionRumors=" + legacyValue
            + " from Dynamic NPC Relationships.");
    }

    private static bool TryReadLegacyMultipleRumorSetting(out bool value)
    {
        value = false;
        string configPath = Settings.ConfigFilePath;
        if (!File.Exists(configPath))
            return false;

        try
        {
            string section = "";
            bool hasNewSetting = false;
            bool hasLegacySetting = false;
            bool legacyValue = false;
            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)
                    && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int separator = line.IndexOf('=');
                if (!section.Equals(
                        "Generation", StringComparison.OrdinalIgnoreCase)
                    || separator < 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string rawValue = line.Substring(separator + 1).Trim();
                if (key.Equals(
                    "MaximumRumorsPerNpcPerDay",
                    StringComparison.OrdinalIgnoreCase))
                {
                    hasNewSetting = true;
                }
                else if (key.Equals(
                        "AllowMultipleRumorsPerNpcPerDay",
                        StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(rawValue, out bool parsed))
                {
                    hasLegacySetting = true;
                    legacyValue = parsed;
                }
            }

            value = legacyValue;
            return !hasNewSetting && hasLegacySetting;
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                "Could not inspect the former multiple-rumor setting: "
                + exception.Message);
            return false;
        }
    }

    private static void MigrateDailyLimitSemantics(
        bool legacyUnlimited)
    {
        const int currentSemanticsVersion = 2;
        if (DailyLimitSemanticsVersion.Value >= currentSemanticsVersion)
            return;

        // Version 1.2 used zero for unlimited. Version 1.1 used an enabled
        // boolean for the same behavior. Preserve both meanings once, before
        // zero becomes the explicit disabled value.
        if (legacyUnlimited || MaximumRumorsPerNpcPerDay.Value == 0)
            MaximumRumorsPerNpcPerDay.Value = -1;
        DailyLimitSemanticsVersion.Value = currentSemanticsVersion;
        Settings.Save();
        Log.LogInfo(
            "Updated daily rumor-limit semantics: -1 is unlimited and 0 is "
            + "disabled.");
    }

    private static bool TryReadLegacyCrossFactionSetting(out bool value)
    {
        value = false;
        string legacyPath = Path.Combine(
            Paths.ConfigPath,
            "renegadex.silverpine.dynamicnpcrelationships.cfg");
        if (!File.Exists(legacyPath))
            return false;

        try
        {
            string section = "";
            foreach (string rawLine in File.ReadAllLines(legacyPath))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)
                    && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int separator = line.IndexOf('=');
                if (!section.Equals("Rumors", StringComparison.OrdinalIgnoreCase)
                    || separator < 0
                    || !line.Substring(0, separator).Trim().Equals(
                        "AllowCrossFactionRumors",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return bool.TryParse(
                    line.Substring(separator + 1).Trim(),
                    out value);
            }
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                "Could not migrate the legacy cross-faction rumor setting: "
                + exception.Message);
        }

        return false;
    }

    public static bool IsCrossFactionRumorBypassEnabled() =>
        CustomizationsEnabled?.Value == true
        && AllowCrossFactionRumors?.Value == true;

    public static bool SetCrossFactionRumorBypassEnabled(bool enabled)
    {
        if (AllowCrossFactionRumors == null || Settings == null)
            return false;
        if (AllowCrossFactionRumors.Value == enabled)
            return true;

        AllowCrossFactionRumors.Value = enabled;
        Settings.Save();
        Log.LogInfo(
            "Cross-faction rumor targeting "
            + (enabled ? "enabled." : "disabled."));
        return true;
    }

    internal static bool AreRumorFactionsCompatible(
        Faction targetFaction,
        Faction sourceFaction) =>
        targetFaction == sourceFaction
        || IsCrossFactionRumorBypassEnabled();

    internal static bool HasReachedDailyRumorLimit(
        NeuralNPC npc,
        bool generatedRumorToday)
    {
        if (!CustomizationsEnabled.Value)
            return generatedRumorToday;

        int maximum = Mathf.Clamp(
            MaximumRumorsPerNpcPerDay.Value, -1, 100);
        if (maximum < 0)
            return false;
        if (maximum == 0)
            return true;
        if (maximum == 1)
            return generatedRumorToday;

        return GetDailyRumorCount(npc, generatedRumorToday) >= maximum;
    }

    internal static void RecordRumorGenerated(NPCName npcName)
    {
        if (!NeuralNPC.neuralNPCs.TryGetValue(
                npcName, out NeuralNPC npc)
            || npc == null
            || WorldInfoManager.Instance == null)
        {
            return;
        }

        int day = WorldInfoManager.Instance.GetCurrentDay();
        DailyRumorGenerationState state =
            DailyRumorCounts.GetValue(
                npc, _ => new DailyRumorGenerationState());
        int storedCount = CountStoredRumorsForDay(npc, day);
        if (state.Day != day)
        {
            state.Day = day;
            // The saved decision memory is added immediately before AddRumor,
            // so exclude that current entry before incrementing below.
            state.Count = Math.Max(0, storedCount - 1);
        }
        state.Count = Math.Max(state.Count + 1, storedCount);
    }

    private static int GetDailyRumorCount(
        NeuralNPC npc,
        bool generatedRumorToday)
    {
        if (WorldInfoManager.Instance == null)
            return generatedRumorToday ? 1 : 0;

        int day = WorldInfoManager.Instance.GetCurrentDay();
        int storedCount = CountStoredRumorsForDay(npc, day);
        DailyRumorGenerationState state =
            DailyRumorCounts.GetValue(
                npc, _ => new DailyRumorGenerationState());
        if (state.Day != day)
        {
            state.Day = day;
            state.Count = storedCount;
        }
        else
        {
            state.Count = Math.Max(state.Count, storedCount);
        }

        if (generatedRumorToday)
            state.Count = Math.Max(1, state.Count);
        return state.Count;
    }

    private static int CountStoredRumorsForDay(NeuralNPC npc, int day)
    {
        if (!(ThingsThatHappenedField.GetValue(npc)
            is List<NeuralNPC.AdvancedMemoryElement> memories))
        {
            return 0;
        }

        int firstTurn = day * 2880;
        int lastTurnExclusive = firstTurn + 2880;
        return memories.Count(memory =>
            memory != null
            && memory.turnCount >= firstTurn
            && memory.turnCount < lastTurnExclusive
            && memory.memory != null
            && memory.memory.IndexOf(
                " decided to later tell ",
                StringComparison.Ordinal) >= 0);
    }

    internal static bool RollPercent(int percentage)
    {
        int clamped = Mathf.Clamp(percentage, 0, 100);
        return clamped >= 100
            || (clamped > 0 && UnityEngine.Random.value * 100f < clamped);
    }

    internal static bool IsDeliveryHourAllowed(
        NPCName target,
        int hour)
    {
        int earliest = Mathf.Clamp(EarliestDeliveryHour.Value, 0, 23);
        int latest = Mathf.Clamp(LatestDeliveryHour.Value, 0, 23);
        bool overnight = earliest > latest;
        bool inWindow = overnight
            ? hour >= earliest || hour <= latest
            : hour >= earliest && hour <= latest;
        if (inWindow)
            return true;

        return !overnight
            && target == NPCName.Darian
            && DarianIgnoresEarliestHour.Value
            && hour < earliest;
    }

    private void OnDestroy()
    {
        // Silverpine destroys the bootstrap host while gameplay continues.
        // Persistent patches, configuration, and Modding Tools registrations
        // intentionally remain alive for the lifetime of the process.
        Log.LogInfo(
            "Plugin host destroyed during Silverpine bootstrap; persistent "
            + "rumor patches and Modding Tools registrations remain installed.");
    }

    private sealed class DailyRumorGenerationState
    {
        internal int Day = int.MinValue;
        internal int Count;
    }
}

[HarmonyPatch(typeof(NeuralNPC), nameof(NeuralNPC.GenerateRumor))]
internal static class RumorGenerationEnablePatch
{
    [HarmonyPrefix]
    private static bool PermitRumorGeneration() =>
        !Plugin.CustomizationsEnabled.Value
        || Plugin.RumorGenerationEnabled.Value;
}

[HarmonyPatch]
internal static class MultipleDailyRumorGenerationPatch
{
    private static readonly FieldInfo GeneratedRumorTodayField =
        AccessTools.Field(typeof(NeuralNPC), "generatedARumorToday");
    private static readonly MethodInfo DailyLimitMethod =
        AccessTools.Method(
            typeof(Plugin),
            nameof(Plugin.HasReachedDailyRumorLimit));

    private static MethodBase TargetMethod()
    {
        MethodInfo generateRumor = AccessTools.Method(
            typeof(NeuralNPC),
            nameof(NeuralNPC.GenerateRumor));
        AsyncStateMachineAttribute? stateMachine =
            generateRumor.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (stateMachine == null)
        {
            throw new MissingMethodException(
                "Could not find NeuralNPC.GenerateRumor's async state machine.");
        }

        return AccessTools.Method(stateMachine.StateMachineType, "MoveNext")
            ?? throw new MissingMethodException(
                "Could not find NeuralNPC.GenerateRumor.MoveNext.");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        int patched = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode != OpCodes.Ldfld
                || !Equals(instruction.operand, GeneratedRumorTodayField))
            {
                yield return instruction;
                continue;
            }

            CodeInstruction duplicateNpc = new(OpCodes.Dup);
            duplicateNpc.labels.AddRange(instruction.labels);
            duplicateNpc.blocks.AddRange(instruction.blocks);
            instruction.labels.Clear();
            instruction.blocks.Clear();
            yield return duplicateNpc;
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Call, DailyLimitMethod);
            patched++;
        }

        if (patched != 1)
        {
            throw new InvalidOperationException(
                "Expected one daily rumor-generation gate, but found "
                + patched + ".");
        }

        Plugin.Log.LogInfo(
            "Installed the configurable daily rumor-generation gate.");
    }
}

[HarmonyPatch(typeof(RumorManager), nameof(RumorManager.AddRumor))]
internal static class SuccessfulRumorGenerationCounterPatch
{
    [HarmonyPostfix]
    private static void CountGeneratedRumor(NPCName npcName, Rumor rumor)
    {
        if (!ForcedRumorDelivery.IsManualQueuedRumor(rumor))
            Plugin.RecordRumorGenerated(npcName);
    }
}

[HarmonyPatch]
internal static class CrossFactionRumorGenerationPatch
{
    private const string DifferentFactionLogPrefix =
        "GenerateRumor: Not sharing rumor with NPC of different faction: ";

    private static readonly FieldInfo FactionField =
        AccessTools.Field(typeof(NeuralNPC), nameof(NeuralNPC.faction));
    private static readonly MethodInfo CompatibilityMethod =
        AccessTools.Method(
            typeof(Plugin),
            nameof(Plugin.AreRumorFactionsCompatible));

    private static MethodBase TargetMethod()
    {
        MethodInfo generateRumor = AccessTools.Method(
            typeof(NeuralNPC),
            nameof(NeuralNPC.GenerateRumor));
        AsyncStateMachineAttribute? stateMachine =
            generateRumor.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (stateMachine == null)
        {
            throw new MissingMethodException(
                "Could not find NeuralNPC.GenerateRumor's async state machine.");
        }

        return AccessTools.Method(stateMachine.StateMachineType, "MoveNext")
            ?? throw new MissingMethodException(
                "Could not find NeuralNPC.GenerateRumor.MoveNext.");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int patched = 0;

        for (int index = 0; index < codes.Count; index++)
        {
            CodeInstruction instruction = codes[index];
            bool isEqualityBranch = instruction.opcode == OpCodes.Beq
                || instruction.opcode == OpCodes.Beq_S;
            bool isFactionComparison = index >= 3
                && Equals(codes[index - 1].operand, FactionField)
                && Equals(codes[index - 3].operand, FactionField);
            bool hasExpectedRejectionMessage = index + 1 < codes.Count
                && codes[index + 1].opcode == OpCodes.Ldstr
                && Equals(
                    codes[index + 1].operand,
                    DifferentFactionLogPrefix);

            if (!isEqualityBranch
                || !isFactionComparison
                || !hasExpectedRejectionMessage)
            {
                yield return instruction;
                continue;
            }

            if (patched != 0)
            {
                throw new InvalidOperationException(
                    "Found more than one rumor faction check.");
            }

            CodeInstruction compatibilityCall = new(
                OpCodes.Call,
                CompatibilityMethod);
            compatibilityCall.labels.AddRange(instruction.labels);
            compatibilityCall.blocks.AddRange(instruction.blocks);
            instruction.labels.Clear();
            instruction.blocks.Clear();

            yield return compatibilityCall;
            instruction.opcode = OpCodes.Brtrue;
            yield return instruction;
            patched++;
        }

        if (patched != 1)
        {
            throw new InvalidOperationException(
                "Could not locate Silverpine's rumor faction check.");
        }

        Plugin.Log.LogInfo(
            "Installed the configurable cross-faction rumor check.");
    }
}

[HarmonyPatch(
    typeof(DynamicRoutineStarter_MultiRumor),
    nameof(DynamicRoutineStarter_MultiRumor.GetOverrideRoutine))]
internal static class ConfigurableRumorRoutinePatch
{
    [HarmonyPrefix]
    private static bool BuildConfiguredRoutine(
        DynamicRoutineStarter_MultiRumor __instance,
        ref NPCRoutine __result)
    {
        if (!Plugin.CustomizationsEnabled.Value)
            return true;

        __result = GetConfiguredRoutine(__instance)!;
        return false;
    }

    private static NPCRoutine? GetConfiguredRoutine(
        DynamicRoutineStarter_MultiRumor starter)
    {
        NPCRoutineExecutor owner = starter.owner;
        if (owner == null || owner.neuralNPC == null
            || owner.neuralNPC.sleeping
            || !Plugin.RollPercent(
                Plugin.RoutineAttemptChancePercent.Value)
            || !RumorManager.Instance.HasRumor(owner.neuralNPC.npcName))
        {
            return null;
        }

        if (TurfCollider.CanSeePlayer(owner.transform)
            && Plugin.RollPercent(
                Plugin.AvoidPlayerVisibilityChancePercent.Value))
        {
            return null;
        }

        Rumor rumor = RumorManager.Instance.GetRumor(owner.neuralNPC.npcName);
        rumor.targetNPCNames.RemoveAll(
            npcName => !NeuralNPC.neuralNPCs.ContainsKey(npcName));
        List<NPCName> eligible = rumor.targetNPCNames
            .Where(npcName =>
                !NeuralNPC.neuralNPCs[npcName].sleeping
                && owner.IsCloseForMeeting(NeuralNPC.neuralNPCs[npcName])
                && !IFollowingPlayerNPCRoutineArgument
                    .DoesNeuralNPCHaveIFollowingPlayerNPCRoutineArgument(
                        NeuralNPC.neuralNPCs[npcName]))
            .ToList();
        if (eligible.Count == 0)
            return null;

        NPCName target = Plugin.PreferNearestRecipient.Value
            ? eligible.OrderBy(npcName => Vector2Int.Distance(
                owner.transform.GetVector2IntPosition(),
                NeuralNPC.neuralNPCs[npcName]
                    .transform.GetVector2IntPosition())).First()
            : eligible[UnityEngine.Random.Range(0, eligible.Count)];
        int hour = WorldInfoManager.Instance.GetCurrentHourOfDay();
        if (!Plugin.IsDeliveryHourAllowed(target, hour))
            return null;

        rumor.targetNPCNames.Remove(target);
        if (rumor.targetNPCNames.Count == 0)
        {
            RumorManager.Instance.RemoveRumor(
                owner.neuralNPC.npcName,
                rumor);
        }

        NeuralNPC recipient = NeuralNPC.neuralNPCs[target];
        string sourcePossessive = owner.neuralNPC.Pronouns.His();
        return new NPCRoutine(
            $"going to {target} to tell {recipient.Pronouns.Him()}: "
                + rumor.toTell,
            $"telling {target}: {rumor.toTell}",
            $"on {sourcePossessive} way to {target}",
            recipient.transform.GetVector2IntPosition(),
            new RoutineArgument_PassOnRumor(target, rumor.toTell));
    }
}
