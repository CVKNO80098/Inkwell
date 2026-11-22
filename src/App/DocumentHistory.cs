using System;
using System.Collections.Generic;

namespace Inkwell.App;

public struct CaretState
{
    public int Line;
    public int Col;
    public int SelLine;
    public int SelCol;

    public static CaretState At(int line, int col) => new CaretState { Line = line, Col = col, SelLine = line, SelCol = col };
}

public struct HistorySnapshot
{
    public string Text;
    public CaretState Caret;
}

/// <summary>
/// Snapshot based undo stack. Snapshots are committed when typing pauses
/// (or before a discrete command), never on every keystroke, so typing stays cheap.
/// </summary>
public sealed class DocumentHistory
{
    readonly Stack<HistorySnapshot> _undo = new Stack<HistorySnapshot>();
    readonly Stack<HistorySnapshot> _redo = new Stack<HistorySnapshot>();
    readonly int _limit;

    HistorySnapshot _current;
    bool _hasCurrent;

    public DocumentHistory(int limit = 400) { _limit = limit; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Reset(string text, CaretState caret)
    {
        _undo.Clear();
        _redo.Clear();
        _current = new HistorySnapshot { Text = text ?? "", Caret = caret };
        _hasCurrent = true;
    }

    /// <summary>Commit the current text as the new baseline when it differs from the last one.</summary>
    public void Commit(string text, CaretState caret)
    {
        if (text == null) return;
        if (!_hasCurrent) { Reset(text, caret); return; }
        if (string.Equals(text, _current.Text, StringComparison.Ordinal))
        {
            _current.Caret = caret;
            return;
        }
        Push(_undo, _current);
        _current = new HistorySnapshot { Text = text, Caret = caret };
        _redo.Clear();
    }

    /// <summary>Record a discrete replacement (formatting command, paste) as one undo step.</summary>
    public void Replace(string text, CaretState caret)
    {
        if (!_hasCurrent) { Reset(text, caret); return; }
        Push(_undo, _current);
        _current = new HistorySnapshot { Text = text ?? "", Caret = caret };
        _redo.Clear();
    }

    /// <summary>Update the baseline without creating an undo step.</summary>
    public void Adopt(string text, CaretState caret)
    {
        if (!_hasCurrent) { Reset(text, caret); return; }
        _current = new HistorySnapshot { Text = text ?? "", Caret = caret };
    }

    public bool TryUndo(string currentText, CaretState caret, out HistorySnapshot snapshot)
    {
        snapshot = default;
        if (!_hasCurrent) return false;

        // Uncommitted typing since the last commit: revert straight back to it.
        if (!string.Equals(currentText, _current.Text, StringComparison.Ordinal))
        {
            Push(_redo, new HistorySnapshot { Text = currentText, Caret = caret });
            snapshot = _current;
            return true;
        }

        if (_undo.Count == 0) return false;
        Push(_redo, _current);
        snapshot = _undo.Pop();
        _current = snapshot;
        return true;
    }

    public bool TryRedo(string currentText, CaretState caret, out HistorySnapshot snapshot)
    {
        snapshot = default;
        if (!_hasCurrent || _redo.Count == 0) return false;
        Push(_undo, _current);
        snapshot = _redo.Pop();
        _current = snapshot;
        return true;
    }

    void Push(Stack<HistorySnapshot> stack, HistorySnapshot s)
    {
        stack.Push(s);
        if (stack.Count <= _limit) return;
        var arr = stack.ToArray();
        stack.Clear();
        for (int i = arr.Length - 1; i >= 0 && stack.Count < _limit; i--) stack.Push(arr[i]);
    }
}
