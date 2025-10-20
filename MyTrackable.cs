using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    public abstract class MyTrackable
    {
        public MyTrackableType TrackableType { get; protected set; }
        public MyTrackable Parent = null;
        public List<MyTrackable> Children;

        public bool MarkedForRemoval = false;

        protected int _containedCount;
        public int ContainedCount => _containedCount;

        public void IncreaseContainedCount(int byAmount)
        {
            _containedCount += byAmount;
            Parent?.IncreaseContainedCount(byAmount);
        }
        public void DecreaseContainedCount(int byAmount)
        {
            _containedCount -= byAmount;
            Parent?.DecreaseContainedCount(byAmount);
        }

        /// <summary>
        /// If marked for removal, kills self and all descendants, otherwise kills all descendants marked for removal, and if all descendants are removed, kills self as well. This includes removal from parent, if any exists. Returns whether this <see cref="MyTrackable"/> was removed.
        /// </summary>
        /// <param name="killedGridIds">Optional list to populate with IDs of killed <see cref="MyTrackable_Grid"/>s.</param>
        /// <returns>Whether this <see cref="MyTrackable"/> killed itself and should be removed.</returns>
        public virtual bool ExecuteRemoval(List<long> killedGridIds = null)
        {
            if (MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"Trackable '{GetDisplayName()}' marked for removal - razing tree...");
                foreach (var proxy in GetAllLeafProxies())
                {
                    proxy.MarkedForRemoval = true;
                    killedGridIds?.Add(proxy.Grid.EntityId);
                }
                Plugin.Instance.TrackingManager.RemoveTrackableFromTrackers(this);
                RazeTree();
                Parent?.RemoveChild(this);
                return true;
            }
            // Plugin.Instance.Logger.Info($"Trackable '{GetDisplayName()}' is not marked for removal - enumerating children...");
            MarkedForRemoval = true;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var child = Children[i];
                if (!child.ExecuteRemoval(killedGridIds))
                    MarkedForRemoval = false;
            }

            if (MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"All children of trackable '{GetDisplayName()}' were removed - forcing mark for removal and repeating...");
                return ExecuteRemoval(killedGridIds);
            }

            return false;
        }

        /// <summary>
        /// Walks the hierarchy downwards to find all (leaf and branching) <see cref="MyTrackable"/> descendants.
        /// </summary>
        /// <returns><see cref="IEnumerable{MyTrackable}"/> of all proxies in the hierarchy below this <see cref="MyTrackable"/>.</returns>
        public IEnumerable<MyTrackable> GetAllProxies()
        {
            yield return this;
            foreach (var child in Children)
                foreach (var proxy in child.GetAllProxies())
                    yield return proxy;
        }
        /// <summary>
        /// Walks the hierarchy downwards to find all leaf <see cref="MyTrackable"/> descendants.
        /// </summary>
        /// <returns><see cref="IEnumerable{MyTrackable}"/> of all leaf proxies in the hierarchy below this <see cref="MyTrackable"/>.</returns>
        public virtual IEnumerable<MyTrackable_Grid> GetAllLeafProxies()
        {
            foreach (var child in Children)
                foreach (var trackable in child.GetAllLeafProxies())
                    yield return trackable;
        }
        /// <summary>
        /// Walks the hierarchy downwards to find all <see cref="MyTrackable"/>s of type <paramref name="type"/>.
        /// </summary>
        /// <param name="type"><see cref="MyTrackableType"/> of proxies to find.</param>
        /// <returns><see cref="IEnumerable{MyTrackable}"/> of all proxies in the hierarchy below this <see cref="MyTrackable"/>.</returns>
        public IEnumerable<MyTrackable> GetAllProxiesOfType(MyTrackableType type)
        {
            if (TrackableType == type)
                yield return this;
            else foreach (var child in Children)
                    foreach (var proxy in child.GetAllProxiesOfType(type))
                        yield return proxy;
        }
        /// <summary>
        /// Walks the hierarchy upwards to find the closest <see cref="MyTrackable"/> ancestor of type <paramref name="type"/>.
        /// </summary>
        /// <param name="type"><see cref="MyTrackableType"/> of ancestor to find.</param>
        /// <returns><see cref="MyTrackable"/> closest ancestor (or self) matching <paramref name="type"/>.</returns>
        public MyTrackable GetAncestorOfType(MyTrackableType type)
        {
            if (TrackableType == type)
                return this;
            return Parent?.GetAncestorOfType(type);
        }
        /// <summary>
        /// Walks the hierarchy upwards to find the highest possible <see cref="MyTrackable"/> whose this <see cref="MyTrackable"/> is the sole descendant.
        /// </summary>
        /// <returns><see cref="MyTrackable"/> furthest ancestor (or self) whose this is the sole descendant.</returns>
        public MyTrackable GetHighestSingleAncestor()
        {
            return Parent == null || Parent.Children.Count() != 1 ? this : Parent.GetHighestSingleAncestor();
        }

        /// <summary>
        /// Removes a <paramref name="child"/> from this <see cref="MyTrackable"/>.
        /// </summary>
        /// <param name="child"><see cref="MyTrackable"/> child to remove.</param>
        public void RemoveChild(MyTrackable child)
        {
            if (Children.Remove(child))
                DecreaseContainedCount(child.ContainedCount);
        }

        /// <summary>
        /// Adds a <paramref name="child"/> to this <see cref="MyTrackable"/> and reassigns its ancestor appropriately. Does not remove <paramref name="child"/> from previous ancestry!
        /// </summary>
        /// <param name="child"><see cref="MyTrackable"/> child to add.</param>
        public void AddChild(MyTrackable child)
        {
            if (MarkedForRemoval)
                throw new InvalidOperationException("Cannot add child to a trackable marked for removal!");
            Children.Add(child);
            child.Parent = this;
            IncreaseContainedCount(child.ContainedCount);
        }

        /// <summary>
        /// Clears all children from this <see cref="MyTrackable"/>. Does not remove ancestry links from children!
        /// </summary>
        public void ClearChildren()
        {
            DecreaseContainedCount(Children.Sum(c => c.ContainedCount));
            Children.Clear();
        }

        /// <summary>
        /// Destroys all ancestry links in the hierarchy below this <see cref="MyTrackable"/>. Does not take care of references to this <see cref="MyTrackable"/> from above in the hierarchy!
        /// </summary>
        public void RazeTree()
        {
            DecreaseContainedCount(Children.Sum(c => c.ContainedCount));
            foreach (var child in Children)
                child.RazeTree();
            Children.Clear();
        }

        /// <summary>
        /// Generates a human-readable identifier of this <see cref="MyTrackable"/> with the largest <see cref="MyCubeGrid"/>'s name and potentially number of other connected <see cref="MyCubeGrid"/>s.
        /// </summary>
        /// <returns>A human-readable identifier of this object including the name of the largest <see cref="MyCubeGrid"/>.</returns>
        public string GetDisplayName()
        {
            if (MarkedForRemoval)
                return $"<REMOVED TRACKABLE (owned {ContainedCount} leaves)>";

            MyCubeGrid largestGrid = null;
            int maxSize = 0;
            foreach (var grid in GetAllLeafProxies())
            {
                if (grid.Grid.BlocksCount > maxSize)
                {
                    maxSize = grid.Grid.BlocksCount;
                    largestGrid = grid.Grid;
                }
            }

            if (largestGrid == null)
                throw new NullReferenceException($"Result of largest {nameof(MyCubeGrid)} search in trackable was NULL! Cannot generate trackable name! Please disable the plugin and contact author!");

            if (ContainedCount > 1)
                return $"{largestGrid.DisplayName} (and {ContainedCount - 1} other grids on {(TrackableType == MyTrackableType.CONSTRUCT ? "subgrids" : "connectors/subgrids")})";

            return largestGrid.DisplayName;
        }
    }

    /// <summary>
    /// Represents a trackable body comprised of a single grid. The simplest trackable body.
    /// </summary>
    public class MyTrackable_Grid : MyTrackable
    {
        public MyCubeGrid Grid { get; protected set; }
        public MyTrackable_Grid(MyCubeGrid grid)
        {
            Grid = grid;
            Children = new List<MyTrackable>();
            TrackableType = MyTrackableType.GRID;
            _containedCount = 1;
        }
        public override IEnumerable<MyTrackable_Grid> GetAllLeafProxies()
        {
            return new MyTrackable_Grid[] { this };
        }

        /// <summary>
        /// If marked for removal, kills self and all descendants, otherwise kills all descendants marked for removal, and if all descendants are removed, kills self as well. This includes removal from parent, if any exists. Returns whether this <see cref="MyTrackable"/> was removed.
        /// </summary>
        /// <param name="killedGridIds">Optional list to populate with IDs of killed <see cref="MyTrackable_Grid"/>s.</param>
        /// <returns>Whether this <see cref="MyTrackable"/> killed itself and should be removed.</returns>
        public override bool ExecuteRemoval(List<long> killedGridIds = null)
        {
            if (MarkedForRemoval)
            {
                Plugin.Instance.Logger.Info($"Grid trackable '{Grid.DisplayName}' marked for removal - killing self...");
                Plugin.Instance.TrackingManager.RemoveTrackableFromTrackers(this);
                killedGridIds?.Add(Grid.EntityId);
                Parent?.RemoveChild(this);
                return true;
            }
            return false;
        }
    }
    /// <summary>
    /// Represents a trackable body comprised of grids interconnected by subgrid connections.
    /// </summary>
    public class MyTrackable_Construct : MyTrackable
    {
        public MyTrackable_Construct(IEnumerable<MyTrackable> grids)
        {
            Children = grids.ToList();
            _containedCount = 0;
            foreach (var child in Children)
            {
                child.Parent = this;
                _containedCount += child.ContainedCount;
            }
            TrackableType = MyTrackableType.CONSTRUCT;
        }
    }
    /// <summary>
    /// Represents a trackable body comprised of grids interconnected by connectors. The most complex trackable body.
    /// </summary>
    public class MyTrackable_ConnectorStructure : MyTrackable
    {
        public MyTrackable_ConnectorStructure(IEnumerable<MyTrackable> constructs)
        {
            Children = constructs.ToList();
            _containedCount = 0;
            foreach (var child in Children)
            {
                child.Parent = this;
                _containedCount += child.ContainedCount;
            }
            TrackableType = MyTrackableType.CONNECTOR_STRUCTURE;
        }
    }

    public enum MyTrackableType
    {
        /// <summary>
        /// A <see cref="MyTrackable"/> of this type represents a single <see cref="MyCubeGrid"/>.
        /// </summary>
        GRID,
        /// <summary>
        /// A <see cref="MyTrackable"/> of this type represents a construct (<see cref="MyCubeGrid"/>s interconnected with subgrids).
        /// </summary>
        CONSTRUCT,
        /// <summary>
        /// A <see cref="MyTrackable"/> of this type represents a connector structure (constructs of <see cref="MyCubeGrid"/>s interconnected with connectors).
        /// </summary>
        CONNECTOR_STRUCTURE
    }
}
