using System.Collections.Generic;

namespace TowerDefense
{
    // A binary min-heap used as the A* open set (priority queue).
    // Items are stored in a flat list that represents a complete binary tree:
    // the children of index i are at 2i+1 and 2i+2, its parent at (i-1)/2.
    // The smallest item is always at index 0, so Pop() is O(log n) and Peek() is O(1).
    public class MinHeap<T>
    {
        private readonly List<T> items = new List<T>();
        private readonly IComparer<T> comparer;

        public MinHeap(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public int Count => items.Count;

        public void Clear()
        {
            items.Clear();
        }

        // Add at the end of the tree, then "bubble up" while smaller than the parent.
        public void Push(T item)
        {
            items.Add(item);
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (comparer.Compare(items[i], items[parent]) >= 0) break;
                Swap(i, parent);
                i = parent;
            }
        }

        // Remove the root (smallest), move the last item to the root, then "sink down"
        // by swapping with the smaller child until the heap order is restored.
        public T Pop()
        {
            T top = items[0];
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;
                if (left < items.Count && comparer.Compare(items[left], items[smallest]) < 0) smallest = left;
                if (right < items.Count && comparer.Compare(items[right], items[smallest]) < 0) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
        }
    }
}
