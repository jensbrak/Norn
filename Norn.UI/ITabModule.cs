using Avalonia.Controls;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// One self-contained tab. Adding a tab is one new file implementing this
/// interface plus one line in <see cref="TabModules"/> — no shell edit.
/// </summary>
/// <remarks>
/// One signature for every tab, editable or not — a tab with no
/// editable field just never calls <c>onEdited</c> or mutates
/// <paramref name="editor"/>. Read via <c>editor.View.Xxx</c>.
/// <para>
/// <c>onMessage</c> (added for the Inventory interaction-model pass) posts a
/// short, transient status-bar note for an action a tab just took — e.g.
/// "Repaired 3 items". Most tabs ignore it today; it's on every tab's
/// signature from the start, not added per-tab later, so adopting it
/// elsewhere is a call-site change, not another interface change.
/// </para>
/// </remarks>
public interface ITabModule
{
    string Title { get; }

    Control Build(CharacterEditor editor, Action onEdited, Action<string> onMessage);
}
