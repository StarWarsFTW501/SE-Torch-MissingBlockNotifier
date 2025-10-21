using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRage.Network;
using Torch.Managers;
using Torch.API;
using SteamKit2.Internal;

namespace TorchPlugin
{
    internal class MyBlockTrackingManager
    {
        public int TotalProxiesOfType(MyTrackableType type)
        {
            int count = 0;
            foreach (var topMost in _topMostTrackables)
                foreach (var trackable in topMost.GetAllProxiesOfType(type))
                    count++;
            return count;
        }
        public string ListTree(int indent = 0, MyTrackable node = null)
        {
            var sb = new StringBuilder();

            var source = node?.Children ?? _topMostTrackables.Select(t => t as MyTrackable);

            foreach (var trackable in source)
            {
                sb.AppendLine($"{new string(' ', indent * 3)}Type: {trackable.TrackableType} -- Identifier: {trackable.GetDisplayName()} -- Children: {trackable.Children.Count()} -- Total leaves: {trackable.ContainedCount}");
                sb.Append(ListTree(indent + 1, trackable));
            }

            return sb.ToString();
        }
        public string ListTrackers()
        {
            var sb = new StringBuilder();
            int num = 1;
            foreach (var tracker in _trackers.Values)
            {
                sb.AppendLine($"Tracker #{num++} - Group: {tracker.Rule.Group} - Target: {tracker.Rule.Group.Type} - Block: {tracker.Rule.TypeId}\\{tracker.Rule.SubtypeName} - Matches for {tracker._matches.Count} entries:");
                foreach (var trackable in tracker._matches)
                    sb.AppendLine($"{new string(' ', 3)}'{trackable.Key.GetDisplayName()}' - {trackable.Value} matches");
            }
            return sb.ToString().TrimEnd();
        }




        public bool IsStarted = false;

        Timer _messageTimer = null;

