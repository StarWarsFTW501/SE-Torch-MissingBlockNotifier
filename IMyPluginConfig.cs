using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal interface IMyPluginConfig
    {
        List<string> ChangedProperties { get; }
        bool HasChanges { get; }
        bool Enabled { get; set; }
        float TimerSeconds { get; set; }
        float InitSeconds { get; set; }
        List<MyTrackingGroup> Groups { get; set; }


        void AddGroup(MyTrackingGroup group);
        void RemoveGroup(MyTrackingGroup group);
        void RemoveGroup(int index);
    }
}
