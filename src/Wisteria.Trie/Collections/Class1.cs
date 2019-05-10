// Copyright © FUJIWARA, Yusuke 
// This file is licensed to you under the Apache 2 license.
// See the LICENSE file in the project root for more information.

// #nullable enabled

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Wisteria.Collections
{
	public partial class Trie<T>
	{
		public int Count { get; private set; }

		public T this[ReadOnlySpan<byte> key]
		{
			get
			{
				if (!this.TryGetValue(key, out T value))
				{
					throw new KeyNotFoundException("T.B.D.");
				}

				return value;
			}
			set => this.AddCore(key, value, allowOverwrite: true);
		}

		public Trie() {}

// TODO: nullability
		public bool TryAdd(ReadOnlySpan<byte> key, T value)
			=> this.AddCore(key, value, allowOverwrite: false);

// TODO: nullability
		public bool TryRemove(ReadOnlySpan<byte> key, out T value)
			=> this.Find(key, removing: true, out value, out int _, out Node _);

// TODO: nullability
		public bool TryGetValue(ReadOnlySpan<byte> key, out T value)
			=> this.Find(key, removing: false, out value, out int _, out Node _);
	}

	// Simple & naive linked-list based tree PAT implementation
	partial class Trie<T>
	{
		private Node _root;

		public void Clear()
			=> this._root = null;

		// TODO: refactoring.

		private bool AddCore(ReadOnlySpan<byte> key, T value, bool allowOverwrite)
		{
			if (this._root == null)
			{
				// first.
				this._root =
					new Node
					{
						Range = key.ToArray(),
						ChildPrefixes = new List<byte>(0),
						Children = new List<Node>(0),
						Value = value,
						HasValue = true
					};
				this.Count++;
				return true;
			}

			var currentKey = key;
			var currentNode = this._root;
			while (true)
			{
				ReadOnlySpan<byte> currentNodeRange = currentNode.Range;
				int currentPrefixLength = Math.Min(currentKey.Length, currentNodeRange.Length);
				var rangeOffset = 0;
				while (rangeOffset < currentPrefixLength)
				{
					if (currentKey[0] == currentNodeRange[rangeOffset])
					{
						currentKey = currentKey.Slice(1);
						rangeOffset++;
						continue;
					}

					break;
				}

				if (rangeOffset == currentNodeRange.Length)
				{
					if (currentKey.Length == 0)
					{
						// it's me
						if (!currentNode.HasValue || allowOverwrite)
						{
							currentNode.Value = value;
							currentNode.HasValue = true;
							this.Count++;
							return true;
						}
						else
						{
							return false;
						}
					}
					else
					{
						// try children
						int foundIndex = currentNode.ChildPrefixes.BinarySearch(currentKey[0]);
						if (foundIndex >= 0)
						{
							currentNode = currentNode.Children[foundIndex];
							continue;
						}
						
						// insert new and set value to it.
						var newNode =
							new Node
							{
								Range = currentKey.ToArray(),
								ChildPrefixes = new List<byte>(0),
								Children = new List<Node>(0),
								Value = value,
								HasValue = true
							};

						var targetIndex = ~foundIndex;
						currentNode.ChildPrefixes.Insert(targetIndex, currentKey[0]);
						currentNode.Children.Insert(targetIndex, newNode);
						this.Count++;
						return true;
					}
				}
				else
				{
					// devide me because new parent is needed.
					var rangeOfNewParentForCurrentChildren = currentNodeRange.Slice(rangeOffset).ToArray();
					var newParentForCurrentChildren =
						new Node
						{
							Range = rangeOfNewParentForCurrentChildren,
							ChildPrefixes = currentNode.ChildPrefixes,
							Children = currentNode.Children,
							Value = currentNode.Value,
							HasValue = currentNode.HasValue
						};
					currentNode.Range = currentNodeRange.Slice(0, rangeOffset).ToArray();

					if (currentKey.Length == 0)
					{
						// OK, new parant is target
						currentNode.Value = value;
						currentNode.HasValue = true;

						currentNode.ChildPrefixes = new List<byte>(1) { rangeOfNewParentForCurrentChildren[0] };
						currentNode.Children = new List<Node>(1) { newParentForCurrentChildren };
					}
					else
					{
						// OK, new sibling is needed.
						var newNode =
							new Node
							{
								Range = currentKey.ToArray(),
								ChildPrefixes = new List<byte>(0),
								Children = new List<Node>(0),
								Value = value,
								HasValue = true
							};

						currentNode.Value = default;
						currentNode.HasValue = false;

						Debug.Assert(rangeOfNewParentForCurrentChildren[0] != currentKey[0], "rangeOfNewParentForCurrentChildren[0] != currentKey[0]");
						if (rangeOfNewParentForCurrentChildren[0] > currentKey[0])
						{
							currentNode.ChildPrefixes =
								new List<byte>(2)
								{
									currentKey[0],
									rangeOfNewParentForCurrentChildren[0]
								};
							currentNode.Children =
								new List<Node>(2)
								{
									newNode,
									newParentForCurrentChildren
								};
						}
						else
						{

							currentNode.ChildPrefixes =
								new List<byte>(2)
								{
									rangeOfNewParentForCurrentChildren[0],
									currentKey[0]
								};
							currentNode.Children =
								new List<Node>(2)
								{
									newParentForCurrentChildren,
									newNode
								};
						}
					}

					this.Count++;
					return true;
				} // while (true)			
			}
		}		

		private bool Find(
			ReadOnlySpan<byte> key,
			bool removing,
			out T value,
			out int prefixLength,
			out Node prefixNode
		)
		{
			if (this._root == null)
			{
				// empty.
				value = default;
				prefixLength = 0;
				prefixNode = null;
				return false;
			}

			var currentKey = key;
			var currentNode = this._root;
			while (true)
			{
				ReadOnlySpan<byte> currentNodeRange = currentNode.Range;
				int currentPrefixLength = Math.Min(currentKey.Length, currentNodeRange.Length);
				var rangeOffset = 0;
				while (rangeOffset < currentPrefixLength)
				{
					if (currentKey[0] == currentNodeRange[rangeOffset])
					{
						currentKey = currentKey.Slice(1);
						rangeOffset++;
						continue;
					}

					break;
				}

				prefixLength = rangeOffset;
				prefixNode = currentNode;

				if (rangeOffset == currentNodeRange.Length)
				{
					if (currentKey.Length == 0)
					{
						// it's me
						var hasValue = currentNode.HasValue;
						if (hasValue)
						{
							value = currentNode.Value;

							if (removing)
							{
								currentNode.Value = default;
								currentNode.HasValue = false;
								this.Count--;
							}
						}
						else
						{
							value = default;
						}

						return hasValue;
					}
					else
					{
						// try children
						int foundIndex = Array.BinarySearch(currentNode.Range, currentKey[0]);
						if (foundIndex >= 0)
						{
							currentNode = currentNode.Children[foundIndex];
							continue;
						}

						// not found.
						value = default;
						return false;
					}
				}
				else
				{
					// not found.
					value = default;
					return false;
				}				
			} // while (true)
		}

		private sealed class Node
		{
			public byte[] Range;
			public List<byte> ChildPrefixes;
			public List<Node> Children;
			public T Value;
			public bool HasValue;

			public Node() {}
		}
	}

	partial class Trie<T>
	{
		// depth-first search.
		public Enumerator GetEnumerator() => new Enumerator(this);

		public struct Enumerator : IEnumerator<KeyValuePair<IEnumerable<byte>, T>>
		{
			private const int IndexInRoot = -1;

			private readonly Trie<T> _trie;
			private readonly Stack<(Node Node, int IndexInParent)> _context;
			private Node _currentNode;

			public void Dispose() {}

			public KeyValuePair<IEnumerable<byte>, T> Current =>
				new KeyValuePair<IEnumerable<byte>, T>(
					this._context.SelectMany(x => x.Node.Range).Concat(this._currentNode.Range),
					this._currentNode.Value
				);

			object IEnumerator.Current => this.Current;

			internal Enumerator(Trie<T> trie)
			{
				this._context = new Stack<(Node, int)>();
				this._trie = trie;
				this._currentNode = null;
			}

#warning TODO: refactor
			private bool FindNode()
			{
				if (this._currentNode == null)
				{
					this._currentNode = this._trie._root;	
					return true;
				}

				if (this._currentNode.Children.Count > 0)
				{
					// go to first child
					var firstChild = this._currentNode.Children[0];
					this._context.Push((this._currentNode, 0));
					this._currentNode = firstChild;		
					return true;
				}

				// back to root
				while (this._context.Count > 0)
				{
					// back to parent
					(Node parent, int indexInParent) = this._context.Pop();

					// go to next sibling if exists
					indexInParent++;
					if (parent.Children.Count > indexInParent)
					{
						this._context.Push((parent, indexInParent));
						this._currentNode = parent.Children[indexInParent];
						return true;
					}
					else
					{
						// all siblings have been traversed, so go to grand parent.
						continue; // inner loop
					}
				} // inner loop

				this._currentNode = null;
				return false;
			}

			public bool MoveNext()
			{
				while (this.FindNode())
				{
					if (this._currentNode.HasValue)
					{
						return true;
					}
				}

				return false;
			}

			void IEnumerator.Reset() => this.InternalReset();

			internal void InternalReset()
			{
				this._currentNode = this._trie._root;
				this._context.Clear();
			}
		}
	}	

	partial class Trie<T> : IDictionary<IEnumerable<byte>, T>, IReadOnlyDictionary<IEnumerable<byte>, T>
	{
		T IReadOnlyDictionary<IEnumerable<byte>, T>.this[IEnumerable<byte> key] => this[key.ToArray()];

		T IDictionary<IEnumerable<byte>, T>.this[IEnumerable<byte> key]
		{
			get => this[key.ToArray()];
			set => this[key.ToArray()] = value;
		}

		private IEnumerable<IEnumerable<byte>> GetKeys() => this.Select(x => x.Key);

		IEnumerable<IEnumerable<byte>> IReadOnlyDictionary<IEnumerable<byte>, T>.Keys => this.GetKeys();

		ICollection<IEnumerable<byte>> IDictionary<IEnumerable<byte>, T>.Keys => this.GetKeys().ToArray();

		IEnumerable<T> GetValues() => this.Select(x => x.Value);

		IEnumerable<T> IReadOnlyDictionary<IEnumerable<byte>, T>.Values => this.GetValues();

		ICollection<T> IDictionary<IEnumerable<byte>, T>.Values => this.GetValues().ToArray();

		bool ICollection<KeyValuePair<IEnumerable<byte>, T>>.IsReadOnly => false;

		IEnumerator<KeyValuePair<IEnumerable<byte>, T>> IEnumerable<KeyValuePair<IEnumerable<byte>, T>>.GetEnumerator() => this.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		private void Add(IEnumerable<byte> key, T value)
		{
			if (!this.TryAdd(key.ToArray(), value))
			{
				throw new ArgumentException("T.B.D.", nameof(key));
			}
		}

		void IDictionary<IEnumerable<byte>, T>.Add(IEnumerable<byte> key, T value)
			=> this.Add(key, value);

		void ICollection<KeyValuePair<IEnumerable<byte>, T>>.Add(KeyValuePair<IEnumerable<byte>, T> item)
			=> this.Add(item.Key, item.Value);

		bool IDictionary<IEnumerable<byte>, T>.Remove(IEnumerable<byte> key)
			=> this.TryRemove(key.ToArray(), out T _);

		bool ICollection<KeyValuePair<IEnumerable<byte>, T>>.Remove(KeyValuePair<IEnumerable<byte>, T> item)
		{
			if (!this.TryGetValue(item.Key.ToArray(), out T foundValue) || !EqualityComparer<T>.Default.Equals(foundValue, item.Value))
			{
				return false;
			}

			return this.TryRemove(item.Key.ToArray(), out T _);
		}

		bool IReadOnlyDictionary<IEnumerable<byte>, T>.TryGetValue(IEnumerable<byte> key, out T value)
			=> this.TryGetValue(key.ToArray(), out value);

		bool IDictionary<IEnumerable<byte>, T>.TryGetValue(IEnumerable<byte> key, out T value)
			=> this.TryGetValue(key.ToArray(), out value);

		bool IReadOnlyDictionary<IEnumerable<byte>, T>.ContainsKey(IEnumerable<byte> key)
			=> this.TryGetValue(key.ToArray(), out T _);

		bool IDictionary<IEnumerable<byte>, T>.ContainsKey(IEnumerable<byte> key)
			=> this.TryGetValue(key.ToArray(), out T _);
		
		bool ICollection<KeyValuePair<IEnumerable<byte>, T>>.Contains(KeyValuePair<IEnumerable<byte>, T> item)
			=> this.TryGetValue(item.Key.ToArray(), out T foundValue) && EqualityComparer<T>.Default.Equals(foundValue, item.Value);

		void ICollection<KeyValuePair<IEnumerable<byte>, T>>.CopyTo(KeyValuePair<IEnumerable<byte>, T>[] array, int arrayIndex)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (arrayIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(arrayIndex), "T.B.D.");
			}

			if (arrayIndex + this.Count >= array.Length)
			{
				throw new ArgumentException("T.B.D.", nameof(arrayIndex));
			}

			var i = arrayIndex;
			foreach (var kv in this)
			{
				array[i] = kv;
				i++;
			}
		}
	}

	partial class Trie<T> : IDictionary
	{
		object IDictionary.this[object key]
		{
			get
			{
				if (!(key is IEnumerable<byte> typedKey))
				{
					throw new ArgumentException("T.B.D.", nameof(key));
				}

				return this[typedKey.ToArray()];
			}
			set
			{
				if (!(key is IEnumerable<byte> typedKey))
				{
					throw new ArgumentException("T.B.D.", nameof(key));
				}

				if (!(value is T typedValue))
				{
					throw new ArgumentException("T.B.D.", nameof(value));
				}

				this[typedKey.ToArray()] = typedValue;
			}
		}

		bool IDictionary.IsFixedSize => false;

		bool IDictionary.IsReadOnly => false;

		ICollection IDictionary.Keys => this.GetKeys().ToArray();

		ICollection IDictionary.Values => this.GetValues().ToArray();

		bool ICollection.IsSynchronized => false;

		object ICollection.SyncRoot => this;

		void IDictionary.Add(object key, object value)
		{
			if (!(key is IEnumerable<byte> typedKey))
			{
				throw new ArgumentException("T.B.D.", nameof(key));
			}

			if (!(value is T typedValue))
			{
				throw new ArgumentException("T.B.D.", nameof(value));
			}

			if (!this.TryAdd(typedKey.ToArray(), typedValue))
			{
				throw new ArgumentException("T.B.D.", nameof(key));
			}
		}

		void IDictionary.Remove(object key)
		{
			if (!(key is IEnumerable<byte> typedKey))
			{
				#warning TODO: really?
				throw new ArgumentException("T.B.D.", nameof(key));
			}

			this.TryRemove(typedKey.ToArray(), out T _);
		}

		bool IDictionary.Contains(object key)
		{
			if (!(key is IEnumerable<byte> typedKey))
			{
				return false;
			}

			return this.TryGetValue(typedKey.ToArray(), out T _);
		}

		void ICollection.CopyTo(Array array, int index)
		{
			if (array == null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (index < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), "T.B.D.");
			}

			if (index + this.Count >= array.Length)
			{
				throw new ArgumentException("T.B.D.", nameof(array));
			}

			var i = index;
			foreach (var kv in this)
			{
				array.SetValue(kv, i);
				i++;
			}
		}

		IDictionaryEnumerator IDictionary.GetEnumerator() => new DictionaryEnumerator(new Enumerator(this));

		private sealed class DictionaryEnumerator : IDictionaryEnumerator
		{
			private Enumerator _enumerator;

			DictionaryEntry IDictionaryEnumerator.Entry
			{
				get
				{
					KeyValuePair<IEnumerable<byte>, T> item = this._enumerator.Current;
					return new DictionaryEntry(item.Key, item.Value);
				}
			}

			object IDictionaryEnumerator.Key => this._enumerator.Current.Key;

			object IDictionaryEnumerator.Value => this._enumerator.Current.Value;

			object IEnumerator.Current => this._enumerator.Current;

			public DictionaryEnumerator(Enumerator enumerator)
				=> this._enumerator = enumerator;
			
			bool IEnumerator.MoveNext() => this._enumerator.MoveNext();

			void IEnumerator.Reset() => this._enumerator.InternalReset();
		}
	}
}
