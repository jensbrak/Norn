namespace Norn.GameCore;

// mirrors: StringExtensionMethods (assembly_utils)
// source:  Valheim 1.0.7
// note:    STUB, NEW ON THIS PATH AT 1.0.7. Only GetStableHashCode is carried.
//          The type also declares AllIndicesOf and several other string helpers
//          in source; none are reachable from any load/save path.
// note:    WHY THIS IS IN GameCore AND NOT Primitives. It looks like a
//          general-purpose hash, but it is not format-level plumbing the way
//          ZPackage is — it is game code, it lives in a game assembly, and its
//          exact arithmetic is now load-bearing for the inventory wire format.
//          If IronGate ever changes it, every stored prefab hash changes
//          meaning, and that is a patch-day event that has to be caught by
//          diffing GameCore against source. Primitives is the layer that
//          "changes ~never"; this is not that.
// note:    WHY IT IS NOT AN EXTENSION METHOD HERE. Source declares it as
//          `this string str`. Held as a plain static here so it reads as a
//          deliberate call at the one site that uses it (Inventory's item
//          writer) rather than disappearing into method-call syntax on every
//          string in the project.
public static class StringExtensionMethods
{
    // mirrors: StringExtensionMethods.GetStableHashCode(string)
    // source:  Valheim 1.0.7
    // note:    A two-lane djb2 variant over UTF-16 code units, combined at the
    //          end with a fixed multiplier. Transcribed operation for
    //          operation: the shift-add-xor form, the two independent
    //          accumulators, the pairwise stride, the early break, and the
    //          final `num + num2 * 1566083941` are all source's. Do not
    //          "simplify" any of it — this function's output IS the wire
    //          format for a prefab identity from item version 108 onward, so
    //          any deviation silently corrupts every item in every save.
    // note:    OVERFLOW. Source relies on C#'s default unchecked integer
    //          arithmetic; every operation here wraps. Norn builds with the
    //          default (unchecked) setting, so this matches — but if
    //          CheckForOverflowUnderflow is ever turned on project-wide, this
    //          method needs an explicit `unchecked` block or it will start
    //          throwing on ordinary input.
    // note:    NUL TERMINATION. The loop stops at the first '\0' rather than
    //          at the string's end, so "abc" and "abc\0def" hash identically.
    //          That is source's behaviour, not an oversight.
    // note:    ONE-WAY. There is no inverse. From item version 108 the save
    //          stores only this hash, never the prefab name, so an item's name
    //          cannot be recovered from a save file alone — it needs a
    //          hash-to-name table built from the game's asset database. That is
    //          an Adapter-layer capability question, deliberately not solved
    //          here.
    public static int GetStableHashCode(string str)
    {
        int num = 5381;
        int num2 = num;
        int num3 = 0;
        while (num3 < str.Length && str[num3] != '\0')
        {
            num = ((num << 5) + num) ^ str[num3];
            if (num3 == str.Length - 1 || str[num3 + 1] == '\0')
            {
                break;
            }

            num2 = ((num2 << 5) + num2) ^ str[num3 + 1];
            num3 += 2;
        }

        return num + num2 * 1566083941;
    }
}
