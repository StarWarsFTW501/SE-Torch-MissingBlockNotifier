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
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Components.BankingAndCurrency;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;

namespace TorchPlugin
{
    [HarmonyPatch]
    public class MyPatches
    {
        readonly static FieldInfo _fieldInfo_m_cubeBlocks = typeof(MyCubeGrid).GetField("m_cubeBlocks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_cubeBlocks' not found in type '{nameof(MyCubeGrid)}'! Please disable the plugin and contact author!");
        readonly static FieldInfo _fieldInfo_m_other = typeof(MyShipConnector).GetField("m_other", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_other' not found in type '{nameof(MyShipConnector)}'! Please disable the plugin and contact author!");

        // When moving blocks to another grid (merge)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlockInternal")]
        public static void MyCubeGrid_AddBlockInternal_Postfix(MyCubeGrid __instance, MySlimBlock block)
        {
            try
            {
                Plugin.Instance.Logger.Info($"AddBlockInternal on grid {__instance.DisplayName} ({__instance.EntityId}) for block {block?.BlockDefinition?.Id} at {block?.Position}, result: {block != null}");
                if (Plugin.Instance.TrackingManager.IsStarted && block != null)
                {
                    Plugin.Instance.TrackingManager.RegisterNewBlock(__instance, block);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in AddBlockInternal Harmony patch.");
                throw;
            }
        }
        
        // Any other case of adding blocks (including on initialization)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlock")]
        public static void MyCubeGrid_AddBlock_Postfix(MyCubeGrid __instance, MySlimBlock __result, MyObjectBuilder_CubeBlock objectBuilder, bool testMerge)
        {
            try
            {
                //Plugin.Instance.Logger.Info($"AddBlock on grid {__instance.DisplayName} ({__instance.EntityId}) for block {objectBuilder?.GetId()} at {__result?.Position}, testMerge: {testMerge}, result: {__result != null}");
                if (Plugin.Instance.TrackingManager.IsStarted && __result != null)
                {
                    Plugin.Instance.TrackingManager.RegisterNewBlock(__instance, __result);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in AddBlock Harmony patch.");
                throw;
            }
        }

        // When connectors connect
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyShipConnector), "UpdateConnectionStateConnecting")]
        public static void MyShipConnector_UpdateConnectionStateConnecting_Postfix(MyShipConnector __instance)
        {
            try
            {
                Plugin.Instance.Logger.Info($"Connector at {__instance.Position} on grid {__instance.CubeGrid?.DisplayName} ({__instance.CubeGrid?.EntityId}) connected to another connector");
                if (Plugin.Instance.TrackingManager.IsStarted)
                {
                    var other = (MyShipConnector)_fieldInfo_m_other.GetValue(__instance);
                    if (other != null)
                    {
                        Plugin.Instance.TrackingManager.GridsConnected(__instance.CubeGrid, other.CubeGrid, MyTrackableType.CONNECTOR_STRUCTURE);

                        // note: both connectors' grids are notified about the other connector being added, but it shouldn't actually trigger the AddBlock methods
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in UpdateConnectionStateConnecting Harmony patch.");
                throw;
            }
        }
        // When connectors disconnect, where their system links have been broken, including block closing/damage/...
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyShipConnector), "RemoveLinks")]
        public static void MyShipConnector_RemoveLinks_Postfix(MyShipConnector __instance, MyShipConnector otherConnector)
        {
            try
            {
                Plugin.Instance.Logger.Info($"Connector at {__instance.Position} on grid {__instance.CubeGrid?.DisplayName} ({__instance.CubeGrid?.EntityId}) disconnected from connector at {otherConnector?.Position} on grid {otherConnector?.CubeGrid?.DisplayName} ({otherConnector?.CubeGrid?.EntityId})");
                if (Plugin.Instance.TrackingManager.IsStarted && otherConnector != null)
                {
                    // needs to handle if connectors are same grid or the construct is still linked
                    Plugin.Instance.TrackingManager.GridsDisconnected(__instance.CubeGrid, otherConnector.CubeGrid, MyTrackableType.CONNECTOR_STRUCTURE);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in RemoveLinks Harmony patch.");
                throw;
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
            try
            {
                Plugin.Instance.Logger.Info($"Mechanical block {__instance.BlockDefinition?.Id} at {__instance.Position} on grid {__instance.CubeGrid?.DisplayName} ({__instance.CubeGrid?.EntityId}) attached to top block {topBlock?.BlockDefinition?.Id} at {topBlock?.Position} on grid {topBlock?.CubeGrid?.DisplayName} ({topBlock?.CubeGrid?.EntityId}), success: {__result}, updateGroup: {updateGroup}");
                if (Plugin.Instance.TrackingManager.IsStarted && __result && updateGroup)
                {
                    // needs to handle if mechanical blocks already had construct links or were same grid
                    Plugin.Instance.TrackingManager.GridsConnected(__instance.CubeGrid, topBlock.CubeGrid, MyTrackableType.CONSTRUCT);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in Mechanical Attach Harmony patch.");
                throw;
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
            try
            {
                Plugin.Instance.Logger.Info($"Mechanical block {__instance.BlockDefinition?.Id} at {__instance.Position} on grid {__instance.CubeGrid?.DisplayName} ({__instance.CubeGrid?.EntityId}) broke links to top block {topBlock?.BlockDefinition?.Id} at {topBlock?.Position} on grid {topGrid?.DisplayName} ({topGrid?.EntityId})");
                if (Plugin.Instance.TrackingManager.IsStarted && topGrid != null)
                {
                    // needs to handle if mechanical blocks still have construct links (by other subgrids) - or i guess same grid too
                    Plugin.Instance.TrackingManager.GridsDisconnected(__instance.CubeGrid, topGrid, MyTrackableType.CONSTRUCT);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in Mechanical BreakLinks Harmony patch.");
                throw;
            }
        }



        // ANY block removal!! except for deleting grid
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MyCubeGrid), "RemoveBlockInternal")]
        public static void MyCubeGrid_RemoveBlockInternal_Prefix(MyCubeGrid __instance, MySlimBlock block, bool close, bool markDirtyDisconnects)
        {
            try
            {
                Plugin.Instance.Logger.Info($"RemoveBlockInternal on grid {__instance.DisplayName} ({__instance.EntityId}) for block {block?.BlockDefinition?.Id} at {block?.Position}");
                if (Plugin.Instance.TrackingManager.IsStarted && ((HashSet<MySlimBlock>)_fieldInfo_m_cubeBlocks.GetValue(__instance)).Contains(block))
                {
                    Plugin.Instance.TrackingManager.UnregisterBlock(__instance, block);
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in RemoveBlockInternal Harmony patch.");
                throw;
            }
        }

        // Grid gets deleted after this (unregister)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "UnregisterBlocksBeforeClose")]
        public static void MyCubeGrid_UnregisterBlocksBeforeClose_Postfix(MyCubeGrid __instance)
        {
            try
            {
                Plugin.Instance.Logger.Info($"UnregisterBlocksBeforeClose on grid {__instance.DisplayName} ({__instance.EntityId})");
                if (Plugin.Instance.TrackingManager.IsStarted)
                    Plugin.Instance.TrackingManager.UnregisterGrid(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in UnregisterBlocksBeforeClose Harmony patch.");
                throw;
            }
        }

        // Any entity initialization from ObjectBuilder - crucially, grids that get loaded in from files (called on new grids too? idk)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyEntity), "Init", new Type[] { typeof(MyObjectBuilder_EntityBase) })]
        public static void MyEntity_Init_Postfix(MyEntity __instance, MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                //Plugin.Instance.Logger.Info($"Init on entity {objectBuilder?.Name ?? "<null>"} ({__instance.EntityId}) - grid? {__instance is MyCubeGrid}");
                if (__instance is MyCubeGrid grid && Plugin.Instance.TrackingManager.IsStarted)
                    Plugin.Instance.TrackingManager.RegisterGridSingle(grid);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, "Unhandled exception in Init Harmony patch.");
                throw;
            }
        }
    }
}
