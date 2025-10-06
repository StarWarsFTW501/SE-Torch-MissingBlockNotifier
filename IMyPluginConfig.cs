using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal interface IMyPluginConfig
    {
        bool Enabled { get; set; }
        double TimerSeconds { get; set; }
        string BlockRules { get; set; }
        MyTrackableType TrackableType { get; set; }
        Array TrackableTypes { get; }
    }
}
