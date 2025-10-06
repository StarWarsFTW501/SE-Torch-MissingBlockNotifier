using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components.BankingAndCurrency;
using VRage.ObjectBuilders;

namespace TorchPlugin
{
    [HarmonyPatch]
    public class MyPatches
    {
        static FieldInfo _fieldInfo_m_cubeBlocks = typeof(MyCubeGrid).GetField("m_cubeBlocks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_cubeBlocks' not found in type '{nameof(MyCubeGrid)}'! Please disable the plugin and contact author!");
        static FieldInfo _fieldInfo_m_other = typeof(MyShipConnector).GetField("m_other", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_other' not found in type '{nameof(MyShipConnector)}'! Please disable the plugin and contact author!");

        // When moving blocks to another grid (merge)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlockInternal")]
        public static void MyCubeGrid_AddBlockInternal_Postfix(MySlimBlock block)
        {
            if (block != null)
            {

            }
        }
        
        // Any other case of adding blocks (including on initialization)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlock")]
        public static void MyCubeGrid_AddBlock_Postfix(MySlimBlock __result, MyObjectBuilder_CubeBlock objectBuilder, bool testMerge)
        {
            if (__result != null)
            {
                
            }
        }

        // When ships connect by connector
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyShipConnector), "UpdateConnectionStateConnecting")]
        public static void MyShipConnector_UpdateConnectionStateConnecting_Postfix(MyShipConnector __instance)
        {
            var other = (MyShipConnector)_fieldInfo_m_other.GetValue(__instance);
            if (other != null)
            {
                Plugin.Instance.TrackingManager.GridsConnectedByConnector(__instance.CubeGrid, other.CubeGrid);
            }
        }


        // ANY block removal!! except for deleting grid
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MyCubeGrid), "RemoveBlockInternal")]
        public static void MyCubeGrid_RemoveBlockInternal_Prefix(MyCubeGrid __instance, MySlimBlock block, bool close, bool markDirtyDisconnects)
        {
            if (((HashSet<MySlimBlock>)_fieldInfo_m_cubeBlocks.GetValue(__instance)).Contains(block))
            {

            }
        }

        // Grid gets deleted after this (unregister)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "BeforeDelete")]
        public static void MyCubeGrid_BeforeDelete_Postfix(MyCubeGrid __instance)
        {

        }

        // Grid gets initialized and its cubes are generated - We need to register it so that we have an entity to register blocks to
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MyCubeGrid), "InitInternal")]
        public static void MyCubeGrid_InitInternal_Prefix(MyCubeGrid __instance, MyObjectBuilder_EntityBase objectBuilder, bool rebuildGrid)
        {

        }
    }
}
