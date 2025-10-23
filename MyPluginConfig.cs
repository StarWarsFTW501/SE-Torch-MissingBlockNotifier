using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;

namespace TorchPlugin
{
    [Serializable]
    public class MyPluginConfig : INotifyPropertyChanged, IMyPluginConfig
    {
        [XmlIgnore]
        public List<string> ChangedProperties { get; } = new List<string>();
        [XmlIgnore]
        public bool HasChanges => ChangedProperties.Count > 0;

        bool _enabled = false;
        float _timerSeconds = 600;
        float _initSeconds = 1;
        List<MyTrackingGroup> _groups = new List<MyTrackingGroup>();

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

        public float TimerSeconds
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

        public float InitSeconds
        {
            get => _initSeconds;
            set
            {
                if (_initSeconds != value)
                {
                    _initSeconds = value;
                    OnPropertyChanged(nameof(InitSeconds));
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
                    foreach (var group in _groups)
                        group.PropertyChanged -= OnGroupChanged;
                    foreach (var group in value)
                        group.PropertyChanged += OnGroupChanged;
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
            ChangedProperties.Add(propertyName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            CommandManager.InvalidateRequerySuggested();
        }
        void OnGroupChanged(object group, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            ChangedProperties.Add(nameof(Groups));
            PropertyChanged?.Invoke(group, new PropertyChangedEventArgs($"{(group as MyTrackingGroup)?.Name}:{propertyChangedEventArgs.PropertyName}"));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
