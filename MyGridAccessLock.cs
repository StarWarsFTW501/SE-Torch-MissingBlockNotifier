using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Torch;

namespace TorchPlugin
{
    public class MyGridAccessLock : IDisposable
    {
        public bool IsActive { get; private set; }
        public IReadOnlyCollection<long> AffectedGrids { get; private set; }
        object _lockObject;

        public MyGridAccessLock(IEnumerable<MyCubeGrid> grids)
        {
            AffectedGrids = grids.Select(g => g.EntityId).ToHashSet().AsReadOnly();
            IsActive = true;
            _lockObject = new object();
            Monitor.Enter(_lockObject);
        }

        public void Dispose()
        {
            if (IsActive)
            {
                if (!IsHeldByCurrentThread())
                    throw new InvalidOperationException($"Cannot dispose a {nameof(MyGridAccessLock)} that is not owned by the current thread!");
                IsActive = false;
                Monitor.Exit(_lockObject);
                MyGridAccessSynchronizer.ReleaseLocks(this);
            }
        }
        public bool IsHeldByCurrentThread() => Monitor.IsEntered(_lockObject);
    }
}
