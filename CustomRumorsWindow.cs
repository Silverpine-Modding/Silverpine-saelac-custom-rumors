#nullable enable

using BepInEx.Configuration;
using Silverpine.ModdingTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

internal sealed class CustomRumorsWindow : ModToolBehaviour
{
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;

    private static CustomRumorsWindow? instance;

    private readonly Vector2 dropdownSize = new(0f, 280f);
    private Vector2 settingsScrollPosition;
    private Vector2 composerScrollPosition;
    private Vector2 dropdownScrollPosition;
    private NPCName? sourceName;
    private NPCName? targetName;
    private string rumorText = "";
    private string dropdownSearch = "";
    private string settingsStatus =
        "Changes apply immediately and are saved to the CFG.";
    private string composerStatus =
        "Choose a sender, recipient, delivery mode, and rumor text.";
    private WindowTab activeTab = WindowTab.Settings;
    private DeliveryMode deliveryMode = DeliveryMode.AddToQueue;
    private OpenDropdown openDropdown;
    private bool open;

    internal static void Open(ModToolSession session)
    {
        if (instance != null)
            instance.Close();

        GameObject root = new("Custom Rumors IMGUI");
        instance = root.AddComponent<CustomRumorsWindow>();
        instance.AttachSession(session);
        instance.open = true;
    }

