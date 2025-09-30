using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal class MyBlockTrackingManager
    {
        Timer _timer;

        /// <summary>
        /// key = rule, value = tracker
        /// </summary>
        Dictionary<string, MyBlockTracker> _trackers = new Dictionary<string, MyBlockTracker>();

        public void LoadConfig(string config)
        {
            var rules = config.Split(',').Select(r => r.Trim()).ToHashSet();



            foreach (var rule in rules)
            {
                if (!_trackers.ContainsKey(rule))
                {

                }
            }

            var allEntities = MyEntities.GetEntities();
        }
        public void LoadTimer(string timer)
        {

        }
    }
}
