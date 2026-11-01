using System;
using System.Collections.Generic;

namespace Forge.Editing.History
{
    /// <summary>
    /// Bounded undo/redo stacks of <see cref="EditStep"/>s. Each state of the
    /// session is identified by the id of the step that produced it, which lets
    /// the session tell whether it is back at the last saved state.
    /// </summary>
    public sealed class UndoHistory
    {
        public const int DefaultCapacity = 256;

        readonly List<EditStep> _undo = new List<EditStep>();
        readonly Stack<EditStep> _redo = new Stack<EditStep>();
        readonly int _capacity;
        int _floorStateId;

        public UndoHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
            _capacity = capacity;
        }

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public string UndoDescription => CanUndo ? _undo[_undo.Count - 1].Description : null;

        public string RedoDescription => CanRedo ? _redo.Peek().Description : null;

        public int UndoCount => _undo.Count;

        public int RedoCount => _redo.Count;

        /// <summary>Id of the current state: the last applied step, or the oldest state still reachable.</summary>
        public int CurrentStateId => CanUndo ? _undo[_undo.Count - 1].Id : _floorStateId;

        /// <summary>Records a new step; any redo history is discarded.</summary>
        internal void Record(EditStep step)
        {
            _redo.Clear();
            _undo.Add(step);
            if (_undo.Count > _capacity)
            {
                // The state after the dropped step becomes the oldest reachable state.
                _floorStateId = _undo[0].Id;
                _undo.RemoveAt(0);
            }
        }

        internal EditStep TakeUndo()
        {
            var step = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            return step;
        }

        internal void PushRedo(EditStep step) => _redo.Push(step);

        internal EditStep TakeRedo() => _redo.Pop();

        internal void PushUndo(EditStep step) => _undo.Add(step);
    }
}
