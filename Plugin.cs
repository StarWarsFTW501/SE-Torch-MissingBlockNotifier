using HarmonyLib;
using Sandbox.Game;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
                        TrackingManager.Start();
                    break;
                case TorchSessionState.Unloading:
                    TrackingManager.Stop();
                    break;
            }
        }

        public void SendChatMessageToSteamId(string message, ulong steamId = 0, Color? serverColor = null, string font = null)
        {
            var chatManager = Torch?.Managers?.GetManager<ChatManagerServer>();
            if (chatManager != null)
            {
                chatManager.SendMessageAsOther("Server", message, serverColor ?? DefaultChatColor, steamId, font ?? DefaultChatFont);
            }
            else
            {
                Logger.Error($"Could not retrieve Torch server chat manager!");
            }
        }
        public void SendChatMessageToIdentityId(string message, long identityId, Color? serverColor = null, string font = null)
        {
            ulong steamId = MySession.Static.Players.TryGetSteamId(identityId);
            if (steamId != 0)
            {
                SendChatMessageToSteamId(message, steamId, serverColor, font);
            }
            else
            {
                Logger.Error($"Could not retrieve SteamId for IdentityId {identityId}!");
            }
        }

        /// <summary>
        /// Retrieves an <see cref="Action"/> used to apply configuration changes. Signals whether its execution will require a full recompute of all tracking data.
        /// </summary>
        /// <param name="needsRecompute">Whether all tracking data will be recomputed by invoking the resulting <see cref="Action"/>.</param>
        /// <returns>The <see cref="Action"/> to invoke for recomputation of tracking data.</returns>
        public Action GetConfigApplicator(out bool needsRecompute)
        {
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