namespace Spark.Engine.Editor;

/// <summary>编辑器中的一个可逆操作。世界和 UI 都通过命令修改状态。</summary>
public interface IEditorCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>带事务边界的撤销/重做历史。</summary>
public sealed class EditorCommandHistory
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public IReadOnlyCollection<IEditorCommand> UndoStack => _undo;
    public IReadOnlyCollection<IEditorCommand> RedoStack => _redo;
    public bool CanUndo => _undo.Count != 0;
    public bool CanRedo => _redo.Count != 0;

    /// <summary>清空历史；场景从磁盘重载后，旧命令已不再适用于当前对象图。</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        return true;
    }
}

public sealed class DelegateEditorCommand(string description, Action execute, Action undo) : IEditorCommand
{
    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));
    public void Execute() => execute();
    public void Undo() => undo();
}
