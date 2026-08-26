#nullable enable

using Silverpine.ModdingTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverpineMods.CustomRumors;

internal sealed class ForcedRumorWindow : ModToolBehaviour
{
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;

    private static ForcedRumorWindow? instance;

    private NPCName? sourceName;
    private NPCName? targetName;
    private string rumorText = "";
    private string status =
        "Choose two loaded NPCs, type the rumor, and force its delivery.";
    private bool open;

    internal static void Open(ModToolSession session)
    {
        if (instance != null)
            instance.Close();

        GameObject root = new("Custom Rumors Forced Delivery IMGUI");
        instance = root.AddComponent<ForcedRumorWindow>();
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

        GUILayout.BeginArea(new Rect(390f, 90f, 1140f, 900f));
        DrawHeader();
        GUILayout.Space(18f);

        List<NeuralNPC> loadedNpcs = GetLoadedNpcs();
        EnsureSelections(loadedNpcs);
        if (loadedNpcs.Count < 2)
        {
            GUILayout.Label(
                "Load a game with at least two NPCs before forcing a rumor.",
                GUI.skin.box,
                GUILayout.MinHeight(90f));
        }
        else
        {
            DrawNpcSelector("NPC passing along the rumor", ref sourceName,
                loadedNpcs);
            GUILayout.Space(10f);
            DrawNpcSelector("NPC receiving the rumor", ref targetName,
                loadedNpcs);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    "Swap Sender and Recipient",
                    GUILayout.Width(280f),
                    GUILayout.Height(48f)))
            {
                (sourceName, targetName) = (targetName, sourceName);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(18f);
        GUILayout.Label(
            "Rumor text typed by the player (maximum "
                + ForcedRumorDelivery.MaximumTextLength + " characters)");
        GUI.SetNextControlName("CustomRumors.ForcedRumorText");
        rumorText = GUILayout.TextArea(
            rumorText,
            ForcedRumorDelivery.MaximumTextLength,
            GUILayout.MinHeight(250f),
            GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            rumorText.Length + " / "
                + ForcedRumorDelivery.MaximumTextLength);
        GUILayout.EndHorizontal();

        GUILayout.Space(15f);
        GUILayout.Label(
            "Forced delivery bypasses automatic chance, time, visibility, "
                + "distance, sleep, and following restrictions. The sender "
                + "will travel to the recipient, then both NPCs receive the "
                + "normal rumor memory. It does not count against the daily "
                + "generation cap.",
            GUI.skin.box,
            GUILayout.MinHeight(82f));

        GUILayout.Space(15f);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && loadedNpcs.Count >= 2;
        if (GUILayout.Button(
                "Force Rumor Delivery",
                GUILayout.Height(64f)))
        {
            StartForcedRumor();
        }
        GUI.enabled = previousEnabled;

        GUILayout.Space(14f);
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label(status, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Close", GUILayout.Width(110f)))
            Close();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private static void DrawHeader()
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Force a Custom Rumor", GUILayout.Width(380f));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Custom Rumors 1.4.0");
        GUILayout.EndHorizontal();
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

    private static void DrawNpcSelector(
        string label,
        ref NPCName? selectedName,
        List<NeuralNPC> loadedNpcs)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(100f));
        GUILayout.Label(label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(70f), GUILayout.Height(52f)))
            CycleSelection(ref selectedName, loadedNpcs, -1);

        NPCName? selection = selectedName;
        NeuralNPC? selected = selection.HasValue
            ? loadedNpcs.FirstOrDefault(
                npc => npc.npcName == selection.Value)
            : null;
        GUILayout.Label(
            selected == null ? "No NPC loaded" : FormatNpc(selected),
            GUI.skin.box,
            GUILayout.ExpandWidth(true),
            GUILayout.Height(52f));

        if (GUILayout.Button(">", GUILayout.Width(70f), GUILayout.Height(52f)))
            CycleSelection(ref selectedName, loadedNpcs, 1);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private static void CycleSelection(
        ref NPCName? selectedName,
        List<NeuralNPC> loadedNpcs,
        int direction)
    {
        if (loadedNpcs.Count == 0)
            return;

        NPCName? selection = selectedName;
        int currentIndex = selection.HasValue
            ? loadedNpcs.FindIndex(
                npc => npc.npcName == selection.Value)
            : -1;
        if (currentIndex < 0)
            currentIndex = 0;
        int nextIndex = (currentIndex + direction) % loadedNpcs.Count;
        if (nextIndex < 0)
            nextIndex += loadedNpcs.Count;
        selectedName = loadedNpcs[nextIndex].npcName;
    }

    private static string FormatNpc(NeuralNPC npc) =>
        npc.GetFinalName() + " [" + npc.npcName + "]"
        + (npc.sleeping ? " - sleeping" : "");

    private void StartForcedRumor()
    {
        if (!sourceName.HasValue || !targetName.HasValue)
        {
            status = "Select both a sender and a recipient.";
            return;
        }

        if (ForcedRumorDelivery.TryStart(
                sourceName.Value,
                targetName.Value,
                rumorText,
                out string result))
        {
            rumorText = "";
        }
        status = result;
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
