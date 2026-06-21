namespace LogViewer.Models;

public class RingBuffer<T>
{
    private T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    public void Add(T item)
    {
        if (_count == _buffer.Length)
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

    public T Get(int index)
    {
        if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
        return _buffer[(_tail + index) % _buffer.Length];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

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