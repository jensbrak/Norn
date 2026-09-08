namespace Norn.UI;

/// <summary>
/// Marks a <see cref="Settings"/> property as one <see cref="SettingsWindow"/>
/// should render a row for. Carries only what reflection can't already infer
/// from the property itself — name comes from <c>PropertyInfo.Name</c>, type
/// from <c>PropertyInfo.PropertyType</c>, default from the property's own
/// initializer (reading a fresh <c>new Settings()</c>) — <see cref="Group"/>
/// and <see cref="Description"/> are the only real payload this attribute
/// carries. Both <c>required</c>: a setting with no description is exactly
/// the kind of thing this whole approach exists to make impossible to forget,
/// not something to default silently.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute : Attribute
{
    public required string Group { get; init; }

    public required string Description { get; init; }
}
