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
        /// Contains all trackable objects tracked by the plugin.
        /// </summary>
        //List<MyTrackable> _trackables = new List<MyTrackable>();

        /// <summary>
        /// Contains all trackable objects with the largest considered interconnection level
        /// </summary>
        List<MyTrackable_ConnectorStructure> _topMostTrackables = new List<MyTrackable_ConnectorStructure>();

        public void LoadConfig()
        {
            _trackers.Clear();
            foreach (var rule in Plugin.Instance.Config.Rules)
            {
                if (!_trackers.ContainsKey(rule))
                {
                    var tracker = new MyBlockTracker(rule);
                    _trackers[rule] = tracker;
                }
                else throw new InvalidOperationException($"Duplicate definition for tracking rule '{rule.Name}'!");
            }
            LoadTrackables();
        }
        public void UnloadTrackables()
        {
            _trackablesByGrid.Clear();
            foreach (var tracker in _trackers)
                tracker.Value.UnregisterAllTrackables();
        }
        public void LoadTrackables()
        {
            var allEntities = MyEntities.GetEntities();
            
            foreach (var entity in allEntities)
            {
                if (entity is MyCubeGrid grid)
                {
                    OnNewGrid(grid);
                }
            }
        }
        public void Reload()
        {
            _trackablesByGrid.Clear();
            LoadConfig();
        }


        /// <summary>
        /// Registers a new <see cref="MyCubeGrid"/> and all its neighbours. Assigns to existing <see cref="MyTrackable"/>s or creates new ones.
        /// </summary>
        /// <param name="grid">The <see cref="MyCubeGrid"/> to be registered along with its neighbours.</param>
        public void OnNewGrid(MyCubeGrid grid)
        {
            // if this grid is already registered (for example by having been spawned as a neighbour to a previously handled grid), exit registration
            if (_trackablesByGrid.ContainsKey(grid.EntityId))
                return;

            var blacklist = new HashSet<MyCubeGrid>();
            var connectorStructureGrids = grid.GetConnectedGrids(GridLinkTypeEnum.Logical);
            var constructGrids = new List<MyCubeGrid>();

            var constructs = new List<MyTrackable_Construct>();

            var trackablesToAdd = new List<MyTrackable>();

            MyTrackable topMostParent = null;
            MyTrackable parent = null;

            foreach (var connectorStructureGrid in connectorStructureGrids)
            {
                // connector structure grids are also 
                if (blacklist.Add(connectorStructureGrid))
                {
                    // grid is newly visited = we haven't seen its construct before

                    // take all grids in the construct
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
                        }

                        trackablesToAdd.Add(trackable);
                    }

                    FinalizeParent(parent, trackablesToAdd, MyTrackableType.CONSTRUCT);

                    parent = null;
                    trackablesToAdd.Clear();
                }
            }

            FinalizeParent(topMostParent, constructs, MyTrackableType.CONNECTOR_STRUCTURE);
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
    }
}
