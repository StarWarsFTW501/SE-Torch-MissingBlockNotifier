using HarmonyLib;
using Sandbox.Game;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers.ChatManager;
using Torch.Session;
using VRage.FileSystem;
using VRage.Utils;
using VRageMath;

namespace TorchPlugin
{
    public class Plugin : TorchPluginBase, IWpfPlugin
    {
        // TODO:
        // - Verify operation of complex structure merging/splitting (so far we only know simple grid addition/removal and subgrid merging works)
        // - Verify merge blocks don't break plugin
        // - Remove useless logs
        // - Update UI elements with proper descriptions
        // - Make UI for groups and rules
        // - Ask to load dev server with full live save for stress testing
        // - Verify partial group ownership works as intended
        // - Update README
        // - Refactor












        const string PLUGIN_NAME = "MissingBlockNotifier";

        readonly static Color DefaultChatColor = Color.Gold;
        readonly static string DefaultChatFont = "White";

        public static Plugin Instance;

        internal MyLogger Logger { get; private set; }

        internal IMyPluginConfig Config => _config?.Data;
        private PersistentConfig<MyPluginConfig> _config;

        internal MyBlockTrackingManager TrackingManager { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            Instance = this;

            Logger = new MyLogger(PLUGIN_NAME, PLUGIN_NAME);

            var configPath = Path.Combine(StoragePath, $"{PLUGIN_NAME}.cfg");
            _config = PersistentConfig<MyPluginConfig>.Load(Logger, configPath);

            var harmony = new Harmony(PLUGIN_NAME);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            if (Config.Groups.Count == 0)
            {
                Logger.Info("Creating new group & rule...");

                Config.AddGroup(new MyTrackingGroup()
                {
                    Name = "Testing Group",
                    Type = MyTrackableType.CONSTRUCT
                });

                Config.Groups[0].AddRule(new MyTrackingRule()
                {
                    TypeId = "MyObjectBuilder_Refinery",
                    SubtypeName = "LargeRefinery",
                    Mode = MyRuleMode.MORE,
                    TargetMatches = 2
                });
            }

            
            TrackingManager = new MyBlockTrackingManager();

            Torch.Managers.GetManager<ITorchSessionManager>().SessionStateChanged += SessionStateChanged;

            Logger.Info("Plugin initialized.");
        }

        void SessionStateChanged(ITorchSession session, TorchSessionState newState)
        {
            switch (newState)
            {
                case TorchSessionState.Loaded:
                    if (Config.Enabled)
                    {
                        foreach (var group in Config.Groups)
                            foreach (var rule in group.Rules)
                                if (!rule.ValidateBlockDefinition())
                                    Logger.Warning($"Rule in group '{group.Name}' has loaded with invalid block definition '{rule.TypeId}/{rule.SubtypeName}'! No blocks will be tracked!");

                        TrackingManager.Start();
                    }
                    break;
                case TorchSessionState.Unloading:
                    TrackingManager.Stop();
                    break;
            }
        }

        /// <summary>
        /// Attempts to send a chat message to the player with the given SteamID. If no such player exists or is offline, no message is sent.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="steamId">The SteamID of the player to send the message to.</param>
        /// <param name="serverColor">The color of the "Server" message author in chat. Default used if <see langword="null"/>.</param>
        /// <param name="font">The font of the message in chat. Default used if <see langword="null"/>.</param>
        public void SendChatMessageToSteamId(string message, ulong steamId = 0, Color? serverColor = null, string font = null)
        {
            var chatManager = Torch?.CurrentSession?.Managers?.GetManager<ChatManagerServer>();
            if (chatManager != null)
            {
                if (steamId == 0 || MySession.Static.Players.TryGetPlayerBySteamId(steamId) != null)
                    chatManager.SendMessageAsOther("Server", message, serverColor ?? DefaultChatColor, steamId, font ?? DefaultChatFont);
            }
            else
            {
                Logger.Error($"Could not retrieve Torch server chat manager!");
            }
        }
        /// <summary>
        /// Attempts to send a chat message to the player with the given identity ID. If no such player exists, no message is sent.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="identityId">The IdentityId of the player to send the message to.</param>
        /// <param name="serverColor">The color of the "Server" message author in chat. Default used if <see langword="null"/>.</param>
        /// <param name="font">The font of the message in chat. Default used if <see langword="null"/>.</param>
        public void SendChatMessageToIdentityId(string message, long identityId, Color? serverColor = null, string font = null)
        {
            ulong steamId = MySession.Static.Players.TryGetSteamId(identityId);
            if (steamId != 0)
            {
                SendChatMessageToSteamId(message, steamId, serverColor, font);
            }
        }

        /// <summary>
        /// Retrieves an <see cref="Action"/> used to apply configuration changes. Signals whether its execution will require a full recompute of all tracking data.
        /// </summary>
        /// <param name="needsRecompute">Whether all tracking data will be recomputed by invoking the resulting <see cref="Action"/>.</param>
        /// <param name="typeProblems">Whether there were problems parsing provided block types or not.</param>
        /// <returns>The <see cref="Action"/> to invoke for recomputation of tracking data.</returns>
        public Action GetConfigApplicator(out bool needsRecompute, out bool typeProblems)
        {
            typeProblems = false;
            needsRecompute = false;
            Action applicator = null;
            foreach (var change in Config.ChangedProperties)
            {
                if (change == nameof(Config.Enabled))
                {
                    applicator += () =>
                    {
                        if (Config.Enabled) TrackingManager.Start();
                        else TrackingManager.Stop();
                        return; // a change to the enabled state either fully boots everything up or fully shuts everything down
                    };
                }
                else if (change == nameof(Config.TimerSeconds))
                {
                    applicator += TrackingManager.SetTimer;
                }
                else if (change == nameof(Config.Groups))
                {
                    foreach (var group in Config.Groups)
                        foreach (var rule in group.Rules)
                            if (!rule.ValidateBlockDefinition())
                            {
                                typeProblems = true;
                                Logger.Warning($"Rule in group '{group.Name}' has an invalid block definition '{rule.TypeId}/{rule.SubtypeName}'! No blocks will be tracked!");
                            }
                    applicator += TrackingManager.LoadConfig;
                    needsRecompute = true;
                }
            }
            applicator += () =>
            {
                CommandManager.InvalidateRequerySuggested();
                Config.ChangedProperties.Clear();
            };
            return applicator;
        }
        public UserControl GetGroupManagerControl() => _groupManagerControl ?? (_groupManagerControl = new GroupManagerView());
        private UserControl _groupManagerControl;
        public UserControl GetControl() => _configurationView ?? (_configurationView = new ConfigView());
        private UserControl _configurationView;
    }
}