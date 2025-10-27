using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SteamKit2.Unified.Internal.CContentBuilder_CommitAppBuild_Request;

namespace TorchPlugin
{
    internal class MyGridAccessSynchronizer
    {
        static object _lock = new object();
        static List<MyGridAccessLock> _activeLocks = new List<MyGridAccessLock>();

        /// <summary>
        /// Acquires a lock for the specified grids. Blocks until the lock can be acquired.
        /// </summary>
        /// <param name="grids">The grids for which to acquire an exclusive lock.</param>
        /// <returns>A <see cref="MyGridAccessLock"/> representing a thread's claim on the involved grids.</returns>
        public static MyGridAccessLock AcquireLocks(IEnumerable<MyCubeGrid> grids)
        {
            lock (_lock)
            {
                var newLock = new MyGridAccessLock(grids);

                // only allow creation of locks that do not conflict with existing locks, unless the existing lock is held by the current thread
                while (_activeLocks.Any(activeLock => activeLock.AffectedGrids.Intersect(newLock.AffectedGrids).Any() && !activeLock.IsHeldByCurrentThread()))
                    Monitor.Wait(_lock, 8);

                _activeLocks.Add(newLock);

                return newLock;
            }
        }

        /// <summary>
        /// Acquires a lock for the specified grids. Blocks until the lock can be acquired.
        /// </summary>
        /// <param name="grids">The grids for which to acquire an exclusive lock.</param>
        /// <returns>A <see cref="MyGridAccessLock"/> representing a thread's claim on the involved grids.</returns>
        public static MyGridAccessLock AcquireLocks(params MyCubeGrid[] grids) => AcquireLocks((IEnumerable<MyCubeGrid>)grids);

        /// <summary>
        /// Releases the specified lock.
        /// </summary>
        /// <remarks>
        /// Although this method disposes of <paramref name="gridLock"/>, it is recommended to be used in reverse - e.g. called through the disposal of said <paramref name="gridLock"/>!</remarks>
        /// <param name="gridLock">A <see cref="MyGridAccessLock"/> to be released.</param>
        public static void ReleaseLocks(MyGridAccessLock gridLock)
        {
            lock (_lock)
            {
                _activeLocks.Remove(gridLock);
                gridLock.Dispose();
            }
        }

        static Timer _debugTimer = null;
        public static void InitDebugging()
        {
            _debugTimer?.Dispose();
            _debugTimer = new Timer(_ =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{nameof(MyGridAccessSynchronizer)}] Active Locks: {_activeLocks.Count}");
                for (int i = 0; i < _activeLocks.Count; i++)
                {
                    var l = _activeLocks[i];
                    sb.AppendLine($"  Lock {i + 1}: Grids: {string.Join(", ", l.AffectedGrids)}");
                }
                Plugin.Instance.Logger.Info(sb.ToString().Trim());
            }, null, 0, 10000);
        }
    }
}