        /// <summary>
        /// Starts tracking. Initializes the notification <see cref="Timer"/> and loads all <see cref="MyTrackingGroup"/>s from config.
        /// </summary>
        public void Start()
        {
            if (IsStarted)
                return;

            if (Plugin.Instance.Torch?.CurrentSession?.State != Torch.API.Session.TorchSessionState.Loaded)
            {
                Plugin.Instance.Logger.Warning($"Cannot start {nameof(MyBlockTrackingManager)} - No session loaded!");
                return;
            }

            IsStarted = true;

            LoadConfig();

            SetTimer();

            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} started.");
        }
        /// <summary>
        /// Stops tracking. Halts the notification <see cref="Timer"/> and unloads all tracking-related structures.
        /// </summary>
        public void Stop()
        {
            if (!IsStarted)
                return;

            KillTimer();

            UnloadTracking();

            IsStarted = false;

            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} stopped.");
        }

        public void SetTimer()
        {
            if (IsStarted)
            {
                float seconds = Plugin.Instance.Config.TimerSeconds;
                if (seconds == 0)
                {
                    KillTimer();
                }
                else
                {
                    _messageTimer?.Dispose();

                    int timerPeriodMillis = (int)seconds * 1000;
                    if (timerPeriodMillis > 0)
                        _messageTimer = new Timer(_ => ExecuteNotification(), null, timerPeriodMillis, timerPeriodMillis);
                }

                Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} message timer (re)started.");
            }
            else
            {
                KillTimer();
            }
        }
        public void KillTimer()
        {
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} message timer killed.");
            _messageTimer?.Dispose();
            _messageTimer = null;
        }



        /// <summary>
        /// Contains all trackers tracking blocks in the world. Key = rule, Value = tracker
        /// </summary>
        Dictionary<MyTrackingRule, MyBlockTracker> _trackers = new Dictionary<MyTrackingRule, MyBlockTracker>();

        /// <summary>
        /// Contains all trackable objects bound to specific grids by EntityId. Key = The bound grids' entityid, Value = Corresponding trackable object
        /// </summary>
        Dictionary<long, MyTrackable_Grid> _trackablesByGrid = new Dictionary<long, MyTrackable_Grid>();

        /// <summary>
        /// Contains all trackable objects with the largest considered interconnection level
        /// </summary>
        List<MyTrackable_ConnectorStructure> _topMostTrackables = new List<MyTrackable_ConnectorStructure>();

        /// <summary>
        /// Contains all topmost trackables whose descendants have been marked for removal
        /// </summary>
        List<MyTrackable_ConnectorStructure> _topMostTrackablesToCull = new List<MyTrackable_ConnectorStructure>();

        /// <summary>
        /// Contains all grids waiting for registration during the bulk load process. Key = grid entityid, Value = corresponding trackable object. Grids don't have ancestry!
        /// </summary>
        Dictionary<long, MyTrackable_Grid> _gridsWaitingForRegister = new Dictionary<long, MyTrackable_Grid>();

        /// <summary>
        /// Loads all <see cref="MyTrackingRule"/>s from config and creates a <see cref="MyBlockTracker"/> for each. Clears all existing trackers and trackables first.
        /// </summary>
        /// <remarks>
        /// This method also scans all grids in the world. Use sparingly.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if a <see cref="MyBlockTracker"/> is configured for assignment to multiple rules. A tracker only has one rule.</exception>
        public void LoadConfig()
        {
            UnloadTracking();
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - Initializing trackers...");
            foreach (var group in Plugin.Instance.Config.Groups)
            {
                foreach (var rule in group.Rules)
                {
                    rule.Group = group; // handles any new rules (they are only utilized beyond this point)
                    if (!_trackers.ContainsKey(rule))
                    {
                        var tracker = new MyBlockTracker(rule);
                        _trackers[rule] = tracker;
                        rule.AssignedTracker = tracker;
                    }
                    else throw new InvalidOperationException($"Duplicate definition for tracking rule!");
                }
                Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - Trackers for group {group.Name} initialized.");
            }
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - All trackers initialized. Total: {_trackers.Values.Count}");
            LoadTrackables();
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - Loaded.");
        }
        /// <summary>
        /// Unloads all <see cref="MyBlockTracker"/>s and <see cref="MyTrackable"/>s. Clears all existing trackers and trackables.
        /// </summary>
        public void UnloadTracking()
        {
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} UNLOAD - Clearing tracking...");
            _trackablesByGrid.Clear();
            _topMostTrackables.Clear();
            foreach (var tracker in _trackers.Values)
            {
                tracker.UnregisterAllTrackables();
                tracker.Rule.AssignedTracker = null;
            }
            _trackers.Clear();
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} UNLOAD - Tracking cleared.");
        }
        /// <summary>
        /// Registers all existing <see cref="MyCubeGrid"/>s in the world and their neighbours. Assigns to existing <see cref="MyTrackable"/>s or creates new ones.
        /// </summary>
        /// <remarks>
        /// This method scans all (unregistered) grids in the world. Use sparingly. If you want to re-scan registered grids, unregister them first.
        /// </remarks>
        public void LoadTrackables()
        {
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - Loading grids...");
            var allEntities = MyEntities.GetEntities();

            List<MyTrackable_Grid> toScan = new List<MyTrackable_Grid>();

            foreach (var entity in allEntities)
            {
                if (entity is MyCubeGrid grid)
                {
                    RegisterGrid(grid, toScan);
                }
            }

            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} LOAD - Scanning blocks...");
            ScanGrids(toScan);
        }

        /// <summary>
        /// Registers a single <see cref="MyCubeGrid"/> without its neighbours. Assigns to existing <see cref="MyTrackable"/> or creates a new one.
        /// </summary>
        /// <param name="grid">The <see cref="MyCubeGrid"/> to be registered.</param>
        public void RegisterGridSingle(MyCubeGrid grid)
        {
            if (_trackablesByGrid.ContainsKey(grid.EntityId))
            {
                Plugin.Instance.Logger.Info($"Grid '{grid.DisplayName}' ({grid.EntityId}) is already registered - skipping registration.");
                return;
            }

            Plugin.Instance.Logger.Info($"Registering single grid '{grid.DisplayName}'...");
            var gt = new MyTrackable_Grid(grid);
            var construct = new MyTrackable_Construct(new[] { gt });
            var connectorStructure = new MyTrackable_ConnectorStructure(new[] { construct });
            _trackablesByGrid[grid.EntityId] = gt;
            _topMostTrackables.Add(connectorStructure);
            foreach (var tracker in _trackers.Values)
                tracker.RegisterNewTrackable(gt);
        }

        /// <summary>
        /// Registers a <see cref="MyCubeGrid"/> and all its neighbours. Assigns to existing <see cref="MyTrackable"/>s or creates new ones.
        /// </summary>
        /// <remarks>
        /// If you wish to scan an already registered grid, unregister it first! This method does not scan the grid's blocks.
        /// </remarks>
        /// <param name="grid">The <see cref="MyCubeGrid"/> to be registered along with its neighbours.</param>
        /// <param name="trackablesToScan">List to populate with <see cref="MyTrackable"/>s that have been newly initialized and need scanning, or <see langword="null"/> if not required.</param>
        public void RegisterGrid(MyCubeGrid grid, List<MyTrackable_Grid> trackablesToScan = null)
        {
            // if this grid is already registered (for example by having been spawned as a neighbour to a previously handled grid), exit registration
            if (_trackablesByGrid.ContainsKey(grid.EntityId))
            {
                Plugin.Instance.Logger.Info($"Grid '{grid.DisplayName}' ({grid.EntityId}) is already registered - skipping registration.");
                return;
            }

            _gridsWaitingForRegister[grid.EntityId] = new MyTrackable_Grid(grid);
        }

        /// <summary>
        /// Scans all provided <see cref="MyTrackable_Grid"/>s using all registered <see cref="MyBlockTracker"/>s. Does not assign new trackables.
        /// </summary>
        /// <remarks>
        /// This method scans a grid's blocks. Use sparingly. If you wish to rescan an already registered grid, re-register it first!
        /// </remarks>
        /// <param name="trackablesToScan">Collection of <see cref="MyTrackable_Grid"/>s to scan.</param>
        public void ScanGrids(IEnumerable<MyTrackable_Grid> trackablesToScan)
        {
            var jobs = new List<MyBlockTracker.ScanJob>();

            // we have all newly created grid proxies with ancestry assigned - we need to scan them with each tracker
            foreach (var trackable in trackablesToScan)
            {

                foreach (var tracker in _trackers.Values)
                {
                    // grabs a job for this trackable's scanning
                    jobs.Add(tracker.ScanTrackable(trackable));
                }

                ScanGridWithJobs(trackable.Grid, jobs);

                jobs.Clear();
            }
        }

        void ScanGridWithJobs(MyCubeGrid gridToScan, IEnumerable<MyBlockTracker.ScanJob> jobs)
        {
            Plugin.Instance.Logger.Info($"Scanning grid '{gridToScan.DisplayName}' with {jobs.Count()} jobs...");
            // only scan blocks if there is at least one tracker that wants to scan them
            if (jobs.Any())
            {
                // scan all blocks in this grid with all jobs in parallel
                Parallel.ForEach(gridToScan.CubeBlocks, b =>
                {
                    foreach (var job in jobs)
                    {
                        // method calls an externally provided predicate which the documentation directs to be thread-safe,
                        // and atomically increments job's internal counter if predicate returns true
                        job.ScanBlock(b);
                    }
                });
            }

            // finalize each job
            foreach (var job in jobs)
            {
                job.Complete();
            }
        }

        /// <summary>
        /// Finishes registration of <see cref="MyTrackable"/>s under a parent <see cref="MyTrackable"/> of a given <paramref name="type"/>. Creates new <paramref name="parent"/> if null.
        /// </summary>
        /// <param name="parent">Found existing parent <see cref="MyTrackable"/> for given <paramref name="children"/>, or null if a new one is to be created.</param>
        /// <param name="children"><see cref="MyTrackable"/>s to assign as children to <paramref name="parent"/>.</param>
        /// <param name="type">Intended <see cref="MyTrackableType"/> of <paramref name="parent"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if given <paramref name="type"/> does not match type of provided <paramref name="parent"/> or is not a leaf proxy type.</exception>
        private void FinalizeParent(ref MyTrackable parent, IEnumerable<MyTrackable> children, MyTrackableType type)
        {
            if (parent == null)
            {
                switch (type)
                {
                    case MyTrackableType.CONNECTOR_STRUCTURE:
                        parent = new MyTrackable_ConnectorStructure(children);
                        _topMostTrackables.Add((MyTrackable_ConnectorStructure)parent);
                        break;
                    case MyTrackableType.CONSTRUCT:
                        parent = new MyTrackable_Construct(children);
                        break;
                    default:
                        throw new InvalidOperationException($"Cannot finalize creation of parent proxy - '{type}' is a leaf proxy type!");
                }
            }
            else
            {
                if ((type == MyTrackableType.CONNECTOR_STRUCTURE && !(parent is MyTrackable_ConnectorStructure)) || (type == MyTrackableType.CONSTRUCT && !(parent is MyTrackable_Construct)))
                    throw new InvalidOperationException($"Cannot finalize creation of parent proxy - '{type}' does not match type of passed found parent!");
                foreach (var child in children)
                    parent.AddChild(child);
            }
        }


        /// <summary>
        /// Registers a new <see cref="MySlimBlock"/> with the appropriate <see cref="MyTrackable"/> and updates trackers.
        /// </summary>
        /// <param name="parentGrid">Grid the new block belongs to.</param>
        /// <param name="block">The new block.</param>
        public void RegisterNewBlock(MyCubeGrid parentGrid, MySlimBlock block)
        {
            var trackable = _trackablesByGrid[parentGrid.EntityId];
            //Plugin.Instance.Logger.Info($"Registering new block {block.BlockDefinition.Id} at {block.Position} on grid '{parentGrid.DisplayName}' ({parentGrid.EntityId}) in trackable '{trackable.GetDisplayName()}' with {_trackers.Values.Count} trackers...");
            foreach (var tracker in _trackers.Values)
                tracker.RegisterNewBlock(trackable, block);
        }

        /// <summary>
        /// Handles a connection of two <see cref="MyCubeGrid"/>s on a desired level.
        /// </summary>
        /// <param name="grid1">One of the connected grids.</param>
        /// <param name="grid2">One of the connected grids.</param>
        /// <param name="connectionLevel">The <see cref="MyTrackableType"/> level on which grids were connected.</param>
        public void GridsConnected(MyCubeGrid grid1, MyCubeGrid grid2, MyTrackableType connectionLevel)
        {
            if (_gridsWaitingForRegister.ContainsKey(grid2.EntityId))
            {
                MyTrackable t2 = _gridsWaitingForRegister[grid2.EntityId];
                _trackablesByGrid[grid2.EntityId] = (MyTrackable_Grid)t2;

                ConstructAncestryToLevel(ref t2, connectionLevel - 1);

                if (_gridsWaitingForRegister.ContainsKey(grid1.EntityId))
                {
                    MyTrackable t1 = _gridsWaitingForRegister[grid1.EntityId];
                    _trackablesByGrid[grid1.EntityId] = (MyTrackable_Grid)t1;

                    ConstructAncestryToLevel(ref t1, connectionLevel - 1);

                    // create common ancestry at connection level
                    MyTrackable parent = null;
                    FinalizeParent(ref parent, new[] { t1, t2 }, connectionLevel);

                    ConstructAncestryToLevel(ref parent, MyTrackableType.CONNECTOR_STRUCTURE);

                    // register topmost parent
                    _topMostTrackables.Add((MyTrackable_ConnectorStructure)parent);

                    _gridsWaitingForRegister.Remove(grid1.EntityId);
                }
                else
                {
                    MyTrackable t1 = _trackablesByGrid[grid1.EntityId].GetAncestorOfType(connectionLevel)
                        ?? throw new NullReferenceException($"Cannot connect grids - Involved grid '{grid1.DisplayName}' does not have a proxy of appropriate level!");

                    t1.GetAncestorOfType(connectionLevel).AddChild(t2);
                }

                _gridsWaitingForRegister.Remove(grid2.EntityId);
                return;
            }

            if (_gridsWaitingForRegister.ContainsKey(grid1.EntityId))
            {
                MyTrackable t1 = _gridsWaitingForRegister[grid1.EntityId];
                _trackablesByGrid[grid1.EntityId] = (MyTrackable_Grid)t1;

                MyTrackable t2 = _trackablesByGrid[grid2.EntityId].GetAncestorOfType(connectionLevel)
                    ?? throw new NullReferenceException($"Cannot connect grids - Involved grid '{grid2.DisplayName}' does not have a proxy of appropriate level!");

                t2.GetAncestorOfType(connectionLevel).AddChild(t1);

                _gridsWaitingForRegister.Remove(grid1.EntityId);
                return;
            }

            MyTrackable trackable1 = _trackablesByGrid.GetValueOrDefault(grid1.EntityId)?.GetAncestorOfType(connectionLevel)
                ?? throw new NullReferenceException($"Cannot connect grids - Involved grid '{grid1.DisplayName}' does not have a proxy of appropriate level!");
            MyTrackable trackable2 = _trackablesByGrid.GetValueOrDefault(grid2.EntityId)?.GetAncestorOfType(connectionLevel)
                ?? throw new NullReferenceException($"Cannot connect grids - Involved grid '{grid2.DisplayName}' does not have a proxy of appropriate level!");


            Plugin.Instance.Logger.Info($"Merging trackables of type {connectionLevel} - '{trackable1.GetDisplayName()}' <- '{trackable2.GetDisplayName()}'");

            if (grid1 != grid2)
            {
                for (int i = (int)connectionLevel; i < 3; i++)
                {
                    trackable1 = trackable1.GetAncestorOfType((MyTrackableType)i);
                    trackable2 = trackable2.GetAncestorOfType((MyTrackableType)i);

                    if (trackable2 == null || trackable2.MarkedForRemoval || trackable1 == trackable2)
                        break; // they are already connected on this level or hanging ancestry structure without leaves was culled / marked for cull - exit

                    // merge all structures higher or equal to connection level

                    // trackers below the connection level don't care, trackers above are notified by subsequent iterations of this loop if they weren't merged already
                    foreach (var tracker in _trackers.Values)
                        if (tracker.Rule.Type == (MyTrackableType)i)
                            tracker.MergeTrackables(trackable2, trackable1);

                    // move children of trackable2 to trackable1
                    foreach (var child in trackable2.Children)
                    {
                        Plugin.Instance.Logger.Info($"Reassigning child '{child.GetDisplayName()}' of type {child.TrackableType} from '{trackable2.GetDisplayName()}' to '{trackable1.GetDisplayName()}'");
                        trackable1.AddChild(child);
                        Plugin.Instance.Logger.Info($"Child '{child.GetDisplayName()}' reassigned. Is now in {trackable1.GetDisplayName()}? {trackable1.Children.Find(c => c == child) != null} - Parent now '{child.Parent?.GetDisplayName() ?? "<null>"}'");
                    }
                    trackable2.ClearChildren(); // otherwise children would be culled as if they were supposed to be removed, which is not the case


                    // unregister trackable2 and cull its ancestry if it has no more leaves

                    foreach (var t in trackable2.GetHighestSingleAncestor().GetAllProxies())
                        t.MarkedForRemoval = true;

                    _topMostTrackablesToCull.Add((MyTrackable_ConnectorStructure)trackable2.GetAncestorOfType(MyTrackableType.CONNECTOR_STRUCTURE));
                }
            }
        }

        /// <summary>
        /// Handles a disconnect between two <see cref="MyCubeGrid"/>s on a desired level.
        /// </summary>
        /// <remarks>
        /// This method scans <paramref name="grid2"/>'s blocks in order to appropriately adjust trackers. Use sparingly.
        /// </remarks>
        /// <param name="grid1">One of the separated grids. This grid is not scanned.</param>
        /// <param name="grid2">One of the separated grids. This grid is scanned.</param>
        /// <param name="disconnectLevel">The <see cref="MyTrackableType"/> level on which grids were separated.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NullReferenceException"></exception>
        public void GridsDisconnected(MyCubeGrid grid1, MyCubeGrid grid2, MyTrackableType disconnectLevel)
        {
            if (disconnectLevel < (MyTrackableType)1)
                throw new InvalidOperationException($"Cannot disconnect trackables on level lower than {(MyTrackableType)1}!");

            var trackable1 = _trackablesByGrid.GetValueOrDefault(grid1.EntityId)
                ?? throw new NullReferenceException($"Cannot disconnect grids - Involved grid '{grid1.DisplayName}' does not have a grid proxy!");
            var trackable2 = _trackablesByGrid.GetValueOrDefault(grid2.EntityId)
                ?? throw new NullReferenceException($"Cannot disconnect grids - Involved grid '{grid2.DisplayName}' does not have a grid proxy!");

            if (trackable1.MarkedForRemoval && trackable2.MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"Grids '{grid1.DisplayName}' and '{grid2.DisplayName}' are marked for removal! Ignoring disconnect.");
                return;
            }

            // if the grid that separated is already marked for removal, we just unregister it from all trackers that care about this level and above
            if (trackable2.MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"Second grid '{grid2.DisplayName}' is marked for removal! Disconnect equates to unregister from first grid's trackers.");
                var scanJobs = new List<MyBlockTracker.ScanJob>();
                foreach (var tracker in _trackers.Values)
                {
                    if (tracker.Rule.Type >= disconnectLevel)
                    {
                        var job = tracker.UnregisterTrackable(trackable2);
                        if (job != null) // can be null if the tracker is of a lower or equal level to the unregistered trackable, therefore it erases all entries without scanning
                            scanJobs.Add(job);
                    }
                }
                ScanGridWithJobs(grid2, scanJobs);
                return;
            }

            // similarly for the "parent" grid (they are actually equal)
            if (trackable1.MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"First grid '{grid1.DisplayName}' is marked for removal! Disconnect equates to unregister from first grid's trackers.");
                var scanJobs = new List<MyBlockTracker.ScanJob>();
                foreach (var tracker in _trackers.Values)
                {
                    if (tracker.Rule.Type >= disconnectLevel)
                    {
                        var job = tracker.UnregisterTrackable(trackable1);
                        if (job != null) // can be null if the tracker is of a lower or equal level to the unregistered trackable, therefore it erases all entries without scanning
                            scanJobs.Add(job);
                    }
                }
                ScanGridWithJobs(grid1, scanJobs);
                return;
            }

            for (int i = (int)disconnectLevel; i < 3; i++)
            {
                // gotta check if they are still connected (the connection leverages common ancestry in our plugin if already connected but here we need to actually check if they separated with logical/mechaincal connections)

                // list constructs with current appropriate connections to grid2 (and check if they don't include grid1)
                var logicalConnectionsToGrid2 = grid1.GetConnectedGrids(
                    disconnectLevel == MyTrackableType.CONNECTOR_STRUCTURE
                        ? GridLinkTypeEnum.Logical
                        : GridLinkTypeEnum.Mechanical);

                var separatedAncestors = new HashSet<MyTrackable>(); //{ trackable2.GetAncestorOfType((MyTrackableType)(i - 1)) }; // can't forget the grid itself

                var blacklist = new HashSet<long>(); //{ grid2.EntityId };

                MyTrackable ancestor;

                bool areStillConnected = false;

                foreach (var connection in logicalConnectionsToGrid2)
                {
                    if (connection == grid1)
                    {
                        // grids are still connected on the supposed disconnect level - exit
                        areStillConnected = true;
                        break;
                    }
                    if (blacklist.Add(connection.EntityId))
                    {
                        ancestor = _trackablesByGrid.GetValueOrDefault(connection.EntityId)?.GetAncestorOfType((MyTrackableType)(i - 1))
                            ?? throw new InvalidOperationException($"Cannot disconnect grids - Remaining grid '{connection.DisplayName}' does not have a proxy structure!");
                        separatedAncestors.Add(ancestor);
                    }
                }

                if (areStillConnected)
                    break;


                // they no longer have an appropriate connection - separate them on appropriate level:

                // create new parent for grid2
                MyTrackable newProxy = null;
                FinalizeParent(ref newProxy, separatedAncestors, (MyTrackableType)i);

                // copy old "super" parent into new proxy (can be null if separating connector structures)
                newProxy.Parent = trackable1.GetAncestorOfType((MyTrackableType)i).Parent;

                // tell trackers that care about this level that new proxies are to be tracked (we will populate them later)
                foreach (var tracker in _trackers.Values)
                    if (tracker.Rule.Type == (MyTrackableType)i)
                        tracker.RegisterNewTrackable(newProxy);

                // the child constructs have now been added to a parent and their ancestry is correct
            }

            // register movement and creation of new trackable with trackers

            // this call means "some of the stuff in trackable1 belongs to trackable2"

            // if they have the same ancestry on the tracker's level, it doesn't care

            // otherwise, it expects trackable2 to be registered with it

            var jobs = new List<MyBlockTracker.ScanJob>();

            foreach (var tracker in _trackers.Values)
            {
                if (tracker.Rule.Type >= disconnectLevel)
                {
                    var job = tracker.SplitTrackable(trackable1, trackable2);
                    if (job != null) // can be null if the tracker is of a higher level than what we ended up splitting, therefore it desn't care about what changed
                        jobs.Add(job);
                }
            }

            ScanGridWithJobs(grid2, jobs);
        }

        /// <summary>
        /// Unregisters a <see cref="MySlimBlock"/> from tracking. Adjusts the appropriate <see cref="MyTrackable"/>s.
        /// </summary>
        /// <param name="parentGrid">The grid to unregister from.</param>
        /// <param name="block">The block to be unregistered.</param>
        public void UnregisterBlock(MyCubeGrid parentGrid, MySlimBlock block)
        {
            var trackable = _trackablesByGrid[parentGrid.EntityId];
            foreach (var tracker in _trackers.Values)
                tracker.UnregisterBlock(trackable, block);
        }

        /// <summary>
        /// Unregisters a <see cref="MyCubeGrid"/> from tracking. Adjusts the appropriate <see cref="MyTrackable"/>s or removes them.
        /// </summary>
        /// <param name="grid">The grid to be unregistered.</param>
        public void UnregisterGrid(MyCubeGrid grid)
            => UnregisterTrackableInternal(_trackablesByGrid[grid.EntityId]);

        void UnregisterTrackableInternal(MyTrackable trackable)
        {
            trackable = trackable.GetHighestSingleAncestor();

            RemoveTrackableFromTrackers(trackable);

            trackable.MarkedForRemoval = true;

            _topMostTrackablesToCull.Add((MyTrackable_ConnectorStructure)trackable.GetAncestorOfType(MyTrackableType.CONNECTOR_STRUCTURE));
        }


        /// <summary>
        /// Checks registered rules and their trackers and notifies online players accordingly.
        /// </summary>
        public void ExecuteNotification()
        {
            Plugin.Instance.Logger.Info($"{nameof(MyBlockTrackingManager)} - Executing notification...");
            foreach (var group in Plugin.Instance.Config.Groups)
            {
                foreach (var connectorStructure in _topMostTrackables)
                {
                    foreach (var trackable in connectorStructure.GetAllProxiesOfType(group.Type))
                    {
                        group.EnqueueMessageForTrackable(trackable);
                    }
                }
                group.SendQueuedMessages();
            }
        }

        /// <summary>
        /// Finalizes the initialization of trackables registered during bulk load. Scans grids!
        /// </summary>
        public void FinalizeTrackableInits()
        {
            if (!IsStarted)
                return;

            var initializedGrids = new List<long>();

            var jobs = new List<MyBlockTracker.ScanJob>();

            foreach (var kvp in _gridsWaitingForRegister)
            {
                // FinalizeInitForGridTrackable(kvp.Value, trackablesToScan);

                MyTrackable trackable = kvp.Value;
                ConstructAncestryToLevel(ref trackable, MyTrackableType.CONNECTOR_STRUCTURE);

                foreach (var tracker in _trackers.Values)
                {
                    tracker.TryRegisterNewTrackable(kvp.Value);
                    var job = tracker.ScanTrackable(kvp.Value);
                    if (job != null)
                        jobs.Add(job);
                }

                ScanGridWithJobs(kvp.Value.Grid, jobs);
                jobs.Clear();

                initializedGrids.Add(kvp.Key);
            }

            foreach (var id in initializedGrids)
                _gridsWaitingForRegister.Remove(id);
        }
        /*
        void FinalizeInitForGridTrackable(MyTrackable_Grid gridTrackable, List<MyTrackable> trackablesToScan = null)
        {
            var blacklist = new HashSet<MyCubeGrid>();
            var constructGrids = new List<MyCubeGrid>();

            var constructsToAdd = new List<MyTrackable>();

            var trackablesToAdd = new List<MyTrackable>();

            var newTrackables = new List<MyTrackable_Grid>();

            Plugin.Instance.Logger.Info($"Registering grid '{gridTrackable.Grid.DisplayName}' and its neighbours...");


            // this retrieves all grids in the current grid's connector structure, the largest extent of our trackable object ancestry
            var connectorStructureGrids = gridTrackable.Grid.GetConnectedGrids(GridLinkTypeEnum.Logical);

            var sb = new StringBuilder();
            foreach (var connector in connectorStructureGrids)
                sb.AppendLine($" - {connector.DisplayName}");

            Plugin.Instance.Logger.Info($"Found {connectorStructureGrids.Count} connected grids in connector structure:\n{sb.ToString().TrimEnd()}");

            MyTrackable topMostParent = null;
            MyTrackable parent = null;
            Plugin.Instance.Logger.Info($"Logical structure enumeration START - grids: {connectorStructureGrids.Count}");

            foreach (var connectorStructureGrid in connectorStructureGrids)
            {
                // each grid may have been visited as a construct grid of a previously visited grid
                if (blacklist.Add(connectorStructureGrid))
                {
                    Plugin.Instance.Logger.Info($"Newly seen grid: {connectorStructureGrid.DisplayName}");
                    // grid is newly visited = we haven't seen its construct before

                    // this retrieves all grids in the current grid's construct, an immediate descendant of the connector structure
                    connectorStructureGrid.GetConnectedGrids(GridLinkTypeEnum.Mechanical, constructGrids);
                    //constructGrids.Add(connectorStructureGrid);

                    Plugin.Instance.Logger.Info($"Mechanical structure enumeration START - grids: {constructGrids.Count}");
                    foreach (var constructGrid in constructGrids)
                    {
                        // this grid won't have been seen before since it's of a new construct, but we shouldn't visit it next time and think it's a new construct again
                        blacklist.Add(constructGrid);

                        if (_trackablesByGrid.TryGetValue(constructGrid.EntityId, out var trackable))
                        {
                            if (trackable == null)
                                throw new Exception("Unexpected null trackable found in trackables by grid dictionary!");


                            Plugin.Instance.Logger.Info($"Found existing grid proxy for {constructGrid.DisplayName} (proxy named {trackable.Grid.DisplayName}) - setting ancestry.");

                            // this grid is already registered = we already have a hierarchy for this whole construct
                            parent = trackable.Parent;

                            // we also necessarily have a connector structure proxy for everything we do here
                            topMostParent = parent.Parent;
                        }
                        else if (constructGrid == gridTrackable.Grid)
                        {
                            Plugin.Instance.Logger.Info($"Initialization target encountered. Including...");

                            // this is the grid we are currently registering = we already have its proxy, but it has no ancestry yet
                            trackable = gridTrackable;

                            _trackablesByGrid[constructGrid.EntityId] = trackable;

                            trackablesToAdd.Add(trackable);

                            newTrackables.Add(trackable);

                            // register it for assigning of a construct (parent)
                            trackablesToAdd.Add(trackable);
                        }
                        else
                        {
                            Plugin.Instance.Logger.Info("No grid proxy found. Creating...");

                            // this grid is not registered = we make a new proxy
                            trackable = new MyTrackable_Grid(constructGrid);
                            _trackablesByGrid[constructGrid.EntityId] = trackable;

                            // register it for assigning of a construct (parent)
                            trackablesToAdd.Add(trackable);

                            newTrackables.Add(trackable);

                            // register it for scanning by trackers once its ancestry is assigned
                            trackablesToScan?.Add(trackable);

                            Plugin.Instance.Logger.Info($"Grid proxy created and registered for {constructGrid.DisplayName}.");
                        }
                    }

                    constructGrids.Clear();
                    Plugin.Instance.Logger.Info("Mechanical structure enumeration END");

                    // create parent if not present and assign all grid trackables to it
                    FinalizeParent(ref parent, trackablesToAdd, MyTrackableType.CONSTRUCT);

                    // register parent for assigning of topmost parent (connector structure)
                    constructsToAdd.Add(parent);

                    // prepare for next construct creation (= unassign the remembered construct parent and clear its registration list)
                    parent = null;
                    trackablesToAdd.Clear();


                    Plugin.Instance.Logger.Info("Parent finalized - enumeration continuing...");
                }
            }
            Plugin.Instance.Logger.Info("Logical structure enumeration END");

            // all constructs have been initialized properly - we need to group them into a (perhaps new) connector structure
            FinalizeParent(ref topMostParent, constructsToAdd, MyTrackableType.CONNECTOR_STRUCTURE);

            foreach (var trackable in newTrackables)
                foreach (var tracker in _trackers.Values)
                    tracker.TryRegisterNewTrackable(trackable);

            Plugin.Instance.Logger.Info("Connector structure finalized - registration END");
        }
        */

        /// <summary>
        /// Removes all <see cref="MyTrackable"/>s that have been marked for removal and properly unregisters them from this manager's collections.
        /// </summary>
        public void FinalizeTrackableRemovals()
        {
            if (!IsStarted)
                return;

            var toRemoveGrids = new List<long>();

            foreach (var trackable in _topMostTrackablesToCull)
                if (trackable.ExecuteRemoval(toRemoveGrids))
                {
                    _topMostTrackables.RemoveAt(i);
                    foreach (var tracker in _trackers.Values)
                        tracker.UnregisterTrackable(trackable);
                    Plugin.Instance.Logger.Info($"Removed topmost trackable '{trackable.GetDisplayName()}' from tracking manager.");
                }

            _topMostTrackablesToCull.Clear();

            foreach (var key in toRemoveGrids)
            {
                Plugin.Instance.Logger.Info($"Removed trackable for grid '{_trackablesByGrid[key].Grid.DisplayName}' of entity ID '{key}' from tracking manager.");
                _trackablesByGrid.Remove(key);
            }
        }
        
        /// <summary>
        /// Removes a trackable from all trackers. Executes any <see cref="MyBlockTracker.ScanJob"/>s required by the unregister process.
        /// </summary>
        /// <param name="trackable">The <see cref="MyTrackable"/> to be removed from all trackers.</param>
        public void RemoveTrackableFromTrackers(MyTrackable trackable)
        {
            var jobs = new List<MyBlockTracker.ScanJob>();
            foreach (var tracker in _trackers.Values)
            {
                var job = tracker.UnregisterTrackable(trackable);
                if (job != null)
                    jobs.Add(job);
            }
            foreach (var leaf in trackable.GetAllLeafProxies())
                ScanGridWithJobs(leaf.Grid, jobs);
        }

        void ConstructAncestryToLevel(ref MyTrackable trackable, MyTrackableType level)
        {
            MyTrackable parent = null;
            for (int i = 1; i <= (int)level; i++)
            {
                parent = null;
                FinalizeParent(ref parent, new[] { trackable }, (MyTrackableType)i);
                trackable = parent;
            }
        }
    }
}
