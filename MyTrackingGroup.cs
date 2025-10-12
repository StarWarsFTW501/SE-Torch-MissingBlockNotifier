using SteamKit2.GC.Dota.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    public class MyTrackingGroup : INotifyPropertyChanged
    {
        /// <summary>
        /// Checks this <see cref="MyTrackingGroup"/>'s rules for a <see cref="MyTrackable"/> and sends a message to the <see cref="MyTrackable"/>'s majority owner(s) if online.
        /// </summary>
        /// <param name="trackable">The <see cref="MyTrackable"/> to check with this group.</param>
        public void ExecuteMessageForTrackable(MyTrackable trackable)
        {
            // check grid count rule first
            if (Type != MyTrackableType.GRID)
            {
                int grids = trackable.ContainedCount;
                if (GridCountMode == MyRuleMode.LESS && !(grids < GridCount))
                    return;
                else if (GridCountMode == MyRuleMode.EQUAL && !(grids == GridCount))
                    return;
                else if (GridCountMode == MyRuleMode.MORE && !(grids > GridCount))
                    return;
            }

            // check if we match the rules
            bool match = Mode == MyGroupMatchMode.ALL || Rules.Count == 0;
            foreach (var rule in Rules)
            {
                if (Mode == MyGroupMatchMode.ANY && rule.TrackableMeetsRule(trackable))
                {
                    match = true;
                    break;
                }
                else if (Mode == MyGroupMatchMode.ALL && !rule.TrackableMeetsRule(trackable))
                {
                    match = false;
                    break;
                }
            }

            // outcome: a group with no rules will always match

            if (match)
            {
                // look for owners
                var ids = new List<long>();
                



                foreach (var grid in trackable.GetAllLeafProxies())
                {
                    if (mo)
                    var owners = MyOwnershipTracker.GetGridMajorityOwners(grid.Grid);
                    foreach (var owner in owners)
                    {
                        var player = MyPlayerTracker.GetPlayerById(owner);
                        if (player?.IsOnline == true)
                        {
                            MyChatSender.SendPrivateMessage(player.SteamId, ChatMessage.Replace("{name}", player.DisplayName).Replace("{grid}", grid.Grid.DisplayName).Replace("{group}", Name));
                        }
                    }
                }
            }
        }


        string _name, _chatMessage;
        MyTrackableType _type = MyTrackableType.CONSTRUCT;
        MyGroupMatchMode _mode = MyGroupMatchMode.ALL;
        MyGroupMessageMode _messageMode = MyGroupMessageMode.BIG_OWNERS;
        MyRuleMode _gridCountMode = MyRuleMode.MORE;
        int _gridCount = 0;
        float _percentOwnedForMessage = 20;
        List<MyTrackingRule> _rules;
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
        public MyGroupMatchMode Mode
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
        public int GridCount
        {
            get => _gridCount;
            set
            {
                if (_gridCount != value)
                {
                    _gridCount = value;
                    OnPropertyChanged(nameof(GridCount));
                }
            }
        }
        public MyRuleMode GridCountMode
        {
            get => _gridCountMode;
            set
            {
                if (_gridCountMode != value)
                {
                    _gridCountMode = value;
                    OnPropertyChanged(nameof(GridCountMode));
                }
            }
        }
        public float PercentOwnedForMessage
        {
            get => _percentOwnedForMessage;
            set
            {
                if (_percentOwnedForMessage != value)
                {
                    _percentOwnedForMessage = value;
                    OnPropertyChanged(nameof(PercentOwnedForMessage));
                }
            }
        }
        public MyGroupMessageMode MessageMode
        {
            get => _messageMode;
            set
            {
                if (_messageMode != value)
                {
                    _messageMode = value;
                    OnPropertyChanged(nameof(MessageMode));
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
        void OnPropertyChanged(string name) => PropertyChanged(this, new PropertyChangedEventArgs(name));
        void OnRuleChanged(object rule, PropertyChangedEventArgs propertyChangedEventArgs) => OnPropertyChanged($"{(rule as MyTrackingRule)?.TypeId}\\{(rule as MyTrackingRule)?.SubtypeName}:{propertyChangedEventArgs.PropertyName}");

        public static Array MatchModes => Enum.GetValues(typeof(MyGroupMatchMode));
        public static Array MessageModes => Enum.GetValues(typeof(MyGroupMessageMode));
    }
    public enum MyGroupMatchMode
    {
        /// <summary>
        /// If any of the rules are met, sends notification
        /// </summary>
        ANY,
        /// <summary>
        /// If all rules are met, sends notification
        /// </summary>
        ALL
    }
    public enum MyGroupMessageMode
    {
        /// <summary>
        /// Group will message the player with the most grids on a block (or multiple if they own the same number)
        /// </summary>
        BIG_OWNERS,
        /// <summary>
        /// Group will message all players with at least a given percentage of grid owned
        /// </summary>
        ALL_OWNERS
    }
}
