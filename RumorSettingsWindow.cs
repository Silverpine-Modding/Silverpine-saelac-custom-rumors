#nullable enable

using BepInEx.Configuration;
using Silverpine.ModdingTools;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

internal sealed class RumorSettingsWindow : ModToolBehaviour
{
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;

    private static RumorSettingsWindow? instance;

    private Vector2 scrollPosition;
    private string status = "Changes apply immediately and are saved to the CFG.";
    private bool open;

    internal static void Open(ModToolSession session)
    {
        if (instance != null)
            instance.Close();

        GameObject root = new("Custom Rumors Settings IMGUI");
        instance = root.AddComponent<RumorSettingsWindow>();
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
        GUILayout.Space(12f);

        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            GUI.skin.box,
            GUILayout.ExpandHeight(true));
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
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(status, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Reload CFG", GUILayout.Width(125f)))
            Reload();
        if (GUILayout.Button("Vanilla Defaults", GUILayout.Width(160f)))
            RestoreVanillaDefaults();
        if (GUILayout.Button("Close", GUILayout.Width(100f)))
            Close();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private static void DrawHeader()
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Custom Rumors", GUILayout.Width(300f));
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            "BepInEx/config/renegadex.silverpine.customrumors.cfg");
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
        GUILayout.Label(description);
        GUILayout.EndVertical();
        string buttonLabel = setting.Value ? "On" : "Off";
        if (GUILayout.Button(buttonLabel, GUILayout.Width(115f),
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
        GUILayout.Label(description);
        GUILayout.EndVertical();
        if (GUILayout.Button("-", GUILayout.Width(58f),
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
        if (GUILayout.Button("+", GUILayout.Width(58f),
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
        status = message;
    }

    private void Reload()
    {
        Plugin.Settings.Reload();
        status = "Reloaded the CFG; values apply immediately.";
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
        Save("Restored Silverpine-equivalent rumor defaults.");
    }

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
}
