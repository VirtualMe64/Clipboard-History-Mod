using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using PolyTechFramework;

namespace ClipboardHistory
{
    [BepInPlugin("org.bepinex.plugins.clipboardhistory", "Clipboard History Mod", "1.0.1")]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class ClipboardHistory: PolyTechMod
    {
        public static ConfigEntry<bool> mEnabled;
        public static ConfigEntry<int> maxHistorySize;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> keybindHistoryBack;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> keybindHistoryForward;   

        public static List<List<ClipboardEdge>> saved_Edges;
        public static List<List<ClipboardJoint>> saved_Joints;
        public static int clipboard_History_Index;

        public static ConfigDefinition mEnabledDefinition = new ConfigDefinition("Clipboard History Mod", "Enable/Disable Mod");

        public ClipboardHistory() {
            Config.Bind(mEnabledDefinition, true, new ConfigDescription("Controls if the mod should be enabled or disabled", null, null));
            keybindHistoryForward = Config.Bind(new ConfigDefinition("History Settings", "History Forward"), new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.U),
                                                new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));            
            keybindHistoryBack = Config.Bind(new ConfigDefinition("History Settings", "History Back"), new BepInEx.Configuration.KeyboardShortcut(UnityEngine.KeyCode.J),
                                             new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
            maxHistorySize = Config.Bind(new ConfigDefinition("History Settings", "Max History Size"), 20, 
                                         new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));            
        }

        void Awake()
        {
            mEnabled = (ConfigEntry<bool>)Config[mEnabledDefinition];
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            this.isCheat = false;
            // Set this to whether the mod is currently enabled or not.
            // Usually you want this to be true by default.
            this.isEnabled = true;

            saved_Edges = new List<List<ClipboardEdge>>();
            saved_Joints = new List<List<ClipboardJoint>>();
            clipboard_History_Index = 0;

            // Register the mod to PTF, that way it will be able to be configured using PTF ingame.
            PolyTechMain.registerMod(this);
            Logger.LogInfo("Clipboard History mod registered");

            // Implement code changes
            Harmony.CreateAndPatchAll(typeof(ClipboardHistory));
        }

        // Use this method to execute code that will be ran when the mod is enabled.
        public override void enableMod() {
            mEnabled.Value = true;
        }
        // Use this method to execute code that will be ran when the mod is disabled.
        public override void disableMod() {
            mEnabled.Value = false;
        }

        // I have no idea how either of this functions work,
        // so just talk to MoonlitJolty if you wanna know what to do this this.
        // This returns a stringified version of the current mod settings.
        public override string getSettings() { return ""; }
        // This takes a stringified version of the mod settings and updates the settings to that.
        public override void setSettings(string settings) {}
        
        // Add list of bridge edges to clipboard history
        private static void updateEdges(List<ClipboardEdge> newEdges) {
            List<ClipboardEdge> listClone = new List<ClipboardEdge>();
            foreach (ClipboardEdge edge in newEdges) {
                listClone.Add(edge);
            }
            saved_Edges.Add(listClone);
        }

        // Add list of bridge joints to clipboard history
        private static void updateJoints(List<ClipboardJoint> newJoints) {
            List<ClipboardJoint> listClone = new List<ClipboardJoint>();
            foreach (ClipboardJoint joint in newJoints) {
                listClone.Add(joint);
            }
            saved_Joints.Add(listClone);
        }

        // Helper method for CopyToClipboard
        // Also copied from BridgeSelectionSet
        private static Vector2 CalculateSelectSetCenter(List<BridgeJoint> m_Joints, List<BridgeEdge> m_Edges)
        {
            float num = 0f;
            float num2 = 0f;
            int num3 = 0;
            foreach (BridgeJoint joint in m_Joints)
            {
                num += joint.transform.position.x;
                num2 += joint.transform.position.y;
                num3++;
            }
            foreach (BridgeEdge edge in m_Edges)
            {
                num += edge.m_JointA.transform.position.x;
                num2 += edge.m_JointA.transform.position.y;
                num += edge.m_JointB.transform.position.x;
                num2 += edge.m_JointB.transform.position.y;
                num3 += 2;
            }
            return Utils.V3toV2(GameUI.SnapPosToGrid(new Vector3(num / (float)num3, num2 / (float)num3, Cameras.MainCamera().transform.position.z + 1f)));
        }

        // Converts list of clipboardEdge to bridgeEdge
        private static List<BridgeEdge> clipboardEdgeToBridgeEdge(List<ClipboardEdge> edges) {
            List<BridgeEdge> list = new List<BridgeEdge>();
            foreach (ClipboardEdge edge in edges) {
                list.Add(edge.m_SourceBridgeEdge);
            }
            return list;
        }

        // Converts list of clipboardJoing to bridgeJoint
        private static List<BridgeJoint> clipboardJointToBridgeJoint(List<ClipboardJoint> joints) {
            List<BridgeJoint> list = new List<BridgeJoint>();
            foreach (ClipboardJoint joint in joints) {
                list.Add(joint.m_SourceBridgeJoint);
            }
            return list;
        }

        // Copy list of joints and edges to the clipboard
        // Mostly copied from BridgeSelectionSet
        private static void CopyToClipboard(List<BridgeJoint> m_Joints, List<BridgeEdge> m_Edges) {
            ClipboardManager.ClearClipboard();
            ClipboardManager.SetContainerPosition(GameUI.SnapPosToGrid(Utils.GetWorldPointFromScreenPos(Input.mousePosition)));
            ClipboardManager.AlignClipboardAnchors();
            Vector2 vector = CalculateSelectSetCenter(m_Joints, m_Edges);
            foreach (BridgeJoint joint in m_Joints)
            {
                if (!joint.m_IsAnchor || BridgeEdges.EdgeIsConnectedToJoint(joint))
                {
                    ClipboardManager.AddJoint(Utils.V3toV2(joint.transform.position) - vector, joint);
                }
            }
            foreach (BridgeEdge edge in m_Edges)
            {
                float z = edge.transform.localEulerAngles.z;
                float length = edge.GetLength();
                ClipboardManager.AddEdge(Utils.V3toV2(edge.transform.position) - vector, z, length, edge);
                if (!m_Joints.Contains(edge.m_JointA))
                {
                    ClipboardManager.AddJoint(Utils.V3toV2(edge.m_JointA.transform.position) - vector, edge.m_JointA);
                }
                if (!m_Joints.Contains(edge.m_JointB))
                {
                    ClipboardManager.AddJoint(Utils.V3toV2(edge.m_JointB.transform.position) - vector, edge.m_JointB);
                }
            }
        }

        // Takes the selected index in the saved history and adds it to the in game clipboard
        private static void updateClipboard() {
            List<ClipboardEdge> clipboardEdges = saved_Edges[clipboard_History_Index];
            List<ClipboardJoint> clipboardJoints = saved_Joints[clipboard_History_Index];
            List<BridgeEdge> edges = clipboardEdgeToBridgeEdge(clipboardEdges);
            List<BridgeJoint> joints = clipboardJointToBridgeJoint(clipboardJoints);
            CopyToClipboard(joints, edges);
        }

        // Patch to automatically update saved history whenever a selection is copied
        [HarmonyPatch(typeof(BridgeSelectionSet), "CopySelectionSet")]
        [HarmonyPostfix]
        private static void updateHistory() {
            if (mEnabled.Value) {
                updateEdges(ClipboardManager.GetEdges());
                updateJoints(ClipboardManager.GetJoints());
                if (saved_Edges.Count > maxHistorySize.Value) {
                    saved_Edges.RemoveAt(0);
                    saved_Joints.RemoveAt(0);
                }
                clipboard_History_Index = saved_Edges.Count;
            }
        }

        // Adds basic controls to navigate clipboard history
        [HarmonyPatch(typeof(GameStateCommonInput), "DoKeyboardProcessing")]
        [HarmonyPostfix]
        private static void saveOnKey() {
            if (mEnabled.Value && GameStateManager.GetState() == GameState.BUILD) {
                if (keybindHistoryForward.Value.IsDown()) {
                    if (clipboard_History_Index < saved_Edges.Count - 1) clipboard_History_Index += 1;
                    updateClipboard();
                }
                if (keybindHistoryBack.Value.IsDown()) {
                    if (clipboard_History_Index > 0) clipboard_History_Index -= 1;
                    updateClipboard();
                }
            }
        }
    }
}