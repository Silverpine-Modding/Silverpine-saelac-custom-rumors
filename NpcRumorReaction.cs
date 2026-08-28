#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

internal static class NpcRumorReaction
{
    private const int MaximumAttempts = 2;
    private static readonly HashSet<NeuralNPC> BusyRecipients = new();
    private static readonly FieldInfo? VerbosityField =
        AccessTools.Field(typeof(NeuralNPC), "verbosity");
    private static readonly MethodInfo? IsVerbosityLimitedMethod =
        AccessTools.Method(typeof(NeuralNPC), "IsVerbosityLimited");

    internal static async void Begin(
        NeuralNPC source,
        NeuralNPC recipient,
        string rumorText)
    {
        if (!Plugin.CustomizationsEnabled.Value
            || !Plugin.NpcRumorReactionsEnabled.Value
            || source == null
            || recipient == null)
        {
            return;
        }

        string rumor = ForcedRumorDelivery.NormalizeText(rumorText);
        if (rumor.Length == 0)
            return;

        string sourceName = source.GetFinalName();
        string recipientName = recipient.GetFinalName();
        if (!BusyRecipients.Add(recipient))
        {
            Plugin.Log.LogWarning(
                "Skipped NPC rumor reaction because " + recipientName
                + " is already considering another NPC rumor.");
            return;
        }

        try
        {
            int? normalConversationWordCap =
                GetNormalConversationWordCap(recipient);
            string response = "";
            string rejectionReason = "no response was generated";
            for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                string rawResponse = await recipient.AskQuestion(
                    BuildPrompt(
                        sourceName,
                        recipientName,
                        rumor,
                        normalConversationWordCap,
                        attempt > 1),
                    deterministic: false,
                    takes: -1,
                    grammar: "root ::= [A-Za-z0-9 .,!?'-]+",
                    targetDialogElements: new List<NeuralNPC.DialogElement>(),
                    isAgenticLoop: true);
                if (TryNormalizeResponse(
                        rawResponse,
                        recipientName,
                        normalConversationWordCap,
                        out response,
                        out rejectionReason))
                {
                    break;
                }

                Plugin.Log.LogWarning(
                    "Rejected NPC rumor reaction attempt " + attempt + "/"
                    + MaximumAttempts + " for " + recipientName + ": "
                    + rejectionReason + ". Raw response: \""
                    + NormalizeForLog(rawResponse) + "\"");
            }

            if (response.Length == 0)
            {
                throw new InvalidOperationException(
                    "The NPC model did not return a valid private "
                    + "reaction after " + MaximumAttempts + " attempts ("
                    + rejectionReason + ").");
            }

            string feelingMemory = "After hearing the rumor from "
                + sourceName + ", " + recipientName
                + " expressed this feeling about the matter: \""
                + response + "\".";
            recipient.AddThingThatHappened(
                feelingMemory,
                overwrite: false,
                NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);
            source.AddThingThatHappened(
                feelingMemory,
                overwrite: false,
                NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);

            Plugin.Log.LogInfo(
                "NPC rumor reaction: " + recipientName + " reacted to "
                + sourceName + "'s rumor \"" + rumor + "\" with \""
                + response + "\"");
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(
                "Could not generate " + recipientName
                + "'s private reaction to " + sourceName + "'s rumor: "
                + exception);
        }
        finally
        {
            BusyRecipients.Remove(recipient);
        }
    }

    private static string BuildPrompt(
        string sourceName,
        string recipientName,
        string rumor,
        int? normalConversationWordCap,
        bool retry)
    {
        return "This is a private NPC-to-NPC exchange. " + sourceName
            + " privately told " + recipientName + " this rumor: \""
            + rumor + "\". Write " + recipientName
            + "'s immediate response directly to " + sourceName
            + ". Only these two NPCs hear the exchange. Regardless of who may "
            + "be nearby, the player and all other people are not participants "
            + "and must never be mentioned, addressed, shown watching, or "
            + "described as overhearing it. Output a concise first-person "
            + "response. Output only the spoken response: no "
            + "narration, actions, asterisks, quotation marks, speaker label, "
            + "or additional lines."
            + (normalConversationWordCap.HasValue
                ? " Use Silverpine's normal conversation limit for "
                    + recipientName + ": no more than "
                    + normalConversationWordCap.Value + " words."
                : "")
            + (retry
                ? " Your previous answer broke these formatting or privacy "
                    + "rules; answer again using only the required response."
                : "");
    }

    private static bool TryNormalizeResponse(
        string rawResponse,
        string recipientName,
        int? normalConversationWordCap,
        out string response,
        out string rejectionReason)
    {
        response = "";
        rejectionReason = "the response was empty";
        if (string.IsNullOrWhiteSpace(rawResponse))
            return false;

        string normalized = ForcedRumorDelivery.NormalizeText(rawResponse);
        normalized = Regex.Replace(normalized, @"\*[^*]*\*", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = Regex.Replace(
            normalized,
            @"^" + Regex.Escape(recipientName) + @"\s*:\s*",
            "",
            RegexOptions.IgnoreCase).Trim();
        if (normalized.Length >= 2
            && ((normalized[0] == '"' && normalized[^1] == '"')
                || (normalized[0] == '“' && normalized[^1] == '”')))
        {
            normalized = normalized.Substring(1, normalized.Length - 2)
                .Trim();
        }

        if (normalized.Length == 0)
            return false;
        if (normalized.Contains('*')
            || normalized.Contains('[')
            || normalized.Contains(']'))
        {
            rejectionReason = "it still contained narration or stage directions";
            return false;
        }
        if (ContainsObserverLanguage(normalized))
        {
            rejectionReason = "it implied that the player or another observer heard the exchange";
            return false;
        }
        if (!Regex.IsMatch(
                normalized,
                @"\b(I|I'm|I've|I'd|I'll|me|my|mine|myself)\b",
                RegexOptions.IgnoreCase))
        {
            rejectionReason = "it was not a first-person reaction";
            return false;
        }

        if (normalConversationWordCap.HasValue)
        {
            int wordCount = Regex.Matches(normalized, @"\S+").Count;
            if (wordCount > normalConversationWordCap.Value)
            {
                rejectionReason = "it exceeded Silverpine's normal "
                    + normalConversationWordCap.Value
                    + "-word conversation limit for this NPC";
                return false;
            }
        }

        response = normalized;
        rejectionReason = "";
        return true;
    }

    private static bool ContainsObserverLanguage(string response) =>
        Regex.IsMatch(
            response,
            @"\b(overhear(?:d|ing|s)?|observer|onlooker|bystander|stranger|"
                + @"spectator|the player|others? (?:can |could |did |might )?"
                + @"hear(?:d|s|ing)?|(?:someone|somebody|anyone|anybody|"
                + @"no one|nobody) (?:else )?(?:can |could |did |might |may |"
                + @"has |have )?hear(?:d|s|ing)?)\b",
            RegexOptions.IgnoreCase);

    private static int? GetNormalConversationWordCap(NeuralNPC recipient)
    {
        try
        {
            bool limited = IsVerbosityLimitedMethod?.Invoke(null, null)
                is true;
            if (limited
                && VerbosityField?.GetValue(recipient) is int verbosity
                && verbosity > 0)
            {
                return verbosity;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                "Could not read Silverpine's normal conversation cap for "
                + recipient.GetFinalName() + ": " + exception.Message);
        }

        return null;
    }

    private static string NormalizeForLog(string response)
    {
        string normalized = ForcedRumorDelivery.NormalizeText(response);
        return normalized.Length <= 300
            ? normalized
            : normalized.Substring(0, 300).TrimEnd() + "...";
    }
}

