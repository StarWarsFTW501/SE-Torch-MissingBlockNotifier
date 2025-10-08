using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TorchPlugin
{
    [Serializable]
    public class MyPluginConfig : INotifyPropertyChanged, IMyPluginConfig
    {
        bool _enabled;
        double _timerSeconds = 600;
        List<MyTrackingRule> _rules;
        string _blockRules = "Beacons|Beacon/LargeBlockBeacon|Beacon/LargeBlockBeaconReskin|Beacon/SmallBlockBeacon|Beacon/SmallBlockBeaconReskin";

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

        public List<MyTrackingRule> Rules
        {
            get => _rules;
            set
            {
                if (_rules != value)
                {
                    foreach (var rule in _rules)
                        PropertyChanged -= OnRuleChanged;
                    foreach (var rule in value)
                        PropertyChanged += OnRuleChanged;
                    _rules = value;
                    OnPropertyChanged(nameof(Rules));
                }
            }
        }

        public void AddRule(MyTrackingRule rule)
        {
            Rules.Add(rule);
            OnPropertyChanged(nameof(Rules));
        }
        public void RemoveRule(MyTrackingRule rule)
        {
            Rules.Remove(rule);
            OnPropertyChanged(nameof(Rules));
        }
        public void RemoveRule(int index)
        {
            Rules.RemoveAt(index);
            OnPropertyChanged(nameof(Rules));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        void OnRuleChanged(object rule, PropertyChangedEventArgs propertyChangedEventArgs) => OnPropertyChanged($"{(rule as MyTrackingRule)?.Name}:{propertyChangedEventArgs.PropertyName}");
    }
}
