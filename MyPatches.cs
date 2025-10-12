using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.BankingAndCurrency;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;

namespace TorchPlugin
{
    [HarmonyPatch]
    public class MyPatches
    {
        // TODO: Subgrid connection procedures

        static FieldInfo _fieldInfo_m_cubeBlocks = typeof(MyCubeGrid).GetField("m_cubeBlocks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_cubeBlocks' not found in type '{nameof(MyCubeGrid)}'! Please disable the plugin and contact author!");
        static FieldInfo _fieldInfo_m_other = typeof(MyShipConnector).GetField("m_other", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_other' not found in type '{nameof(MyShipConnector)}'! Please disable the plugin and contact author!");

        // When moving blocks to another grid (merge)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlockInternal")]
        public static void MyCubeGrid_AddBlockInternal_Postfix(MyCubeGrid __instance, MySlimBlock block)
        {
            if (block != null)
            {
                Plugin.Instance.TrackingManager.RegisterNewBlock(__instance, block);
            }
        }
        
        // Any other case of adding blocks (including on initialization)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlock")]
        public static void MyCubeGrid_AddBlock_Postfix(MyCubeGrid __instance, MySlimBlock __result, MyObjectBuilder_CubeBlock objectBuilder, bool testMerge)
        {
            if (__result != null)
            {
                Plugin.Instance.TrackingManager.RegisterNewBlock(__instance, __result);
            }
        }

        // When connectors connect
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyShipConnector), "UpdateConnectionStateConnecting")]
        public static void MyShipConnector_UpdateConnectionStateConnecting_Postfix(MyShipConnector __instance)
        {
            var other = (MyShipConnector)_fieldInfo_m_other.GetValue(__instance);
            if (other != null)
            {
                Plugin.Instance.TrackingManager.GridsConnectedByConnector(__instance.CubeGrid, other.CubeGrid);

                // note: both connectors' grids are notified about the other connector being added, but it shouldn't actually trigger the AddBlock methods
            }
        }
        // When connectors disconnect, where their system links have been broken, including block closing/damage/...
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyShipConnector), "RemoveLinks")]
        public static void MyShipConnector_RemoveLinks_Postfix(MyShipConnector __instance, MyShipConnector otherConnector)
        {
            if (otherConnector != null)
            {
                // needs to handle if connectors are same grid or the construct is still linked
                Plugin.Instance.TrackingManager.GridsDisconnectedByConnector(__instance.CubeGrid, otherConnector.CubeGrid);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyMechanicalConnectionBlockBase), "Attach")]
        public static void MyMechanicalConnectionBlockBase_Attach_Postfix(
            bool __result,
            MyMechanicalConnectionBlockBase __instance,
            MyAttachableTopBlockBase topBlock,
            bool updateGroup)
        {
            if (__result && updateGroup)
            {
                // needs to handle if mechanical blocks already had construct links or were same grid
                Plugin.Instance.TrackingManager.GridsConnectedBySubgrid(__instance.CubeGrid, topBlock.CubeGrid);
            }
        }
        // When subgrids disconnect, where their system links have been broken, including block closing/damage/...
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyMechanicalConnectionBlockBase), "BreakLinks")]
        public static void MyMechanicalConnectionBlockBase_BreakLinks_Postfix(
            MyMechanicalConnectionBlockBase __instance,
            MyCubeGrid topGrid,
            MyAttachableTopBlockBase topBlock)
        {
            if (topGrid != null)
            {
                // needs to handle if mechanical blocks still have construct links (by other subgrids) - or i guess same grid too
                Plugin.Instance.TrackingManager.GridsDisconnectedBySubgrid(__instance.CubeGrid, topGrid);
            }
        }



        // ANY block removal!! except for deleting grid
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MyCubeGrid), "RemoveBlockInternal")]
        public static void MyCubeGrid_RemoveBlockInternal_Prefix(MyCubeGrid __instance, MySlimBlock block, bool close, bool markDirtyDisconnects)
        {
            if (((HashSet<MySlimBlock>)_fieldInfo_m_cubeBlocks.GetValue(__instance)).Contains(block))
            {
                Plugin.Instance.TrackingManager.UnregisterBlock(__instance, block);
            }
        }

        // Grid gets deleted after this (unregister)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "BeforeDelete")]
        public static void MyCubeGrid_BeforeDelete_Postfix(MyCubeGrid __instance)
        {
            Plugin.Instance.TrackingManager.UnregisterGrid(__instance);
        }

        // Grid gets initialized and its cubes are generated - We need to register it so that we have an entity to register blocks to
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MyCubeGrid), "InitInternal")]
        public static void MyCubeGrid_InitInternal_Prefix(MyCubeGrid __instance, MyObjectBuilder_EntityBase objectBuilder, bool rebuildGrid)
        {
            Plugin.Instance.TrackingManager.RegisterGrid(__instance);
        }
    }
}
