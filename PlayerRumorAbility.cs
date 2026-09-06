#nullable enable

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

[HarmonyPatch(typeof(Player), nameof(Player.GetPlayerAbilities))]
internal static class AddTellRumorAbilityPatch
{
    private static bool logged;

    [HarmonyPostfix]
    private static void AddAbility(ref List<PlayerAbility> __result)
    {
        if (!EnsurePresent(__result) || logged)
            return;

        logged = true;
        Plugin.Log.LogInfo(
            "Injected Tell Rumor into Player.GetPlayerAbilities().");
    }

    internal static bool EnsurePresent(List<PlayerAbility> abilities)
    {
        if (abilities.Any(ability => ability is PlayerAbility_TellRumor))
            return false;

        abilities.Add(new PlayerAbility_TellRumor());
        return true;
    }
}

[HarmonyPatch(typeof(RadialMenuUI), nameof(RadialMenuUI.OpenAndDraw))]
internal static class DrawTellRumorInPlayerActionWheelPatch
{
    private static bool logged;

    [HarmonyPrefix]
    private static void AddAbility(
        ref List<ListUIItem_Generic> genericListUIItems)
    {
        if (!LooksLikePlayerActionWheel(genericListUIItems)
            || genericListUIItems.Any(item => item.name == "Tell Rumor"))
        {
            return;
        }

        var ability = new PlayerAbility_TellRumor();
        genericListUIItems.Add(
            new ListUIItem_Generic(
                ability.Name,
                ability.Icon,
                ability.Suffix,
                ability.Callback));

        if (!logged)
        {
            logged = true;
            Plugin.Log.LogInfo(
                "Drew Tell Rumor in the player-action radial wheel.");
        }
    }

    private static bool LooksLikePlayerActionWheel(
        List<ListUIItem_Generic> items)
    {
        return SettingsUI.Instance != null
            && (Input.GetKeyDown(SettingsUI.Instance.abilitiesKeyCode)
                || (items.Any(item => item.name == "Sleep On Ground")
                    && items.Any(item => item.name == "Pass Time")
                    && items.Any(item => item.name == "Say")));
    }
}

internal sealed class PlayerAbility_TellRumor : PlayerAbility
{
    public override string Name => "Tell Rumor";

    protected override string IconPath => "Sprites/Misc/sprite_ui_say";

    protected override void OnUse()
    {
        base.OnUse();
        Targetable.StartTargetingMode(
            IsSelectableNpc,
            OnTargetSelected,
            autoSelectSolitary: true);
    }

    private static bool IsSelectableNpc(GameObject candidate)
    {
        if (candidate.GetComponent<NeuralNPC>() == null)
            return false;

        Vector2Int candidatePosition =
            candidate.transform.GetVector2IntPosition();
        Vector2Int playerPosition =
            Player.Instance.transform.GetVector2IntPosition();
        int range = MapZone.GetPositionZoneName(candidatePosition)
            == MapZone.GetPositionZoneName(playerPosition)
                ? 2
                : 1;
        return Mathf.Abs(candidatePosition.x - playerPosition.x) <= range
            && Mathf.Abs(candidatePosition.y - playerPosition.y) <= range;
    }

    private static void OnTargetSelected(Targetable target)
    {
        if (target == null)
        {
            Notify("There's nobody close enough to tell a rumor.");
            return;
        }

        NeuralNPC npc = target.GetComponent<NeuralNPC>();
        if (npc == null)
        {
            Notify("The selected target cannot receive a rumor.");
            return;
        }
        if (PlayerRumorReaction.IsBusy(npc))
        {
            Notify(npc.GetFinalName()
                + " is still considering the previous rumor.");
            return;
        }
        if (InferenceServerSetupHandler.Instance == null
            || !InferenceServerSetupHandler.Instance.modelLoaded)
        {
            Notify("No AI model has been loaded for the NPC's response.");
            return;
        }
        if (TextInputUI.Instance == null)
        {
            Notify("Silverpine's text input is not ready.");
            return;
        }

        TextInputUI.Instance.Open(
            "What rumor do you want to tell "
                + npc.GetFinalName() + "?",
            text => !string.IsNullOrWhiteSpace(text)
                && ForcedRumorDelivery.NormalizeText(text).Length
                    <= ForcedRumorDelivery.MaximumTextLength,
            text => PlayerRumorReaction.Begin(npc, text));
    }

    private static void Notify(string message)
    {
        if (UpperNotificationUI.Instance != null)
            UpperNotificationUI.Instance.OneOff(message);
    }
}

internal static class PlayerRumorReaction
{
    private static readonly HashSet<NeuralNPC> BusyNpcs = new();

    internal static bool IsBusy(NeuralNPC npc) =>
        npc != null && BusyNpcs.Contains(npc);

    internal static async void Begin(NeuralNPC npc, string rumorText)
    {
        if (npc == null || !BusyNpcs.Add(npc))
            return;

        string rumor = ForcedRumorDelivery.NormalizeText(rumorText);
        string npcName = npc.GetFinalName();
        string playerName = Player.Instance != null
            ? Player.Instance.playerName
            : "The player";
        string receivedMemory = playerName + " told " + npcName
            + " this rumor: \"" + rumor + "\".";
        npc.AddThingThatHappened(
            receivedMemory,
            overwrite: false,
            NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);

        try
        {
            if (AnimationHelper.Instance != null)
            {
                AnimationHelper.Instance.SpawnFloatingText(
                    "Thinking...",
                    npc.transform);
            }

            string prompt = playerName + " just told you this rumor: \""
                + rumor + "\". Respond with exactly one short, first-person, "
                + "in-character sentence that clearly expresses how you feel "
                + "about the matter. Do not add a speaker label, narration, "
                + "stage directions, or a second line.";
            string response = await npc.AskQuestion(
                prompt,
                deterministic: false,
                takes: -1,
                grammar: "root ::= [A-Za-z0-9 .,!?'-]+");
            response = NormalizeResponse(response);
            if (response.Length == 0)
                throw new InvalidOperationException(
                    "The NPC model returned an empty rumor response.");

            string feelingMemory = "After hearing the rumor from "
                + playerName + ", " + npcName
                + " expressed this feeling about the matter: \""
                + response + "\".";
            npc.AddThingThatHappened(
                feelingMemory,
                overwrite: false,
                NeuralNPC.AdvancedMemoryElement.MemoryType.Rumor);

            if (UpperNotificationUI.Instance != null)
            {
                UpperNotificationUI.Instance.OneOff(
                    npcName + ": \"" + response + "\"");
            }
            Plugin.Log.LogInfo(
                playerName + " told " + npcName + " a rumor; response: \""
                + response + "\"");
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(
                "Could not generate " + npcName
                + "'s response to the player rumor: " + exception);
            if (UpperNotificationUI.Instance != null)
            {
                UpperNotificationUI.Instance.OneOff(
                    npcName + " heard the rumor, but no response was "
                    + "generated.");
            }
        }
        finally
        {
            BusyNpcs.Remove(npc);
        }
    }

    private static string NormalizeResponse(string response)
    {
        string normalized = ForcedRumorDelivery.NormalizeText(response);
        if (normalized.Length >= 2
            && normalized[0] == '"'
            && normalized[^1] == '"')
        {
            normalized = normalized.Substring(1, normalized.Length - 2)
                .Trim();
        }
        return normalized.Length <= 400
            ? normalized
            : normalized.Substring(0, 400).TrimEnd();
    }
}
