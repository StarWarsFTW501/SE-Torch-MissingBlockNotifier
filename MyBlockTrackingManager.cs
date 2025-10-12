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

namespace TorchPlugin
{
    internal class MyBlockTrackingManager
    {
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
        /// Loads all <see cref="MyTrackingRule"/>s from config and creates a <see cref="MyBlockTracker"/> for each. Clears all existing trackers and trackables first.
        /// </summary>
        /// <remarks>
        /// This method also scans all grids in the world. Use sparingly.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if a <see cref="MyBlockTracker"/> is configured for assignment to multiple rules. A tracker only has one rule.</exception>
        public void LoadConfig()
        {
            Unload();
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
            }
            LoadTrackables();
        }
        /// <summary>
        /// Unloads all <see cref="MyBlockTracker"/>s and <see cref="MyTrackable"/>s. Clears all existing trackers and trackables.
        /// </summary>
        public void Unload()
        {
            _trackablesByGrid.Clear();
            foreach (var tracker in _trackers.Values)
            {
                tracker.UnregisterAllTrackables();
                tracker.Rule.AssignedTracker = null;
            }
            _trackers.Clear();
        }
        /// <summary>
        /// Registers all existing <see cref="MyCubeGrid"/>s in the world and their neighbours. Assigns to existing <see cref="MyTrackable"/>s or creates new ones.
        /// </summary>
        /// <remarks>
        /// This method scans all (unregistered) grids in the world. Use sparingly. If you want to re-scan registered grids, unregister them first.
        /// </remarks>
        public void LoadTrackables()
        {
            var allEntities = MyEntities.GetEntities();

            List<MyTrackable_Grid> toScan = new List<MyTrackable_Grid>();
            
            foreach (var entity in allEntities)
            {
                if (entity is MyCubeGrid grid)
                {
                    RegisterGrid(grid, toScan);
                }
            }

            ScanGrids(toScan);
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
                return;

            var blacklist = new HashSet<MyCubeGrid>();
            var constructGrids = new List<MyCubeGrid>();

            var constructsToAdd = new List<MyTrackable>();

            var trackablesToAdd = new List<MyTrackable>();

            // this retrieves all grids in the current grid's connector structure, the largest extent of our trackable object ancestry
            var connectorStructureGrids = grid.GetConnectedGrids(GridLinkTypeEnum.Logical);

            MyTrackable topMostParent = null;
            MyTrackable parent = null;

            foreach (var connectorStructureGrid in connectorStructureGrids)
            {
                // each grid may have been visited as a construct grid of a previously visited grid
                if (blacklist.Add(connectorStructureGrid))
                {
                    // grid is newly visited = we haven't seen its construct before

                    // this retrieves all grids in the current grid's construct, an immediate descendant of the connector structure
                    grid.GetConnectedGrids(GridLinkTypeEnum.Mechanical, constructGrids);
                    constructGrids.Add(connectorStructureGrid);

                    foreach (var constructGrid in constructGrids)
                    {
                        // this grid won't have been seen before since it's of a new construct, but we shouldn't visit it next time and think it's a new construct again
                        blacklist.Add(constructGrid);

                        if (_trackablesByGrid.TryGetValue(constructGrid.EntityId, out var trackable))
                        {
                            // this grid is already registered = we already have a hierarchy for this whole construct
                            parent = trackable.Parent;

                            // we also necessarily have a connector structure proxy for everything we do here
                            topMostParent = parent.Parent;
                        }
                        else
                        {
                            // this grid is not registered = we make a new proxy
                            trackable = new MyTrackable_Grid(grid);
                            _trackablesByGrid[grid.EntityId] = trackable;

                            // register it for assigning of a construct (parent)
                            trackablesToAdd.Add(trackable);

                            // register it for scanning by trackers once its ancestry is assigned
                            trackablesToScan?.Add(trackable);
                        }
                    }

                    // create parent if not present and assign all grid trackables to it
                    FinalizeParent(parent, trackablesToAdd, MyTrackableType.CONSTRUCT);

                    // register parent for assigning of topmost parent (connector structure)
                    constructsToAdd.Add(parent);

                    // prepare for next construct creation (= unassign the remembered construct parent and clear its registration list)
                    parent = null;
                    trackablesToAdd.Clear();
                }
            }

            // all constructs have been initialized properly - we need to group them into a (perhaps new) connector structure
            FinalizeParent(topMostParent, constructsToAdd, MyTrackableType.CONNECTOR_STRUCTURE);
        }
        /// <summary>
        /// Scans all provided <see cref="MyTrackable_Grid"/>s using all registered <see cref="MyBlockTracker"/>s. Does not assign new trackables.
        /// </summary>
        /// <remarks>
        /// This method scans a grid's blocks. Use sparingly. If you wish to rescan an already registered grid, re-register it first!
        /// </remarks>
        /// <param name="trackablesToScan"></param>
        public void ScanGrids(IEnumerable<MyTrackable_Grid> trackablesToScan)
        {
            var jobs = new List<MyBlockTracker.ScanJob>();

            // we have all newly created grid proxies with ancestry assigned - we need to scan them with each tracker
            foreach (var trackable in trackablesToScan)
            {
                foreach (var tracker in _trackers.Values)
                {
                    // registers trackable in each tracker
                    tracker.RegisterNewTrackable(trackable);

                    // grabs a job for this trackable's scanning
                    jobs.Add(tracker.ScanTrackable(trackable));
                }

                // only scan blocks if there is at least one tracker that wants to scan them
                if (jobs.Count != 0)
                {
                    // scan all blocks in this grid with all jobs in parallel
                    Parallel.ForEach(trackable.Grid.CubeBlocks, b =>
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

                jobs.Clear();
            }
        }
        /// <summary>
        /// Finishes registration of <see cref="MyTrackable"/>s under a parent <see cref="MyTrackable"/> of a given <paramref name="type"/>. Creates new <paramref name="parent"/> if null.
        /// </summary>
        /// <param name="parent">Found existing parent <see cref="MyTrackable"/> for given <paramref name="children"/>, or null if a new one is to be created.</param>
        /// <param name="children"><see cref="MyTrackable"/>s to assign as children to <paramref name="parent"/>.</param>
        /// <param name="type">Intended <see cref="MyTrackableType"/> of <paramref name="parent"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if given <paramref name="type"/> does not match type of provided <paramref name="parent"/> or is not a leaf proxy type.</exception>
        private void FinalizeParent(MyTrackable parent, IEnumerable<MyTrackable> children, MyTrackableType type)
        {
            if (parent == null)
            {
                switch (type)
                {
                    case MyTrackableType.CONNECTOR_STRUCTURE:
                        parent = new MyTrackable_ConnectorStructure(children);
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
                {
                    child.Parent = parent;
                }
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
            foreach (var tracker in _trackers.Values)
                tracker.RegisterNewBlock(trackable, block);
        }

        /// <summary>
        /// Handles a connection between two grids via connector. Merges the appropriate <see cref="MyTrackable"/>s.
        /// </summary>
        /// <param name="grid1">One of the connected grids.</param>
        /// <param name="grid2">One of the connected grids.</param>
        public void GridsConnectedByConnector(MyCubeGrid grid1, MyCubeGrid grid2)
            => TrackablesConnected(
                _trackablesByGrid[grid1.EntityId].GetAncestorOfType(MyTrackableType.CONNECTOR_STRUCTURE),
                _trackablesByGrid[grid2.EntityId].GetAncestorOfType(MyTrackableType.CONNECTOR_STRUCTURE));

        /// <summary>
        /// Handles a connection between two grids via base-head subgrid connection. Merges the appropriate <see cref="MyTrackable"/>s.
        /// </summary>
        /// <param name="grid1">One of the connected grids.</param>
        /// <param name="grid2">One of the connected grids.</param>
        public void GridsConnectedBySubgrid(MyCubeGrid grid1, MyCubeGrid grid2)
            => TrackablesConnected(
                _trackablesByGrid[grid1.EntityId].GetAncestorOfType(MyTrackableType.CONSTRUCT),
                _trackablesByGrid[grid2.EntityId].GetAncestorOfType(MyTrackableType.CONSTRUCT));

        private void TrackablesConnected(MyTrackable trackable1, MyTrackable trackable2)
        {
            if (trackable1 != trackable2)
            {
                foreach (var tracker in _trackers.Values)
                {
                    tracker.MergeTrackables(trackable2, trackable1);
                }
                UnregisterTrackable(trackable2);
            }
        }

        public void GridsDisconnectedByConnector(MyCubeGrid grid1, MyCubeGrid grid2)
        {
            // check if they are still connected (the connection leverages common ancestry in our plugin if already connected but here we need to actually check if they separated with logical/mechaincal connections)
        }
        public void GridsDisconnectedBySubgrid(MyCubeGrid grid1, MyCubeGrid grid2)
        {
            // check if they are still connected (again, connection has ancestry, but here mechanical checks are needed)
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
            => UnregisterTrackable(_trackablesByGrid[grid.EntityId]);

        private void UnregisterTrackable(MyTrackable trackable)
        {
            trackable = trackable.GetHighestSingleAncestor();

            foreach (var tracker in _trackers.Values)
                tracker.UnregisterTrackable(trackable);

            trackable.Parent?.RemoveChild(trackable);

            if (trackable is MyTrackable_Grid grid)
                _trackablesByGrid.Remove(grid.Grid.EntityId);
            if (trackable is MyTrackable_ConnectorStructure connectorStructure)
                _topMostTrackables.Remove(connectorStructure);
        }


        /// <summary>
        /// Checks registered rules and their trackers and notifies online players accordingly.
        /// </summary>
        public void ExecuteNotification()
        {
            foreach (var group in Plugin.Instance.Config.Groups)
            {
                foreach (var connectorStructure in _topMostTrackables)
                {
                    foreach (var trackable in connectorStructure.GetAllProxiesOfType(group.Type))
                    {
                        
                    }
                }
            }
        }
    }
}
