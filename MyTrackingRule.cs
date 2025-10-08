using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game;
using VRage.ObjectBuilders;

namespace TorchPlugin
{
    [Serializable]
    public class MyTrackingRule : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        string _name, _chatMessage;
        int _requiredMatches;
        MyTrackableType _type;
        List<MyDefinitionId> _blocks;

        StringBuilder _stringBuilder = new StringBuilder();

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        public string ChatMessage
        {
            get => _chatMessage;
            set
            {
                if (_chatMessage != value)
                {
                    _chatMessage = value;
                    OnPropertyChanged(nameof(ChatMessage));
                }
            }
        }
        public string BlockString
        {
            get
            {
                lock (_stringBuilder)
                {
                    foreach (var block in _blocks)
                    {
                        _stringBuilder.Append(block.TypeId).Append("|").Append(block.SubtypeName).Append(";");
                    }
                    var result = _stringBuilder.ToString().TrimEnd(';');
                    _stringBuilder.Clear();
                    return result;
                }
            }
            set
            {
                _blocks.Clear();
                var definitions = value.Split(';');
                _blocks.Capacity = definitions.Length;
                string[] ids;
                foreach (var definition in definitions)
                {
                    ids = definition.Split('|');
                    _blocks.Add(new MyDefinitionId(MyObjectBuilderType.Parse(ids[0]), ids[1]));
                }
                OnPropertyChanged(nameof(BlockString));
            }
        }
        public int RequiredMatches
        {
            get => _requiredMatches;
            set
            {
                if (_requiredMatches != value)
                {
                    _requiredMatches = value;
                    OnPropertyChanged(nameof(RequiredMatches));
                }
            }
        }
        public MyTrackableType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
        }


        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }




        public bool BlockMatchesRule(MySlimBlock block)
            => _blocks.Contains(block.BlockDefinition.Id);


        public static Array TrackingTypes => Enum.GetValues(typeof(MyTrackableType));
    }
}
