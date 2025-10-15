using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SteamKit2.GC.Dota.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.API.Managers;
using VRage.Utils;

namespace TorchPlugin
{
    public class MyTrackingGroup : INotifyPropertyChanged
    {
        readonly static FieldInfo _fieldInfo_m_ownershipManager = typeof(MyCubeGrid).GetField("m_ownershipManager", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Field 'm_ownershipManager' not found in type '{nameof(MyCubeGrid)}'! Please disable the plugin and contact author!");
        readonly static Type _type_MyCubeGridOnwershipManager = AccessTools.TypeByName("Sandbox.Game.Entities.Cube.MyCubeGridOwnershipManager")
            ?? throw new MissingMemberException($"Type 'Sandbox.Game.Entities.Cube.MyCubeGridOwnershipManager' not found in game assembly! Please disable the plugin and contact author!");
        readonly static FieldInfo _fieldInfo_PlayerOwnedValidBlocks = _type_MyCubeGridOnwershipManager.GetField("PlayerOwnedValidBlocks")
            ?? throw new MissingFieldException($"Field 'PlayerOwnedValidBlocks' not found in type 'MyCubeGridOwnershipManager'! Please disable the plugin and contact author!");

        Dictionary<long, List<MyTrackable>> _toMessageTrackablesByPlayer = new Dictionary<long, List<MyTrackable>>();

        /// <summary>
        /// Checks this <see cref="MyTrackingGroup"/>'s rules for a <see cref="MyTrackable"/> and enqueues a message to the <see cref="MyTrackable"/>'s majority owner(s) if online.
        /// </summary>
        /// <param name="trackable">The <see cref="MyTrackable"/> to check with this group.</param>
        public void EnqueueMessageForTrackable(MyTrackable trackable)
        {
            Plugin.Instance.Logger.Info($"Group '{Name}' checking trackable '{trackable.GetDisplayName()}' for message enqueue...");

            // check grid count rule first
            if (Type != MyTrackableType.GRID)
            {
                int grids = trackable.ContainedCount;
                Plugin.Instance.Logger.Info($"Trackable '{trackable.GetDisplayName()}' has {grids} grids. Comparing to Group '{Name}' mode {GridCountMode} and target {GridCount}...");
                if (GridCountMode == MyRuleMode.LESS && !(grids < GridCount))
                    return;
                else if (GridCountMode == MyRuleMode.EQUAL && !(grids == GridCount))
                    return;
                else if (GridCountMode == MyRuleMode.MORE && !(grids > GridCount))
                    return;
            }

            // check block count rule second
            var blockCount = trackable.GetAllLeafProxies().Sum(g => g.Grid.BlocksCount);
            Plugin.Instance.Logger.Info($"Trackable '{trackable.GetDisplayName()}' has {blockCount} blocks. Comparing to Group '{Name}' mode {BlockCountMode} and target {BlockCount}...");
            if (BlockCountMode == MyRuleMode.LESS && !(blockCount < BlockCount))
                return;
            else if (BlockCountMode == MyRuleMode.EQUAL && !(blockCount == BlockCount))
                return;
            else if (BlockCountMode == MyRuleMode.MORE && !(blockCount > BlockCount))
                return;

            Plugin.Instance.Logger.Info($"Checking matches...");
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
                Plugin.Instance.Logger.Info($"Matches accepted, gathering players via {MessageMode}...");
                // look for owners
                var ownedBlocksByOwner = new Dictionary<long, int>();

                int totalBlocks = 0;

                foreach (var grid in trackable.GetAllLeafProxies())
                {
                    var ownershipManager = _fieldInfo_m_ownershipManager.GetValue(grid.Grid);

                    var dict = (Dictionary<long, int>)_fieldInfo_PlayerOwnedValidBlocks.GetValue(ownershipManager);

                    Plugin.Instance.Logger.Info($"Contained grid '{grid.Grid.DisplayName}' has {dict.Count} owners.");

                    foreach (var owner in dict)
                    {
                        int ownedBlocks = owner.Value;

                        totalBlocks += ownedBlocks;

                        if (!ownedBlocksByOwner.ContainsKey(owner.Key))
                            ownedBlocksByOwner[owner.Key] = ownedBlocks;
                        else ownedBlocksByOwner[owner.Key] += ownedBlocks;
                    }
                }

                Plugin.Instance.Logger.Info($"Total blocks in trackable: {totalBlocks}. Found {ownedBlocksByOwner.Count} total owners.");


                var toNotify = new List<long>();
                if (MessageMode == MyGroupMessageMode.BIG_OWNERS)
                {
                    int maxBlocks = 0;
                    foreach (var owner in ownedBlocksByOwner)
                    {
                        int ownedBlocks = owner.Value;
                        if (ownedBlocks > maxBlocks)
                        {
                            Plugin.Instance.Logger.Info($"{owner} is biggest owner so far, with {ownedBlocks} blocks.");
                            toNotify.Clear();
                            toNotify.Add(owner.Key);
                            maxBlocks = ownedBlocks;
                        }
                        else if (ownedBlocks == maxBlocks)
                        {
                            Plugin.Instance.Logger.Info($"{owner} is tied, with {ownedBlocks} blocks.");
                            toNotify.Add(owner.Key);
                        }
                    }
                }
                else // if (MessageMode == MyGroupMessageMode.PERCENTAGE)
                {
                    foreach (var owner in ownedBlocksByOwner)
                    {
                        int ownedBlocks = owner.Value;
                        if ((double)ownedBlocks / totalBlocks > _fractionOwnedForMessage)
                        {
                            toNotify.Add(owner.Key);
                        }
                    }
                }

                foreach (var owner in toNotify)
                {
                    if (!_toMessageTrackablesByPlayer.ContainsKey(owner))
                        _toMessageTrackablesByPlayer[owner] = new List<MyTrackable> { trackable };
                    else _toMessageTrackablesByPlayer[owner].Add(trackable);
                }
                Plugin.Instance.Logger.Info($"{toNotify.Count} players found, enqueued.");
            }
        }

