﻿using System;

namespace HLE.Collections;

public ref struct Queue<T>
{
    public int Count { get; private set; }

    public readonly int Capacity => _queue.Length;

    private readonly Span<T> _queue = Span<T>.Empty;
    private readonly int _lastIndex;
    private int _enqueueIndex;
    private int _dequeueIndex;

    public Queue()
    {
    }

    public Queue(Span<T> queue)
    {
        _queue = queue;
        _lastIndex = queue.Length - 1;
    }

    public void Enqueue(T item)
    {
        if (_enqueueIndex > _lastIndex)
        {
            if (_dequeueIndex == 0)
            {
                throw new InvalidOperationException("Queue is full.");
            }

            // copies the Span to the front of the queue
            Span<T> elementsToCopy = _queue[_dequeueIndex..];
            elementsToCopy.CopyTo(_queue);
            _dequeueIndex = 0;
            _enqueueIndex = elementsToCopy.Length;
        }

        _queue[_enqueueIndex++] = item;
        Count++;
    }

    public T Dequeue()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        Count--;
        return _queue[_dequeueIndex++];
    }

    public readonly T Peek()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Queue is empty.");
        }

        return _queue[_dequeueIndex];
    }

    public bool TryEnqueue(T item)
    {
        if (Count >= Capacity)
        {
            return false;
        }

        Enqueue(item);
        return true;
    }

    public bool TryDequeue(out T? item)
    {
        if (Count <= 0)
        {
            item = default;
            return false;
        }

        item = Dequeue();
        return true;
    }

    public readonly bool TryPeek(out T? item)
    {
        if (Count <= 0)
        {
            item = default;
            return false;
        }

        item = Peek();
        return true;
    }

    public void Clear()
    {
        Count = 0;
        _enqueueIndex = 0;
        _dequeueIndex = 0;
    }

    public static implicit operator Queue<T>(Span<T> queue) => new(queue);
}
