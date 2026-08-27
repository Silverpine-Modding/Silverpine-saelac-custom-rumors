#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

internal static class ForcedRumorDelivery
{
    internal const int MaximumTextLength = 1000;
    internal const string RoutineId =
        Plugin.PluginGuid + ".forced-rumor-delivery";
    private static readonly ConditionalWeakTable<Rumor, ManualRumorMarker>
        ManualQueuedRumors = new();

    internal static bool TryQueue(
        NPCName sourceName,
        NPCName targetName,
        string rumorText,
        out string status)
    {
        string normalizedText = NormalizeText(rumorText);
        if (normalizedText.Length == 0)
        {
            status = "Type the rumor before adding it to the queue.";
            return false;
        }
        if (normalizedText.Length > MaximumTextLength)
        {
            status = "Rumors can contain at most " + MaximumTextLength
                + " characters.";
            return false;
        }
        if (sourceName == targetName)
        {
            status = "The sender and recipient must be different NPCs.";
            return false;
        }
        if (RumorManager.Instance == null)
        {
            status = "Load a game before adding a rumor to the queue.";
            return false;
        }
        if (!NeuralNPC.neuralNPCs.TryGetValue(
                sourceName, out NeuralNPC source)
            || source == null)
        {
            status = "The selected sender is not currently loaded.";
            return false;
        }
        if (!NeuralNPC.neuralNPCs.TryGetValue(
                targetName, out NeuralNPC target)
            || target == null)
        {
            status = "The selected recipient is not currently loaded.";
            return false;
        }

        var rumor = new Rumor(
            normalizedText,
            new List<NPCName> { targetName });
        ManualQueuedRumors.Add(rumor, new ManualRumorMarker());
        try
        {
            RumorManager.Instance.AddRumor(sourceName, rumor);
        }
        catch (Exception exception)
        {
            ManualQueuedRumors.Remove(rumor);
            Plugin.Log.LogError(
                "Could not queue a manual rumor from "
                + source.GetFinalName() + " to " + target.GetFinalName()
                + ": " + exception);
            status = "The rumor could not be queued; see the BepInEx log.";
            return false;
        }

        Plugin.Log.LogInfo(
            "Queued manual rumor from " + source.GetFinalName()
            + " to " + target.GetFinalName() + ": \""
            + normalizedText + "\"");
        status = "Queued " + source.GetFinalName() + " to tell "
            + target.GetFinalName() + " the typed rumor.";
        return true;
    }

    internal static bool TryStart(
        NPCName sourceName,
        NPCName targetName,
        string rumorText,
        out string status)
    {
        string normalizedText = NormalizeText(rumorText);
        if (normalizedText.Length == 0)
        {
            status = "Type the rumor before forcing its delivery.";
            return false;
        }
        if (normalizedText.Length > MaximumTextLength)
        {
            status = "Rumors can contain at most " + MaximumTextLength
                + " characters.";
            return false;
        }
        if (sourceName == targetName)
        {
            status = "The sender and recipient must be different NPCs.";
            return false;
        }
        if (Player.Instance == null)
        {
            status = "Load a game before forcing a rumor.";
            return false;
        }
        if (!NeuralNPC.neuralNPCs.TryGetValue(
                sourceName, out NeuralNPC source)
            || source == null)
        {
            status = "The selected sender is not currently loaded.";
            return false;
        }
        if (!NeuralNPC.neuralNPCs.TryGetValue(
                targetName, out NeuralNPC target)
            || target == null)
        {
            status = "The selected recipient is not currently loaded.";
            return false;
        }
        if (ReferenceEquals(
                NeuralNPC.currentActiveDialogNeuralNPC, source)
            || ReferenceEquals(
                NeuralNPC.currentActiveDialogNeuralNPC, target))
        {
            status = "Finish the active conversation involving the sender or "
                + "recipient first.";
            return false;
        }

        NPCRoutineExecutor sourceExecutor =
            source.GetComponent<NPCRoutineExecutor>();
        NPCRoutineExecutor targetExecutor =
            target.GetComponent<NPCRoutineExecutor>();
        if (sourceExecutor == null || sourceExecutor.currentRoutine == null)
        {
            status = source.GetFinalName()
                + " is not ready to start a delivery routine.";
            return false;
        }
        if (targetExecutor == null || targetExecutor.currentRoutine == null)
        {
            status = target.GetFinalName()
                + " is not ready to receive a rumor.";
            return false;
        }
        if (sourceExecutor.GetCurrentAndPreOverrideRoutines().Any(
                routine => routine != null
                    && string.Equals(
                        routine.id, RoutineId,
                        StringComparison.Ordinal)))
        {
            status = source.GetFinalName()
                + " is already carrying a forced rumor.";
            return false;
        }

        string sourceDisplayName = source.GetFinalName();
        string targetDisplayName = target.GetFinalName();
        try
        {
            var argument = new RoutineArgument_PassOnRumor(
                targetName,
                normalizedText);
            var routine = new NPCRoutine(
                "going to " + targetDisplayName + " to tell "
                    + target.Pronouns.Him() + ": " + normalizedText,
                "telling " + targetDisplayName + ": " + normalizedText,
                "on " + source.Pronouns.His() + " way to "
                    + targetDisplayName,
                target.transform.GetVector2IntPosition(),
                argument)
            {
                id = RoutineId
            };
            sourceExecutor.StartOverrideRoutine(routine);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(
                "Could not start a forced rumor from " + sourceDisplayName
                + " to " + targetDisplayName + ": " + exception);
            status = "The forced rumor could not start; see the BepInEx log.";
            return false;
        }

        Plugin.Log.LogInfo(
            "Started forced rumor delivery from " + sourceDisplayName
            + " to " + targetDisplayName + ": \"" + normalizedText + "\"");
        status = "Sent " + sourceDisplayName + " to tell "
            + targetDisplayName + " the typed rumor.";
        return true;
    }

