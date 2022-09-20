using MelonLoader;

namespace CCK.Debugger.Utils;

public class LooseList<T> : List<T> {

    public T CurrentObject { get; private set; }
    public int CurrentObjectIndex { get; private set; }

    private bool _hasChanged;
    public bool HasChanged {
        get => _hasChanged;
        set {
            // Ignore if it didn't change
            //if (_hasChanged == value) return;
            _hasChanged = value;
            Events.DebuggerMenu.OnSwitchInspectedEntity(!_hasChanged);
        }
    }

    private readonly IEnumerable<T> _source;
    private readonly Func<T, bool> _sourceEntryIsValid;
    private readonly bool _initializeWithDefault;
    private int _pageIncrement;

    public bool ListenPageChangeEvents;

    public LooseList(IEnumerable<T> source, Func<T, bool> sourceEntryIsValid, bool initializeWithDefault = false) {

        _source = source;
        _sourceEntryIsValid = sourceEntryIsValid;
        _initializeWithDefault = initializeWithDefault;
        HasChanged = true;

        // Add handlers for increment changes
        Events.DebuggerMenu.ControlsNextPage += () => {
            if (ListenPageChangeEvents) _pageIncrement = 1;
        };
        Events.DebuggerMenu.ControlsPreviousPage += () => {
            if (ListenPageChangeEvents) _pageIncrement = -1;
        };
    }

    public void UpdateViaSource() {
        try {

            Clear();

            // If the list always has a special initial value
            if (_initializeWithDefault) Add(default);

            foreach (var sourceEntry in _source) {
                if (_sourceEntryIsValid.Invoke(sourceEntry)) Add(sourceEntry);
            }

            // If there are no elements, end it here
            if (Count == 0) {
                CurrentObject = default;
                CurrentObjectIndex = 0;
                return;
            }

            var currIndex = IndexOf(CurrentObject);

            // Reset the list to index 0 if the object is not in the list
            if (currIndex == -1) {
                currIndex = 0;
            }

            // Calculate the new index including the page increments/decrements
            CurrentObjectIndex = (currIndex + _pageIncrement + Count) % Count;
            _pageIncrement = 0;

            // Detect current object changes
            if (!EqualityComparer<T>.Default.Equals(this[CurrentObjectIndex], CurrentObject)) HasChanged = true;

            CurrentObject = this[CurrentObjectIndex];
        }
        catch (Exception e) {
            MelonLogger.Error("Something went wrong when updating our loose list.");
            MelonLogger.Error(e);
            throw;
        }

    }
}
