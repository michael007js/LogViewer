namespace LogViewer.Models;

/// <summary>
/// 环形缓冲区，实现 O(1) 高性能 FIFO 存储。
/// 用于缓存日志条目，容量固定后自动覆盖最早数据。
/// <b>注意：此类不是线程安全的</b>——所有读写操作必须在 UI 线程进行，
/// 或通过外部同步机制保护。
/// </summary>
public class RingBuffer<T>
{
    /// <summary>内部数组，实际存储数据。</summary>
    private T[] _buffer;

    /// <summary>下一个写入位置的索引。</summary>
    private int _head;

    /// <summary>最早有效数据的索引（队列头）。</summary>
    private int _tail;

    /// <summary>当前有效元素数量。</summary>
    private int _count;

    /// <summary>
    /// 初始化指定容量的环形缓冲区。
    /// </summary>
    /// <param name="capacity">缓冲区最大容量，必须大于 0。</param>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// 向缓冲区尾部添加一个元素。若已满则覆盖最早的元素。
    /// </summary>
    public void Add(T item)
    {
        if (_count == _buffer.Length) // 缓冲区已满，覆盖最老数据
        {
            _buffer[_tail] = default!;
            _tail = (_tail + 1) % _buffer.Length;
        }
        else
        {
            _count++;
        }

        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
    }

    /// <summary>
    /// 按逻辑索引获取元素（0 = 最早添加的数据）。
    /// </summary>
    /// <param name="index">逻辑索引，必须介于 [0, Count-1] 之间。</param>
    public T Get(int index)
    {
        if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
        return _buffer[(_tail + index) % _buffer.Length];
    }

    /// <summary>当前有效元素数量。</summary>
    public int Count => _count;

    /// <summary>缓冲区最大容量。</summary>
    public int Capacity => _buffer.Length;

    /// <summary>清空缓冲区，所有元素被释放。</summary>
    public void Clear()
    {
        if (_count > 0)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// 调整缓冲区容量（仅保留最早 min(当前数量, 新容量) 个元素）。
    /// </summary>
    /// <param name="newCapacity">新的容量，必须大于 0。</param>
    public void Resize(int newCapacity)
    {
        if (newCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(newCapacity));
        var newBuffer = new T[newCapacity];
        var copyCount = Math.Min(_count, newCapacity);
        for (int i = 0; i < copyCount; i++)
        {
            newBuffer[i] = _buffer[(_tail + i) % _buffer.Length];
        }

        _buffer = newBuffer;
        _tail = 0;
        _head = copyCount;
        _count = copyCount;
    }
}