using System.Collections;

namespace Spark.Engine.Render;

/// <summary>
/// 帧内复用、按线程独占访问的可变长缓冲（池化数组，只归零计数不释放底层数组），
/// 避免逻辑/渲染线程每帧分配。快照缓冲只被其所属线程在单帧内触碰，复用是安全的。
/// </summary>
public sealed class FrameBuffer<T> : IReadOnlyList<T>
{
    private T[] _items;

    public int Count { get; private set; }

    public T this[int index] => _items[index];

    /// <summary>当前有效元素的连续视图。</summary>
    public ReadOnlySpan<T> Span => _items.AsSpan(0, Count);

    public FrameBuffer(int initialCapacity = 16)
    {
        _items = new T[initialCapacity];
    }

    public void Add(in T item)
    {
        if (Count == _items.Length)
            Array.Resize(ref _items, System.Math.Max(1, _items.Length * 2));
        _items[Count++] = item;
    }

    /// <summary>仅归零计数，复用底层数组。</summary>
    public void Clear() => Count = 0;

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