    private void Update()
    {
        if (open && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    private void OnGUI()
    {
        if (!open)
            return;

        GUI.enabled = true;
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.depth = -1000;

        using ModGuiScope scope = ModGui.BeginScaled(
            DesignWidth,
            DesignHeight);
        Color oldColor = GUI.color;
        GUI.color = new Color(0.055f, 0.045f, 0.075f, 0.995f);
        GUI.DrawTexture(
            new Rect(0f, 0f, DesignWidth, DesignHeight),
            Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUILayout.BeginArea(
            new Rect(300f, 65f, DesignWidth - 600f, DesignHeight - 130f));
        DrawHeader();
        GUILayout.Space(10f);
        DrawTabs();
        GUILayout.Space(10f);

        if (activeTab == WindowTab.Settings)
            DrawSettings();
        else
            DrawComposer();

        DrawFooter();
        GUILayout.EndArea();
    }

    private static void DrawHeader()
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Custom Rumors", GUILayout.Width(300f));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Version " + Plugin.PluginVersion);
        GUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(
                activeTab == WindowTab.Settings
                    ? "● Settings"
                    : "Settings",
                GUILayout.Height(58f)))
        {
            activeTab = WindowTab.Settings;
            CloseDropdown();
        }
        if (GUILayout.Button(
                activeTab == WindowTab.ComposeRumor
                    ? "● Rumor Composer"
                    : "Rumor Composer",
                GUILayout.Height(58f)))
        {
            activeTab = WindowTab.ComposeRumor;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        settingsScrollPosition.x = 0f;
        settingsScrollPosition = GUILayout.BeginScrollView(
            settingsScrollPosition,
            false,
            true,
            GUIStyle.none,
            GUI.skin.verticalScrollbar,
            GUI.skin.box,
            GUILayout.ExpandHeight(true));
        settingsScrollPosition.x = 0f;
        DrawToggle(
            "Customizations",
            "Master switch. Off restores all vanilla rumor behavior without "
                + "discarding these saved settings.",
            Plugin.CustomizationsEnabled);
        DrawToggle(
            "Rumor Generation",
            "Allow conversations to create new rumors. Queued rumors remain "
                + "eligible for delivery.",
            Plugin.RumorGenerationEnabled);
        DrawInteger(
            "Maximum Rumors Per NPC Per Day",
            "Maximum new rumors each NPC can choose. -1 is unlimited, 0 is "
                + "disabled, and 1 is vanilla.",
            Plugin.MaximumRumorsPerNpcPerDay,
            -1,
            100,
            1,
            " / day",
            "Disabled",
            "Unlimited");
        DrawToggle(
            "Cross-Faction Recipients",
            "Let newly generated rumors retain targets from other factions.",
            Plugin.AllowCrossFactionRumors);
        DrawInteger(
            "Routine Attempt Chance",
            "Chance per eligible multi-rumor routine evaluation. Vanilla: 10%.",
            Plugin.RoutineAttemptChancePercent,
            0,
            100,
            5,
            "%");
        DrawInteger(
            "Postpone While Player Is Visible",
            "Chance to defer an attempt when the source NPC can see the player. "
                + "Vanilla: 95%.",
            Plugin.AvoidPlayerVisibilityChancePercent,
            0,
            100,
            5,
            "%");
        DrawInteger(
            "Earliest Delivery Hour",
            "Inclusive 24-hour clock value. Set later than the latest hour for "
                + "an overnight window.",
            Plugin.EarliestDeliveryHour,
            0,
            23,
            1,
            ":00");
        DrawInteger(
            "Latest Delivery Hour",
            "Inclusive 24-hour clock value. Vanilla: 22:00.",
            Plugin.LatestDeliveryHour,
            0,
            23,
            1,
            ":00");
        DrawToggle(
            "Darian Ignores Earliest Hour",
            "Preserve Silverpine's exception that lets Darian deliver before "
                + "the normal start hour.",
            Plugin.DarianIgnoresEarliestHour);
        DrawToggle(
            "Prefer Nearest Recipient",
            "Choose the closest eligible target. Off chooses a random eligible "
                + "recipient.",
            Plugin.PreferNearestRecipient);
        DrawToggle(
            "NPC Reactions to NPC Rumors",
            "Let the receiving NPC privately generate one line describing "
                + "their feeling about an NPC-delivered rumor. The reaction "
                + "is saved to memory and the BepInEx log only.",
            Plugin.NpcRumorReactionsEnabled);
        GUILayout.EndScrollView();
    }

    private void DrawComposer()
    {
        List<NeuralNPC> loadedNpcs = GetLoadedNpcs();
        EnsureSelections(loadedNpcs);

        composerScrollPosition.x = 0f;
        composerScrollPosition = GUILayout.BeginScrollView(
            composerScrollPosition,
            false,
            true,
            GUIStyle.none,
            GUI.skin.verticalScrollbar,
            GUI.skin.box,
            GUILayout.ExpandHeight(true));
        composerScrollPosition.x = 0f;
        if (loadedNpcs.Count < 2)
        {
            GUILayout.Label(
                "Load a game with at least two NPCs before composing an NPC "
                    + "rumor.",
                GUI.skin.box,
                GUILayout.MinHeight(80f));
        }
        else
        {
            DrawNpcDropdown(
                "NPC passing along the rumor",
                ref sourceName,
                loadedNpcs,
                OpenDropdown.Source);
            GUILayout.Space(10f);
            DrawNpcDropdown(
                "NPC receiving the rumor",
                ref targetName,
                loadedNpcs,
                OpenDropdown.Target);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    "Swap Sender and Recipient",
                    GUILayout.Width(280f),
                    GUILayout.Height(48f)))
            {
                (sourceName, targetName) = (targetName, sourceName);
                CloseDropdown();
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(15f);
        GUILayout.Label("Delivery mode");
        DrawDeliveryModeDropdown();

        GUILayout.Space(15f);
        GUILayout.Label(
            "Rumor text typed by the player (maximum "
                + ForcedRumorDelivery.MaximumTextLength + " characters)");
        GUI.SetNextControlName("CustomRumors.ComposerText");
        GUIStyle textAreaStyle = new(GUI.skin.textArea)
        {
            wordWrap = true
        };
        rumorText = GUILayout.TextArea(
            rumorText,
            ForcedRumorDelivery.MaximumTextLength,
            textAreaStyle,
            GUILayout.MinHeight(220f),
            GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            rumorText.Length + " / "
                + ForcedRumorDelivery.MaximumTextLength);
        GUILayout.EndHorizontal();

        GUILayout.Space(12f);
        string explanation = deliveryMode == DeliveryMode.AddToQueue
            ? "Queue mode stores the rumor for normal scheduled delivery. It "
                + "uses the configured delivery rules but does not consume the "
                + "sender's automatic daily generation cap."
            : "Forced mode immediately sends the sender toward the recipient "
                + "and bypasses automatic chance, time, visibility, distance, "
                + "sleep, and following restrictions for this delivery.";
        GUILayout.Label(
            explanation,
            WrappedBoxStyle(),
            GUILayout.MinHeight(80f));

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && loadedNpcs.Count >= 2;
        if (GUILayout.Button("Create Rumor", GUILayout.Height(64f)))
            SubmitRumor();
        GUI.enabled = previousEnabled;

        GUILayout.Space(14f);
        GUILayout.Label(
            "Player action: choose Tell Rumor from the inventory Actions menu "
                + "or the U radial wheel, select a nearby NPC, and enter the "
                + "rumor in Silverpine's standalone text prompt. No dialogue "
                + "is opened. The NPC's one-line reaction and feeling are "
                + "saved with the rumor.",
            WrappedBoxStyle(),
            GUILayout.MinHeight(90f));
        GUILayout.EndScrollView();
    }

    private void DrawNpcDropdown(
        string label,
        ref NPCName? selectedName,
        List<NeuralNPC> loadedNpcs,
        OpenDropdown dropdown)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(label);
        NPCName? selection = selectedName;
        NeuralNPC? selected = selection.HasValue
            ? loadedNpcs.FirstOrDefault(
                npc => npc.npcName == selection.Value)
            : null;
        string selectedLabel = selected == null
            ? "Select an NPC ▼"
            : FormatNpc(selected) + " ▼";
        if (GUILayout.Button(
                selectedLabel,
                WrappedButtonStyle(),
                GUILayout.Height(56f),
                GUILayout.ExpandWidth(true)))
        {
            if (openDropdown == dropdown)
            {
                CloseDropdown();
            }
            else
            {
                openDropdown = dropdown;
                dropdownSearch = "";
                dropdownScrollPosition = Vector2.zero;
            }
        }

        if (openDropdown == dropdown)
        {
            GUILayout.Label("Search NPCs");
            dropdownSearch = GUILayout.TextField(dropdownSearch);
            IEnumerable<NeuralNPC> matches = loadedNpcs;
            if (!string.IsNullOrWhiteSpace(dropdownSearch))
            {
                string search = dropdownSearch.Trim();
                matches = matches.Where(npc =>
                    FormatNpc(npc).IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) >= 0);
            }

            dropdownScrollPosition.x = 0f;
            dropdownScrollPosition = GUILayout.BeginScrollView(
                dropdownScrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUI.skin.box,
                GUILayout.Height(dropdownSize.y));
            dropdownScrollPosition.x = 0f;
            bool found = false;
            foreach (NeuralNPC npc in matches)
            {
                found = true;
                string optionLabel = npc.npcName == selectedName
                    ? "● " + FormatNpc(npc)
                    : FormatNpc(npc);
                if (GUILayout.Button(
                        optionLabel,
                        WrappedButtonStyle(),
                        GUILayout.Height(46f),
                        GUILayout.ExpandWidth(true)))
                {
                    selectedName = npc.npcName;
                    CloseDropdown();
                }
            }
            if (!found)
                GUILayout.Label("No loaded NPCs match that search.");
            GUILayout.EndScrollView();
        }
        GUILayout.EndVertical();
    }

    private void DrawDeliveryModeDropdown()
    {
        string selectedLabel = deliveryMode == DeliveryMode.AddToQueue
            ? "Add to Rumor Queue ▼"
            : "Force Immediate Delivery ▼";
        if (GUILayout.Button(
                selectedLabel,
                WrappedButtonStyle(),
                GUILayout.Height(56f),
                GUILayout.ExpandWidth(true)))
        {
            if (openDropdown == OpenDropdown.DeliveryMode)
            {
                CloseDropdown();
            }
            else
            {
                openDropdown = OpenDropdown.DeliveryMode;
                dropdownSearch = "";
                dropdownScrollPosition = Vector2.zero;
            }
        }

        if (openDropdown != OpenDropdown.DeliveryMode)
            return;

        if (deliveryMode != DeliveryMode.AddToQueue
            && GUILayout.Button(
                "Add to Rumor Queue",
                WrappedButtonStyle(),
                GUILayout.Height(48f),
                GUILayout.ExpandWidth(true)))
        {
            deliveryMode = DeliveryMode.AddToQueue;
            CloseDropdown();
        }
        if (deliveryMode != DeliveryMode.ForceImmediately
            && GUILayout.Button(
                "Force Immediate Delivery",
                WrappedButtonStyle(),
                GUILayout.Height(48f),
                GUILayout.ExpandWidth(true)))
        {
            deliveryMode = DeliveryMode.ForceImmediately;
            CloseDropdown();
        }
    }

    private static List<NeuralNPC> GetLoadedNpcs()
    {
        if (NeuralNPC.neuralNPCs == null)
            return new List<NeuralNPC>();

        return NeuralNPC.neuralNPCs.Values
            .Where(npc => npc != null)
            .GroupBy(npc => npc.npcName)
            .Select(group => group.First())
            .OrderBy(
                npc => npc.GetFinalName(),
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnsureSelections(List<NeuralNPC> loadedNpcs)
    {
        if (loadedNpcs.Count == 0)
        {
            sourceName = null;
            targetName = null;
            return;
        }

        if (!sourceName.HasValue
            || loadedNpcs.All(npc => npc.npcName != sourceName.Value))
        {
            sourceName = loadedNpcs[0].npcName;
        }
        if (!targetName.HasValue
            || loadedNpcs.All(npc => npc.npcName != targetName.Value))
        {
            targetName = loadedNpcs
                .FirstOrDefault(npc => npc.npcName != sourceName.Value)
                ?.npcName
                ?? loadedNpcs[0].npcName;
        }
    }

    private static string FormatNpc(NeuralNPC npc) =>
        npc.GetFinalName() + " [" + npc.npcName + "]"
        + (npc.sleeping ? " - sleeping" : "");

    private void SubmitRumor()
    {
        if (!sourceName.HasValue || !targetName.HasValue)
        {
            composerStatus = "Select both a sender and a recipient.";
            return;
        }

        bool succeeded = deliveryMode == DeliveryMode.AddToQueue
            ? ForcedRumorDelivery.TryQueue(
                sourceName.Value,
                targetName.Value,
                rumorText,
                out string result)
            : ForcedRumorDelivery.TryStart(
                sourceName.Value,
                targetName.Value,
                rumorText,
                out result);
        composerStatus = result;
        if (succeeded)
            rumorText = "";
    }

    private void DrawFooter()
    {
        string status = activeTab == WindowTab.Settings
            ? settingsStatus
            : composerStatus;
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(
            status,
            WrappedLabelStyle(),
            GUILayout.ExpandWidth(true));
        if (activeTab == WindowTab.Settings)
        {
            if (GUILayout.Button("Reload CFG", GUILayout.Width(125f)))
                Reload();
            if (GUILayout.Button(
                    "Vanilla Defaults",
                    GUILayout.Width(160f)))
            {
                RestoreVanillaDefaults();
            }
        }
        if (GUILayout.Button("Close", GUILayout.Width(100f)))
            Close();
        GUILayout.EndHorizontal();
    }

    private void DrawToggle(
        string label,
        string description,
        ConfigEntry<bool> setting)
    {
        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(80f));
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label(label);
        GUILayout.Label(description, WrappedLabelStyle());
        GUILayout.EndVertical();
        string buttonLabel = setting.Value ? "On" : "Off";
        if (GUILayout.Button(
                buttonLabel,
                GUILayout.Width(115f),
                GUILayout.MinHeight(52f)))
        {
            setting.Value = !setting.Value;
            Save(label + " set to " + (setting.Value ? "On." : "Off."));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawInteger(
        string label,
        string description,
        ConfigEntry<int> setting,
        int minimum,
        int maximum,
        int step,
        string suffix,
        string? zeroLabel = null,
        string? negativeOneLabel = null)
    {
        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(80f));
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label(label);
        GUILayout.Label(description, WrappedLabelStyle());
        GUILayout.EndVertical();
        if (GUILayout.Button(
                "-",
                GUILayout.Width(58f),
                GUILayout.MinHeight(52f)))
        {
            setting.Value = Mathf.Clamp(setting.Value - step, minimum, maximum);
            Save(label + " set to "
                + FormatInteger(
                    setting.Value, suffix, zeroLabel, negativeOneLabel) + ".");
        }
        GUILayout.Label(
            FormatInteger(
                setting.Value, suffix, zeroLabel, negativeOneLabel),
            GUI.skin.box,
            GUILayout.Width(105f),
            GUILayout.MinHeight(52f));
        if (GUILayout.Button(
                "+",
                GUILayout.Width(58f),
                GUILayout.MinHeight(52f)))
        {
            setting.Value = Mathf.Clamp(setting.Value + step, minimum, maximum);
            Save(label + " set to "
                + FormatInteger(
                    setting.Value, suffix, zeroLabel, negativeOneLabel) + ".");
        }
        GUILayout.EndHorizontal();
    }

    private static string FormatInteger(
        int value,
        string suffix,
        string? zeroLabel,
        string? negativeOneLabel) =>
        value == -1 && !string.IsNullOrWhiteSpace(negativeOneLabel)
            ? negativeOneLabel!
            : value == 0 && !string.IsNullOrWhiteSpace(zeroLabel)
            ? zeroLabel!
            : value + suffix;

    private void Save(string message)
    {
        Plugin.Settings.Save();
        settingsStatus = message;
    }

    private void Reload()
    {
        Plugin.Settings.Reload();
        settingsStatus = "Reloaded the CFG; values apply immediately.";
    }

    private void RestoreVanillaDefaults()
    {
        Plugin.CustomizationsEnabled.Value = true;
        Plugin.RumorGenerationEnabled.Value = true;
        Plugin.MaximumRumorsPerNpcPerDay.Value = 1;
        Plugin.AllowCrossFactionRumors.Value = false;
        Plugin.RoutineAttemptChancePercent.Value = 10;
        Plugin.AvoidPlayerVisibilityChancePercent.Value = 95;
        Plugin.EarliestDeliveryHour.Value = 8;
        Plugin.LatestDeliveryHour.Value = 22;
        Plugin.DarianIgnoresEarliestHour.Value = true;
        Plugin.PreferNearestRecipient.Value = true;
        Plugin.NpcRumorReactionsEnabled.Value = false;
        Save("Restored Silverpine-equivalent rumor defaults.");
    }

    private void CloseDropdown()
    {
        openDropdown = OpenDropdown.None;
        dropdownSearch = "";
        dropdownScrollPosition = Vector2.zero;
    }

    private static GUIStyle WrappedLabelStyle() =>
        new(GUI.skin.label)
        {
            wordWrap = true,
            stretchWidth = true
        };

    private static GUIStyle WrappedBoxStyle() =>
        new(GUI.skin.box)
        {
            wordWrap = true,
            stretchWidth = true
        };

    private static GUIStyle WrappedButtonStyle() =>
        new(GUI.skin.button)
        {
            wordWrap = true,
            stretchWidth = true
        };

    private void Close()
    {
        if (!open)
            return;

        open = false;
        ReleaseSession();
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(instance, this))
            instance = null;
        base.OnDestroy();
    }

    private enum WindowTab
    {
        Settings,
        ComposeRumor
    }

    private enum DeliveryMode
    {
        AddToQueue,
        ForceImmediately
    }

    private enum OpenDropdown
    {
        None,
        Source,
        Target,
        DeliveryMode
    }
}
