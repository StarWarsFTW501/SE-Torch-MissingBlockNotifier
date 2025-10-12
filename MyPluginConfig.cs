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
        List<MyTrackingGroup> _groups;

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

        public List<MyTrackingGroup> Groups
        {
            get => _groups;
            set
            {
                if (_groups != value)
                {
                    foreach (var rule in _groups)
                        PropertyChanged -= OnGroupChanged;
                    foreach (var rule in value)
                        PropertyChanged += OnGroupChanged;
                    _groups = value;
                    OnPropertyChanged(nameof(Groups));
                }
            }
        }

        public void AddGroup(MyTrackingGroup group)
        {
            Groups.Add(group);
            group.PropertyChanged += OnGroupChanged;
            OnPropertyChanged(nameof(Groups));
        }
        public void RemoveGroup(MyTrackingGroup group)
        {
            Groups.Remove(group);
            group.PropertyChanged -= OnGroupChanged;
            OnPropertyChanged(nameof(Groups));
        }
        public void RemoveGroup(int index)
        {
            Groups[index].PropertyChanged -= OnGroupChanged;
            Groups.RemoveAt(index);
            OnPropertyChanged(nameof(Groups));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        void OnGroupChanged(object group, PropertyChangedEventArgs propertyChangedEventArgs) => OnPropertyChanged($"{(group as MyTrackingGroup)?.Name}:{propertyChangedEventArgs.PropertyName}");
    }
}
