using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TorchPlugin
{
    public class MyPluginConfig : INotifyPropertyChanged, IMyPluginConfig
    {
        bool _enabled;
        double _timerSeconds = 600;
        string _blockRules = "Beacon/LargeBlockBeacon|Beacon/LargeBlockBeaconReskin|Beacon/SmallBlockBeacon|Beacon/SmallBlockBeaconReskin";

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged(nameof(Enabled));
                }
            }
        }

        public double TimerSeconds
        {
            get => _timerSeconds;
            set
            {
                if (_timerSeconds != value)
                {
                    _timerSeconds = value;
                    OnPropertyChanged(nameof(TimerSeconds));
                }
            }
        }

        public string BlockRules
        {
            get => _blockRules;
            set
            {
                if (_blockRules != value)
                {
                    _blockRules = value;
                    OnPropertyChanged(nameof(BlockRules));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
