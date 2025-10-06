using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal class MyBlockTrackingManager
    {
        /// <summary>
        /// Contains all trackers tracking blocks in the world. Key = rule, Value = tracker
        /// </summary>
        Dictionary<string, MyBlockTracker> _trackers = new Dictionary<string, MyBlockTracker>();

        /// <summary>
        /// Contains all trackable objects tracked by the plugin by grid ID. Key = A contained grid's entityid, Value = Corresponding trackable object
        /// </summary>
        Dictionary<long, MyTrackable> _trackablesByGrid = new Dictionary<long, MyTrackable>();
        /// <summary>
        /// Contains all trackable objects tracked by the plugin.
        /// </summary>
        List<MyTrackable> _trackables = new List<MyTrackable>();

        public void LoadConfig(string config)
        {
            var rules = config.Split(',').Select(r => r.Trim()).ToHashSet();

            foreach (var rule in rules)
            {
                if (!_trackers.ContainsKey(rule))
                {
                    var tracker = new MyBlockTracker(rule);
                    _trackers[rule] = tracker;
                }
                else throw new InvalidOperationException($"Duplicate definition for tracking rule '{rule}'!");
            }

            var allEntities = MyEntities.GetEntities();
        }

        /// <summary>
        /// Registers a new <see cref="MyCubeGrid"/> upon creation in the world. Assigns to an existing <see cref="MyTrackable"/> or creates a new one.
        /// </summary>
        /// <param name="grid">The grid to be registered.</param>
        public void RegisterNewGrid(MyCubeGrid grid)
        {
            var trackable = FindTrackableForRegistration(grid);
            if (trackable != null)
            {
                (trackable as MyMultiGridTrackable).AddGrid(grid);
            }
            else
            {
                trackable = CreateNewTrackable(grid);
            }
            _trackablesByGrid[grid.EntityId] = trackable;
        }

        /// <summary>
        /// Registers a new <see cref="MySlimBlock"/> with the appropriate <see cref="MyTrackable"/> and updates trackers
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
        {
            if (Plugin.Instance.Config.TrackableType == MyTrackableType.CONNECTOR_STRUCTURE)
            {
                var trackable1 = _trackablesByGrid[grid1.EntityId];
                var trackable2 = _trackablesByGrid[grid2.EntityId];
                if (trackable1 != trackable2)
                {
                    foreach (var tracker in _trackers.Values)
                    {
                        tracker.MergeTrackables(trackable2, trackable1);
                    }
                    _trackablesByGrid[grid2.EntityId] = trackable1;
                }
            }
        }

        /// <summary>
        /// Handles a connection between two grids via base-head subgrid connection. Merges the appropriate <see cref="MyTrackable"/>s.
        /// </summary>
        /// <param name="grid1">One of the connected grids.</param>
        /// <param name="grid2">One of the connected grids.</param>
        public void GridsConnectedBySubgrid(MyCubeGrid grid1, MyCubeGrid grid2)
        {
            if (Plugin.Instance.Config.TrackableType == MyTrackableType.CONSTRUCT || Plugin.Instance.Config.TrackableType == MyTrackableType.CONNECTOR_STRUCTURE)
            {
                var trackable1 = _trackablesByGrid[grid1.EntityId];
                var trackable2 = _trackablesByGrid[grid2.EntityId];
                if (trackable1 != trackable2)
                {
                    foreach (var tracker in _trackers.Values)
                    {
                        tracker.MergeTrackables(trackable2, trackable1);
                    }
                    _trackablesByGrid[grid2.EntityId] = trackable1;
                }
            }
        }

        /// <summary>
        /// Unregisters a <see cref="MyCubeGrid"/> from tracking. Adjusts the appropriate <see cref="MyTrackable"/> or removes it.
        /// </summary>
        /// <param name="grid">The grid to be unregistered.</param>
        public void UnregisterGrid(MyCubeGrid grid)
        {
            var trackable = _trackablesByGrid[grid.EntityId];
            if (trackable is MyMultiGridTrackable multiGridTrackable)
            {
                multiGridTrackable.RemoveGrid(grid);
                if (multiGridTrackable.ContainedGrids.Count != 0)
                {
                    foreach (var tracker in _trackers.Values)
                        tracker.RemoveGridFromTrackable(trackable, grid);
                    return;
                }
            }
            foreach (var tracker in _trackers.Values)
                tracker.UnregisterTrackable(trackable);
        }

        private MyTrackable FindTrackableForRegistration(MyCubeGrid grid)
        {
            switch (Plugin.Instance.Config.TrackableType)
            {
                case MyTrackableType.CONNECTOR_STRUCTURE:
                case MyTrackableType.CONSTRUCT:
                    var grids = grid.GetConnectedGrids(VRage.Game.ModAPI.GridLinkTypeEnum.Mechanical);
                    foreach (var mechanicalGrid in grids)
                        foreach (var trackable in _trackablesByGrid.Values)
                            if (trackable is MyMultiGridTrackable multiGridTrackable && multiGridTrackable.ContainedGrids.Contains(mechanicalGrid))
                                return trackable;
                    break;
            }
            return null;
        }

        private MyTrackable CreateNewTrackable(MyCubeGrid grid)
        {
            switch (Plugin.Instance.Config.TrackableType)
            {
                case MyTrackableType.CONNECTOR_STRUCTURE:
                    return new MyTrackable_ConnectorStructure(grid);
                case MyTrackableType.CONSTRUCT:
                    return new MyTrackable_Construct(grid);
                case MyTrackableType.GRID:
                    return new MyTrackable_Grid(grid);
                default:
                    throw new NotImplementedException($"No trackable could be instantiated for set trackable type (enum) '{Plugin.Instance.Config.TrackableType}'!");
            }
        }
    }
}
