using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal abstract class MyTrackable
    {
    }
    internal abstract class MyMultiGridTrackable : MyTrackable
    {
        public List<MyCubeGrid> ContainedGrids { get; protected set; }
        public MyMultiGridTrackable(MyCubeGrid grid)
        {
        }
        /// <summary>
        /// Adds a grid to this trackable.
        /// </summary>
        /// <param name="grid">Grid to add to the trackable.</param>
        public void AddGrid(MyCubeGrid grid)
        {
            ContainedGrids.Add(grid);
        }
        public void RemoveGrid(MyCubeGrid grid)
        {
            ContainedGrids.Remove(grid);
        }
    }

    /// <summary>
    /// Represents a trackable body comprised of grids interconnected by subgrid connections
    /// </summary>
    internal class MyTrackable_Construct : MyMultiGridTrackable
    {
        public MyTrackable_Construct(MyCubeGrid grid) : base(grid) { }
    }

    internal class MyTrackable_ConnectorStructure : MyMultiGridTrackable
    {
        public MyTrackable_ConnectorStructure(MyCubeGrid grid) : base(grid) { }
    }

    internal class MyTrackable_Grid : MyTrackable
    {
        public MyCubeGrid Grid { get; protected set; }
        public MyTrackable_Grid(MyCubeGrid grid)
        {
            Grid = grid;
            Id = grid.EntityId;
        }
    }
    public enum MyTrackableType
    {
        CONSTRUCT,
        CONNECTOR_STRUCTURE,
        GRID
    }
}
