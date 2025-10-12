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
        public IEnumerable<MyTrackable> Children;

        protected int _containedCount;
        public int ContainedCount => _containedCount;

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
            return Parent == null || Parent.Children.Count() != 0 ? this : Parent.GetHighestSingleAncestor();
        }

        /// <summary>
        /// Removes a <paramref name="child"/> from this <see cref="MyTrackable"/>.
        /// </summary>
        /// <param name="child"><see cref="MyTrackable"/> child to remove.</param>
        public void RemoveChild(MyTrackable child)
            => Children = Children.Where(c => c != child).ToList();
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
    }
    /// <summary>
    /// Represents a trackable body comprised of grids interconnected by subgrid connections.
    /// </summary>
    public class MyTrackable_Construct : MyTrackable
    {
        public MyTrackable_Construct(IEnumerable<MyTrackable> grids)
        {
            Children = grids;
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
            Children = constructs;
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