    internal static string NormalizeText(string text) =>
        (text ?? "")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    internal static bool IsManualQueuedRumor(Rumor? rumor) =>
        rumor != null && ManualQueuedRumors.TryGetValue(rumor, out _);

    internal static bool IsForcedRoutine(NPCRoutine? routine) =>
        routine != null
        && string.Equals(routine.id, RoutineId, StringComparison.Ordinal);

    internal static void StopMissingTargetRoutine(NPCRoutine routine)
    {
        Plugin.Log.LogWarning(
            "Stopped a forced rumor because its selected recipient is no "
            + "longer loaded.");
        StopRoutine(routine);
    }

    internal static void Complete(
        NPCRoutine routine,
        NeuralNPC target,
        string rumorText)
    {
        NPCRoutineExecutor sourceExecutor = routine.executor;
        NeuralNPC source = sourceExecutor.neuralNPC;
        NPCRoutineExecutor targetExecutor =
            target.GetComponent<NPCRoutineExecutor>();
        if (source == null || targetExecutor == null
            || targetExecutor.currentRoutine == null)
        {
            return;
        }
        if (ReferenceEquals(
                NeuralNPC.currentActiveDialogNeuralNPC, source)
            || ReferenceEquals(
                NeuralNPC.currentActiveDialogNeuralNPC, target))
        {
            return;
        }

        Vector2Int sourcePosition =
            source.transform.GetVector2IntPosition();
        Vector2Int targetPosition =
            target.transform.GetVector2IntPosition();
        if (!sourcePosition.IsAdjacentOrOnTop(targetPosition))
            return;

        sourceExecutor.GetComponent<EntityMover>().TurnTowards(targetPosition);
        target.GetComponent<EntityMover>().TurnTowards(sourcePosition);
        sourceExecutor.pathfindingEntity.ClearPathAndDeclareSuccess();

        string sourceName = source.GetFinalName();
        string targetName = target.GetFinalName();
        sourceExecutor.StartOverrideRoutine(new NPCRoutine(
            "telling " + targetName + ": \"" + rumorText + "\"",
            "telling " + targetName + ": \"" + rumorText + "\"",
            "telling " + targetName + ": \"" + rumorText + "\"",
            sourcePosition,
            new RoutineArgument_EndOverrideIf_AfterTurns(10)));
        targetExecutor.StartOverrideRoutine(new NPCRoutine(
            "listening to " + sourceName + " tell "
                + target.Pronouns.Him() + ": \"" + rumorText + "\"",
            "listening to " + sourceName + " tell "
                + target.Pronouns.Him() + ": \"" + rumorText + "\"",
            "listening to " + sourceName,
            targetPosition,
            new RoutineArgument_EndOverrideIf_AfterTurns(11)));

        string memory = sourceName + " told " + targetName + ": \""
            + rumorText + "\".";
        source.AddThingThatHappened(
            memory,
            overwrite: false,
            NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);
        target.AddThingThatHappened(
            memory,
            overwrite: false,
            NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);

        if (!routine.routineArguments.Any(argument =>
                argument is RoutineArgument_EndOverrideIf_Overridden))
        {
            StopRoutine(routine);
        }

        Plugin.Log.LogInfo(
            "Completed forced rumor delivery from " + sourceName
            + " to " + targetName + ": \"" + rumorText + "\"");
        NpcRumorReaction.Begin(source, target, rumorText);
        if (UpperNotificationUI.Instance != null)
        {
            UpperNotificationUI.Instance.OneOff(
                sourceName + " delivered the forced rumor to "
                + targetName + ".");
        }
    }

    private static void StopRoutine(NPCRoutine routine)
    {
        NPCRoutineExecutor? executor = routine.executor;
        if (executor == null)
            return;
        if (ReferenceEquals(executor.currentRoutine, routine)
            || executor.preOverrideRoutines.Contains(routine))
        {
            executor.StopOverrideRoutine(routine);
        }
    }

    private sealed class ManualRumorMarker
    {
    }
}

[HarmonyPatch(typeof(RoutineArgument_PassOnRumor), "Check")]
internal static class ForcedRumorCheckPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static bool ReplaceEligibilityChecksForForcedRumor(
        RoutineArgument_PassOnRumor __instance,
        NPCName ___targetNPCName,
        string ___toTell)
    {
        NPCRoutine? routine = __instance.routine;
        if (!ForcedRumorDelivery.IsForcedRoutine(routine))
            return true;

        if (!NeuralNPC.neuralNPCs.TryGetValue(
                ___targetNPCName, out NeuralNPC target)
            || target == null)
        {
            ForcedRumorDelivery.StopMissingTargetRoutine(routine!);
            return false;
        }

        ForcedRumorDelivery.Complete(routine!, target, ___toTell);
        return false;
    }
}