[HarmonyPatch(typeof(RoutineArgument_PassOnRumor), "Check")]
internal static class NpcRumorReactionPatch
{
    private static readonly ConditionalWeakTable<
        RoutineArgument_PassOnRumor, ReactionMarker> ReactedDeliveries = new();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void CaptureSuccessfulDelivery(
        RoutineArgument_PassOnRumor __instance,
        NPCName ___targetNPCName,
        string ___toTell,
        out ReactionCandidate? __state)
    {
        __state = null;
        if (!Plugin.CustomizationsEnabled.Value
            || !Plugin.NpcRumorReactionsEnabled.Value
            || ReactedDeliveries.TryGetValue(__instance, out _))
        {
            return;
        }

        NPCRoutine? routine = __instance.routine;
        if (routine == null
            || ForcedRumorDelivery.IsForcedRoutine(routine)
            || routine.executor == null)
        {
            return;
        }

        NPCRoutineExecutor sourceExecutor = routine.executor;
        NeuralNPC source = sourceExecutor.neuralNPC;
        if (source == null
            || !NeuralNPC.neuralNPCs.TryGetValue(
                ___targetNPCName, out NeuralNPC recipient)
            || recipient == null
            || recipient.sleeping
            || !sourceExecutor.IsCloseForMeeting(recipient)
            || IFollowingPlayerNPCRoutineArgument
                .DoesNeuralNPCHaveIFollowingPlayerNPCRoutineArgument(
                    recipient))
        {
            return;
        }

        Vector2Int sourcePosition =
            sourceExecutor.transform.GetVector2IntPosition();
        Vector2Int recipientPosition =
            recipient.transform.GetVector2IntPosition();
        if (!sourcePosition.IsAdjacentOrOnTop(recipientPosition))
            return;

        NPCRoutineExecutor recipientExecutor =
            recipient.GetComponent<NPCRoutineExecutor>();
        if (recipientExecutor == null
            || recipientExecutor.currentRoutine == null
            || recipientExecutor.currentRoutine
                .GetRoutineArgument<RoutineArgument_EndOverrideIf_Overridden>()
                != null)
        {
            return;
        }

        __state = new ReactionCandidate(source, recipient, ___toTell);
    }

    [HarmonyPostfix]
    private static void StartReactionAfterDelivery(
        RoutineArgument_PassOnRumor __instance,
        ReactionCandidate? __state)
    {
        if (__state == null
            || ReactedDeliveries.TryGetValue(__instance, out _))
        {
            return;
        }

        ReactedDeliveries.Add(__instance, new ReactionMarker());
        NpcRumorReaction.Begin(
            __state.Source,
            __state.Recipient,
            __state.RumorText);
    }

    private sealed class ReactionCandidate
    {
        internal ReactionCandidate(
            NeuralNPC source,
            NeuralNPC recipient,
            string rumorText)
        {
            Source = source;
            Recipient = recipient;
            RumorText = rumorText;
        }

        internal NeuralNPC Source { get; }
        internal NeuralNPC Recipient { get; }
        internal string RumorText { get; }
    }

    private sealed class ReactionMarker
    {
    }
}
