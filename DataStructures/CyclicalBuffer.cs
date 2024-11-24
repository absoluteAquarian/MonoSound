using System;
using System.Collections;
using System.Collections.Generic;

namespace MonoSound.DataStructures {
	/// <summary>
	/// Represents a buffer that can be used to store a fixed number of elements, and will overwrite the oldest elements when full
	/// </summary>
	public class CyclicalBuffer<T> : ICollection<T> {
		private readonly T[] _array;
		private int _head;
		private int _size;
		private int _version;

		/// <summary>
		/// The maximum number of elements that can be stored in the buffer
		/// </summary>
		public int Capacity => _array.Length;

		/// <inheritdoc/>
		public int Count => _size;

		/// <summary>
		/// Gets or sets the element at the specified relative index in the buffer
		/// </summary>
		/// <exception cref="IndexOutOfRangeException"/>
		public T this[int index] {
			get {
				ArgumentOutOfRangeException.ThrowIfNegative(index);
				ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);

				return _array[(_head + index) % _array.Length];
			}
		}

		/// <summary>
		/// Creates a new <see cref="CyclicalBuffer{T}"/> with the specified capacity
		/// </summary>
		public CyclicalBuffer(int capacity) {
			_array = new T[capacity];
			_head = 0;
			_size = 0;
		}

		/// <summary>
		/// Adds an element to the end of the buffer, overwriting the oldest element if the buffer is full
		/// </summary>
		public void Enqueue(T item) {
			if (_size == _array.Length) {
				_head = (_head + 1) % _array.Length;
				_size--;
			}

			_array[(_head + _size) % _array.Length] = item;
			_size++;
			_version++;
		}

		/// <summary>
		/// Removes the oldest element from the buffer and returns it
		/// </summary>
		/// <exception cref="InvalidOperationException"/>
		public T Dequeue() {
			if (_size == 0)
				throw new InvalidOperationException("The buffer is empty.");

			T item = _array[_head];
			_head = (_head + 1) % _array.Length;
			_size--;
			_version++;

			return item;
		}

		/// <summary>
		/// Returns the oldest element in the buffer without removing it
		/// </summary>
		/// <exception cref="InvalidOperationException"/>
		public T Peek() {
			if (_size == 0)
				throw new InvalidOperationException("The buffer is empty.");

			return _array[_head];
		}

		/// <inheritdoc/>
		public void Clear() {
			_head = 0;
			_size = 0;
			_version++;
		}

		#region Implement ICollection<T>
		/// <inheritdoc/>
		public bool IsReadOnly => false;

		void ICollection<T>.Add(T item) => Enqueue(item);

		/// <inheritdoc/>
		public bool Contains(T item) {
			for (int i = 0; i < _size; i++) {
				T current = this[i];
				if (current is null)
					return item is null;
				else if (current.Equals(item))
					return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public void CopyTo(T[] array, int arrayIndex) {
			ArgumentNullException.ThrowIfNull(array);
			ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
			ArgumentOutOfRangeException.ThrowIfGreaterThan(arrayIndex, array.Length - _size);

			for (int i = 0; i < _size; i++)
				array[arrayIndex + i] = this[i];
		}

		/// <inheritdoc/>
		public IEnumerator<T> GetEnumerator() => new Enumerator(this);

		bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);
		#endregion

		private class Enumerator(CyclicalBuffer<T> buffer) : IEnumerator<T> {
			private readonly CyclicalBuffer<T> _buffer = buffer;
			private readonly int _version = buffer._version;
			private int _index = -1;

			private T _current;
			public T Current => _current;

			object IEnumerator.Current => Current;

			public void Dispose() { }

			public bool MoveNext() {
				if (_version != _buffer._version)
					throw new InvalidOperationException("The buffer was modified while enumerating, and the enumeration cannot continue.");

				if (_index + 1 >= _buffer._size) {
					_current = default;
					return false;
				}

				_index++;
				_current = _buffer[_index];
				return true;
			}

			public void Reset() {
				_index = -1;
				_current = default;
			}
		}
	}
}
