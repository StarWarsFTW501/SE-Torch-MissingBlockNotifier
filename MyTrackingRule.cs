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
        [XmlIgnore]
        public MyBlockTracker AssignedTracker = null;
        [XmlIgnore]
        public MyTrackingGroup Group;
        [XmlIgnore]
        public MyTrackableType Type => Group.Type;

        /// <summary>
        /// Checks whether the given <see cref="MyTrackable"/> matches this rule.
        /// </summary>
        /// <remarks>
        /// Example: If <see cref="Mode"/> is set to <see cref="MyRuleMode.LESS"/> and <see cref="TargetMatches"/> to 10, then a <see cref="MyTrackable"/> with 5 matches will return <see langword="true"/>.
        /// </remarks>
        /// <param name="trackable">The <see cref="MyTrackable"/> to check.</param>
        /// <returns><see langword="true"/> if <paramref name="trackable"/> meets this <see cref="MyTrackingRule"/>, <see langword="false"/> otherwise.</returns>
        public bool TrackableMeetsRule(MyTrackable trackable)
        {
            if (AssignedTracker == null)
                throw new InvalidOperationException("This rule does not have an assigned tracker.");
            int matches = AssignedTracker.GetMatchesForTrackable(trackable);
            if (Mode == MyRuleMode.LESS)
                return matches < TargetMatches;
            else if (Mode == MyRuleMode.EQUAL)
                return matches == TargetMatches;
            else //if (Mode == MyRuleMode.MORE)
                return matches > TargetMatches;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        string _typeId, _subtypeName;
        int _targetMatches;
        MyRuleMode _mode;
        MyDefinitionId? _block;


        
        public string TypeId
        {
            get => _typeId;
            set
            {
                if (_typeId != value)
                {
                    _typeId = value;
                    OnPropertyChanged(nameof(TypeId));
                    ValidateBlockDefinition();
                }
            }
        }
        public string SubtypeName
        {
            get => _subtypeName;
            set
            {
                if (_subtypeName != value)
                {
                    _subtypeName = value;
                    OnPropertyChanged(nameof(SubtypeName));
                    ValidateBlockDefinition();
                }
            }
        }
        public int TargetMatches
        {
            get => _targetMatches;
            set
            {
                if (_targetMatches != value)
                {
                    _targetMatches = value;
                    OnPropertyChanged(nameof(TargetMatches));
                }
            }
        }
        public MyRuleMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }


        void ValidateBlockDefinition()
        {
            MyDefinitionId definition = default;
            if (!string.IsNullOrWhiteSpace(_typeId)
                && !string.IsNullOrWhiteSpace(_subtypeName)
                && MyDefinitionId.TryParse(_typeId, _subtypeName, out definition))
            {
                _block = definition;
            }
            else
            {
                _block = null;
            }
        }

        void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));



        /// <summary>
        /// Returns whether or not the given <see cref="MySlimBlock"/>'s definition matches this rule's block definition.
        /// </summary>
        /// <param name="block">The <see cref="MySlimBlock"/> to check.</param>
        /// <returns><see langword="true"/> if <paramref name="block"/> matches this rule's definition, <see langword="false"/> otherwise.</returns>
        public bool BlockMatchesRule(MySlimBlock block)
            => _block?.Equals(block.BlockDefinition.Id) ?? false;


        public static Array TrackingTypes => Enum.GetValues(typeof(MyTrackableType));
        public static Array RuleModes => Enum.GetValues(typeof(MyRuleMode));
    }
    public enum MyRuleMode
    {
        LESS,
        EQUAL,
        MORE
    }
}
