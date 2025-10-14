using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;

namespace TorchPlugin
{
    public class MyBlockTracker
    {
        /// <summary>
        /// Represents a job for scanning a large number of blocks simultaneously for any number of <see cref="MyBlockTracker"/>s.
        /// </summary>
        public class ScanJob
        {
            int _matches = 0;
            public Action<int> Callback;
            public Func<MySlimBlock, bool> ScanBlockPredicate;

            /// <summary>
            /// Creates a new <see cref="ScanJob"/> with a callback to be executed when done.
            /// </summary>
            /// <param name="scanBlock">Predicate returning whether or not a passed block matches criteria given by <see cref="MyBlockTracker"/>. Needs to be thread safe!</param>
            /// <param name="callback">Callback executed when the scan is done, passing number of total matches encountered.</param>
            public ScanJob(Func<MySlimBlock, bool> scanBlock, Action<int> callback)
            {
                ScanBlockPredicate = scanBlock;
                Callback = callback;
            }
            /// <summary>
            /// Applies the scan to a single block, registering a match if appropriate.
            /// </summary>
            /// <param name="block"></param>
            public void ScanBlock(MySlimBlock block)
            {
                if (ScanBlockPredicate(block))
                    _matches++;
            }
            /// <summary>
            /// Finalizes the scan and executes the callback with the total number of matches encountered.
            /// </summary>
            public void Complete()
            {
                Callback?.Invoke(_matches);
            }
        }


        public readonly MyTrackingRule Rule;
        public MyBlockTracker(MyTrackingRule rule)
        {
            Rule = rule;
        }

        /// <summary>
        /// Retrieves the currently accumulated number of matches for a given <see cref="MyTrackable"/> by this <see cref="MyBlockTracker"/>.
        /// </summary>
        /// <param name="trackable">The <see cref="MyTrackable"/> to retrieve matches for.</param>
        /// <returns>The accumulated number of matches.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the requested <see cref="MyTrackable"/> is not registered with this tracker.</exception>
        public int GetMatchesForTrackable(MyTrackable trackable)
        {
            if (_matches.TryGetValue(trackable, out var matches))
                return matches;
            throw new InvalidOperationException("Attempted to retrieve matches for untracked trackable!");
        }


        /// <summary>
        /// Tracks the number of matches for this rule in each registered Trackable. Key = trackable, Value = number of matches
        /// </summary>
        public Dictionary<MyTrackable, int> _matches = new Dictionary<MyTrackable, int>();

        /// <summary>
        /// Registers a new <see cref="MyTrackable"/> for tracking by this tracker. Does not check trackable's blocks!
        /// </summary>
        /// <param name="trackable"><see cref="MyTrackable"/> to register.</param>
        public void RegisterNewTrackable(MyTrackable trackable)
        {
            trackable = trackable?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during registration!");
            if (_matches.ContainsKey(trackable))
                throw new InvalidOperationException("Attempted double registration of trackable in tracker!");
            _matches[trackable] = 0;
        }

