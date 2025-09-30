using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;

namespace TorchPlugin
{
    internal class MyBlockTracker
    {
        public readonly List<(string type, string subtype)> Rule = new List<(string type, string subtype)>();
        public MyBlockTracker(string rule)
        {
            Rule = rule.Split('|').Select(b =>
            {
                var spl = b.Split(new char[] { '/' }, 2);
                return (spl[0], spl[1]);
            }).ToList();
        }

        /// <summary>
        /// key = entityId of grid, value = number of matches for this tracker
        /// </summary>
        Dictionary<long, int> _matches = new Dictionary<long, int>();
        public void InitBlockForGrid(MyCubeGrid grid, MySlimBlock block)
        {
            
        }
    }
}