        /// <summary>
        /// Sends all enqueued messages to the appropriate players in bulk.
        /// </summary>
        public void SendQueuedMessages()
        {
            var stringBuilder = new StringBuilder();
            foreach (var messagePair in _toMessageTrackablesByPlayer)
            {
                stringBuilder.Clear().Append(ChatMessage);

                foreach (var trackable in messagePair.Value)
                    stringBuilder.Append("\n - ").Append(trackable.GetDisplayName());

                Plugin.Instance.SendChatMessageToIdentityId(stringBuilder.ToString(), messagePair.Key);
            }
            _toMessageTrackablesByPlayer.Clear();
        }


        string _name = "Tracking Group", _chatMessage = "This message will be followed by a list of grids. Violating grids:";
        MyTrackableType _type = MyTrackableType.CONSTRUCT;
        MyGroupMatchMode _mode = MyGroupMatchMode.ALL;
        MyGroupMessageMode _messageMode = MyGroupMessageMode.BIG_OWNERS;
        MyRuleMode _gridCountMode = MyRuleMode.MORE, _blockCountMode = MyRuleMode.MORE;
        int _gridCount = 0, _blockCount = 0;
        float _fractionOwnedForMessage = .2f;
        List<MyTrackingRule> _rules = new List<MyTrackingRule>();
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
        public int BlockCount
        {
            get => _blockCount;
            set
            {
                if (_blockCount != value)
                {
                    _blockCount = value;
                    OnPropertyChanged(nameof(BlockCount));
                }
            }
        }
        public MyRuleMode BlockCountMode
        {
            get => _blockCountMode;
            set
            {
                if (_blockCountMode != value)
                {
                    _blockCountMode = value;
                    OnPropertyChanged(nameof(BlockCountMode));
                }
            }
        }
        public float PercentOwnedForMessage
        {
            get => _fractionOwnedForMessage * 100;
            set
            {
                value = value / 100;
                if (_fractionOwnedForMessage != value)
                {
                    _fractionOwnedForMessage = value;
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
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
        PERCENTAGE
    }
}
