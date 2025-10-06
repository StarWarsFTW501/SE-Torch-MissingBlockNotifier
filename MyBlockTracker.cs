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
    internal class MyBlockTracker
    {
        public readonly List<(string type, string subtype)> Rule = new List<(string type, string subtype)>();
        public MyBlockTracker(string rule)
        {
            Rule = rule.Split('|').Select(b =>
            {
                var spl = b.Split(new char[] { '/' }, 2);
                return (spl[0], spl[1]);
            }).ToList();
        }


        /// <summary>
        /// Tracks the number of matches for this rule in each registered Trackable. Key = trackable, Value = number of matches
        /// </summary>
        Dictionary<MyTrackable, int> _matches = new Dictionary<MyTrackable, int>();

        /// <summary>
        /// Registers a new <see cref="MyTrackable"/> for tracking by this tracker. Does not check trackable's blocks!
        /// </summary>
        /// <param name="trackable"><see cref="MyTrackable"/> to register.</param>
        public void RegisterNewTrackable(MyTrackable trackable)
        {
            _matches[trackable] = 0;
        }

        /// <summary>
        /// Attempts to register a block belonging to a trackable with this tracker, and adjusts tracked matches if appropriate.
        /// </summary>
        /// <param name="parentTrackable">Trackable this block has been added to.</param>
        /// <param name="block">Block that has been added.</param>
        public void RegisterNewBlock(MyTrackable parentTrackable, MySlimBlock block)
        {
            if (!_matches.ContainsKey(parentTrackable))
                throw new Exception("Attempted to register block belonging to an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // if block matches rule

            // increment matches for trackable

            // if no trackable found, throw exception (should have been initialized)
        }

        /// <summary>
        /// Unregisters an existing <see cref="MyTrackable"/> and all its blocks from this tracker. Exceptions will be thrown if the trackable is accessed after calling!
        /// </summary>
        /// <param name="trackable">The trackable to unregister.</param>
        public void UnregisterTrackable(MyTrackable trackable)
        {
            if (!_matches.ContainsKey(trackable))
                throw new Exception("Attempted to unregister a trackable object that was not tracked! Indicative of error in plugin! Please disable the plugin and inform author!");

            _matches.Remove(trackable);
        }

        /// <summary>
        /// Merges the tracked blocks of one trackable into another and unregisters the old one.
        /// </summary>
        /// <param name="pullingFrom"><see cref="MyTrackable"/> that is being incorporated into another one.</param>
        /// <param name="mergingInto"><see cref="MyTrackable"/> that is accepting tracked blocks from the first one.</param>
        public void MergeTrackables(MyTrackable pullingFrom, MyTrackable mergingInto)
        {
            if (!_matches.ContainsKey(pullingFrom))
                throw new Exception("Attempted to merge from an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");
            if (!_matches.ContainsKey(mergingInto))
                throw new Exception("Attempted to merge into an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // enumerate all tracked numbers for pullingFrom

            // unregister pullingFrom
            UnregisterTrackable(pullingFrom);
        }

        /// <summary>
        /// Moves the appropriate number of tracked blocks to one trackable from another. Used when a new trackable has been created by splitting.
        /// </summary>
        /// <param name="splittingFrom">The original <see cref="MyTrackable"/> that has been split.</param>
        /// <param name="target">The new <see cref="MyTrackable"/> that blocks are being moved to.</param>
        public void SplitTrackable(MyTrackable splittingFrom, MyTrackable target)
        {
            if (!_matches.ContainsKey(splittingFrom))
                throw new Exception("Attempted to split an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");
            if (!_matches.ContainsKey(target))
                throw new Exception("Attempted to move blocks into an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // enumerate all blocks in target trackable and equalize tracked numbers
        }

        /// <summary>
        /// Removes the appropriate number of tracked blocks as contained in the grid from the given trackable.
        /// </summary>
        /// <param name="removingFrom"><see cref="MyTrackable"/> to remove the grid's blocks from.</param>
        /// <param name="grid">The grid whose blocks are being removed.</param>
        public void RemoveGridFromTrackable(MyTrackable removingFrom, MyCubeGrid grid)
        {
            if (!_matches.ContainsKey(removingFrom))
                throw new Exception("Attempted to remove a grid from an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // enumerate all blocks in grid and remove them from removingFrom
        }

        /// <summary>
        /// Attempts to unregister a block belonging to a trackable with this tracker, and adjusts tracked matches if appropriate.
        /// </summary>
        /// <param name="parentTrackable">Trackable this block has been removed from.</param>
        /// <param name="block">Block that has been removed.</param>
        public void UnregisterBlock(MyTrackable parentTrackable, MySlimBlock block)
        {
            if (!_matches.ContainsKey(parentTrackable))
                throw new Exception("Attempted to unregister block belonging to an untracked trackable object! Indicative of error in plugin! Please disable the plugin and inform author!");

            // if block matches rule

            // decrement matches for trackable

            // if no trackable found, throw exception (should have been initialized)
        }
    }
}
