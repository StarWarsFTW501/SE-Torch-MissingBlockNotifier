using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace TorchPlugin
{
    public class MyCommands : CommandModule
    {
        /// <summary>
        /// Responds to the issued command in chat.
        /// </summary>
        /// <param name="message">Message to respond with.</param>
        void Respond(string message)
        {
            Context?.Respond(message);
        }

        [Command("notifier notify", "Immediately notifies all players of current violations of set tracking groups.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Notify()
        {
            Respond("Notifying all players of current violations of set tracking groups...");
            Plugin.Instance.TrackingManager.ExecuteNotification();
            Respond("Players notified.");
        }
        [Command("notifier listgroups", "Lists all currently active tracking groups.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListGroups()
        {
            foreach (var group in Plugin.Instance.Config.Groups)
            {
                Respond($"{group.Name} -- Type: {group.Type} -- Mode: {group.Mode} -- Rules: {group.Rules.Count}");
            }
        }
        [Command("notifier listrules", "Lists all currently active rules for given tracking group.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListGroups(string groupName)
        {
            foreach (var group in Plugin.Instance.Config.Groups)
            {
                if (group.Name == groupName)
                {
                    foreach (var rule in group.Rules)
                        Respond($"{rule.TypeId}\\{rule.SubtypeName} -- Mode: {rule.Mode} -- Target matches: {rule.TargetMatches}");
                    return;
                }
            }
            Respond($"No group found with name '{groupName}'.");
        }
        [Command("notifier listtree", "Lists all entries in the tracking hierarchy.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListTree()
        {
            Respond($"Listing all registered proxies in the tracking hierarchy:\n{Plugin.Instance.TrackingManager.ListTree()}\nTotal connector structures: {Plugin.Instance.TrackingManager.TotalProxiesOfType(MyTrackableType.CONNECTOR_STRUCTURE)}\nTotal constructs: {Plugin.Instance.TrackingManager.TotalProxiesOfType(MyTrackableType.CONSTRUCT)}\nTotal grids: {Plugin.Instance.TrackingManager.TotalProxiesOfType(MyTrackableType.GRID)}");
        }
        [Command("notifier listtrackers", "Lists all running trackers, the names of their bound groups, target blocks and information about tracked proxies.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ListTrackers()
        {
            Respond($"Listing all registered trackers and their matches:\n{Plugin.Instance.TrackingManager.ListTrackers()}");
        }
        [Command("notifier apply", "Applies unsaved changes to the configuration.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Apply()
        {
            if (!Plugin.Instance.Config.HasChanges)
            {
                Respond("No changes to apply.");
                return;
            }
            var sb = new StringBuilder("Applying changes to:");
            foreach (var prop in Plugin.Instance.Config.ChangedProperties)
                sb.Append($" {prop},");
            Respond(sb.ToString().TrimEnd(','));
            Plugin.Instance.GetConfigApplicator(out _)();
            Respond("Configuration applied.");
        }
        [Command("notifier reload", "Reloads tracking data for all trackable entities on the server.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Reload()
        {
            if (!Plugin.Instance.TrackingManager.IsStarted)
            {
                Respond("Tracking manager is not started. Cannot reload tracking data.");
                return;
            }
            Respond("Reloading tracking data for all trackable entities on the server...");
            Plugin.Instance.TrackingManager.LoadConfig();
            Respond("Tracking data reloaded.");
        }
        [Command("notifier start", "Starts the tracking manager and loads tracking data.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Start()
        {
            if (Plugin.Instance.TrackingManager.IsStarted)
            {
                Respond("Tracking manager is already started.");
                return;
            }
            Respond("Starting tracking manager and loading tracking data...");
            Plugin.Instance.TrackingManager.Start();
            Respond("Tracking manager started.");
        }
        [Command("notifier stop", "Stops the tracking manager and unloads all tracking data.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Stop()
        {
            if (!Plugin.Instance.TrackingManager.IsStarted)
            {
                Respond("Tracking manager is not started.");
                return;
            }
            Respond("Stopping tracking manager...");
            Plugin.Instance.TrackingManager.Stop();
            Respond("Tracking manager stopped.");
        }
    }
}
