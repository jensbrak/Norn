namespace Norn.GameCore;

// note: NOT IN SOURCE. Substitutes for Dictionary<TKey, TValue>.Add (throws on
// duplicate key), the indexer set (overwrites in place, preserving the key's
// original position), and HashSet<T>.Add (silently drops a duplicate).
// .NET does not contract either collection's enumeration order, so a writer
// that re-enumerates one cannot guarantee byte identity. Used wherever a mirrored field's source type is Dictionary<,> or
// HashSet<T>. Read order and gates at every call site are untouched; these
// only decide what happens to a value once it has already been read.
internal static class OrderedCollections
{
    internal static void Add<TKey, TValue>(List<KeyValuePair<TKey, TValue>> list, TKey key, TValue value)
    {
        foreach (KeyValuePair<TKey, TValue> pair in list)
        {
            if (EqualityComparer<TKey>.Default.Equals(pair.Key, key))
            {
                throw new ArgumentException("An item with the same key has already been added.");
            }
        }

        list.Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    internal static void Set<TKey, TValue>(List<KeyValuePair<TKey, TValue>> list, TKey key, TValue value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<TKey>.Default.Equals(list[i].Key, key))
            {
                list[i] = new KeyValuePair<TKey, TValue>(key, value);
                return;
            }
        }

        list.Add(new KeyValuePair<TKey, TValue>(key, value));
    }

    internal static void AddToSet<T>(List<T> list, T value)
    {
        foreach (T item in list)
        {
            if (EqualityComparer<T>.Default.Equals(item, value))
            {
                return;
            }
        }

        list.Add(value);
    }

    // note: For a Dictionary<TKey, TValue> substitute where TValue itself
    // carries the key as one of its own fields (Skills.m_skillData), rather
    // than a separate KeyValuePair. Same indexer-overwrite-in-place semantics
    // as Set above.
    internal static void SetByKey<TKey, TValue>(List<TValue> list, TKey key, TValue value, Func<TValue, TKey> keySelector)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<TKey>.Default.Equals(keySelector(list[i]), key))
            {
                list[i] = value;
                return;
            }
        }

        list.Add(value);
    }
}