        /// <summary>
        /// Registers a new <see cref="MyTrackable"/> for tracking by this tracker. Does not check trackable's blocks! If already registered, the generated error is ignored.
        /// </summary>
        /// <param name="trackable"><see cref="MyTrackable"/> to register.</param>
        /// <returns><see langword="true"/> if successfully registered, <see langword="false"/> otherwise.</returns>
        public bool TryRegisterNewTrackable(MyTrackable trackable)
        {
            try
            {
                RegisterNewTrackable(trackable);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a <see cref="ScanJob"/> which registers encountered matches to a given <see cref="MyTrackable"/> according to this tracker's Rule.
        /// </summary>
        /// <param name="trackable"><see cref="MyTrackable"/> to scan.</param>
        /// <returns><see cref="ScanJob"/> for registration of <paramref name="trackable"/>'s blocks.</returns>
        public ScanJob ScanTrackable(MyTrackable trackable)
        {
            var target = trackable?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during scan!");

            return new ScanJob(Rule.BlockMatchesRule, m => _matches[target] += m);
        }

        /// <summary>
        /// Attempts to register a block belonging to a trackable with this tracker, and adjusts tracked matches if appropriate.
        /// </summary>
        /// <param name="parentTrackable">Trackable this block has been added to.</param>
        /// <param name="block">Block that has been added.</param>
        public void RegisterNewBlock(MyTrackable parentTrackable, MySlimBlock block)
        {
            var target = parentTrackable?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during block registration!");

            if (!_matches.ContainsKey(target))
                throw new Exception("Attempted to register block belonging to an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // if block matches rule
            if (Rule.BlockMatchesRule(block))
            {
                // increment matches for trackable
                _matches[target]++;
            }
        }

        /// <summary>
        /// Unregisters any existing <see cref="MyTrackable"/>s in given proxy's ancestry of appropriate type along with all their blocks.
        /// </summary>
        /// <param name="trackable">The trackable to unregister relative to.</param>
        /// <returns><see cref="ScanJob"/> for unregistration of <paramref name="trackable"/>'s blocks, or null if none is required.</returns>
        public ScanJob UnregisterTrackable(MyTrackable trackable)
        {
            if (trackable == null)
                throw new ArgumentNullException(nameof(trackable), "Cannot unregister null trackable!");
            if (trackable.TrackableType == Rule.Type)
            {
                _matches.Remove(trackable);
            }
            else
            {
                var target = trackable.GetAncestorOfType(Rule.Type);
                if (target != null)
                {
                    return new ScanJob(Rule.BlockMatchesRule, m => _matches[target] -= m);
                }
                else
                {
                    foreach (var proxy in trackable.GetAllProxiesOfType(Rule.Type))
                    {
                        _matches.Remove(proxy);
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// Resets the specified trackable to its initial state.
        /// </summary>
        /// <remarks>This method reinitializes the state of the provided trackable object, allowing it to
        /// be reused or reprocessed. Ensure that the trackable is in a valid state for resetting before calling this
        /// method.</remarks>
        /// <param name="trackable">The trackable object to reset. Cannot be null.</param>
        public void ResetTrackable(MyTrackable trackable)
        {
            foreach (var target in trackable.GetAllProxiesOfType(Rule.Type))
            {
                _matches[target] = 0;
            }
        }

        /// <summary>
        /// Merges the tracked blocks of one trackable into another and unregisters the old one.
        /// </summary>
        /// <param name="pullingFrom"><see cref="MyTrackable"/> that is being incorporated into another one.</param>
        /// <param name="mergingInto"><see cref="MyTrackable"/> that is accepting tracked blocks from the first one.</param>
        /// <returns><see cref="ScanJob"/> for merging of <paramref name="pullingFrom"/>'s blocks, or null if none is required.</returns>
        public ScanJob MergeTrackables(MyTrackable pullingFrom, MyTrackable mergingInto)
        {
            var targetInto = mergingInto?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during merge as target!");

            int matchesFrom = 0;
            var targetFrom = pullingFrom.GetAncestorOfType(Rule.Type);
            if (targetFrom == null)
            {
                // all blocks on a proxy at or above Rule type are being moved = we move all matches for all its descendants (or itself) on the correct level
                foreach (var descendant in pullingFrom.GetAllProxiesOfType(Rule.Type))
                {
                    // remember how many to move over
                    matchesFrom += _matches[descendant];

                    // forget original
                    _matches.Remove(descendant);
                }

                _matches[targetInto] += matchesFrom;

                // no need for simultaneous scan job, we used already cached match counts
                return null;
            }

            if (targetFrom == pullingFrom)
            {
                _matches[targetInto] += _matches[targetFrom];

                _matches.Remove(targetFrom);

                return null;
            }

            // we are moving blocks below Rule type = we need to only move the number of blocks this trackable targets
                
            return new ScanJob(Rule.BlockMatchesRule, m =>
            {
                _matches[targetInto] += m;

                _matches[targetFrom] -= m;
            });
        }

        /// <summary>
        /// Moves the appropriate number of tracked blocks to one trackable from another. Used when a new trackable has been created by splitting.
        /// </summary>
        /// <param name="splittingFrom">The original <see cref="MyTrackable"/> that has been split.</param>
        /// <param name="target">The new <see cref="MyTrackable"/> that blocks are being moved to.</param>
        /// <returns><see cref="ScanJob"/> for splitting of <paramref name="target"/>'s blocks, or null if none is required.</returns>
        public ScanJob SplitTrackable(MyTrackable splittingFrom, MyTrackable target)
        {
            var targetFrom = splittingFrom?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during split!");
            var targetInto = target?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during split!");

            if (targetFrom == targetInto)
            {
                // common ancestor on lower or equal level to this tracker = tracking stays the same
                return null;
            }

            if (!_matches.ContainsKey(targetFrom))
                throw new InvalidOperationException("Attempted to split an untracked trackable object!");
            if (!_matches.ContainsKey(targetInto))
                throw new InvalidOperationException("Attempted to move blocks into an untracked trackable object!");

            if (target == targetInto)
            {
                // in case we are splitting 
            }

            return new ScanJob(Rule.BlockMatchesRule, m =>
            {
                _matches[targetFrom] -= m;
                _matches[targetInto] += m;
            });
        }

        /// <summary>
        /// Attempts to unregister a block belonging to a trackable with this tracker, and adjusts tracked matches if appropriate.
        /// </summary>
        /// <param name="parentTrackable">Trackable this block has been removed from.</param>
        /// <param name="block">Block that has been removed.</param>
        public void UnregisterBlock(MyTrackable parentTrackable, MySlimBlock block)
        {
            var target = parentTrackable?.GetAncestorOfType(Rule.Type)
                ?? throw new NullReferenceException($"No ancestor of type '{Rule.Type}' found for trackable during block unregister!");

            if (!_matches.ContainsKey(target))
                throw new Exception("Attempted to unregister block belonging to an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // if block matches rule
            if (Rule.BlockMatchesRule(block))
            {
                // decrement matches for trackable
                _matches[target]--;
            }
        }

        /// <summary>
        /// Unregisters all trackables and resets tracked matches.
        /// </summary>
        public void UnregisterAllTrackables()
        {
            _matches.Clear();
        }
    }
}
