using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    [HarmonyPatch]
    public class MyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyCubeGrid), "AddBlockInternal")]
        public static void MyCubeGrid_AddBlockInternal_Postfix(MySlimBlock block)
        {
            if (block != null)
            {

            }
        }
    }
}
