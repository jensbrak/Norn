using System.Reflection;
using Norn.UI;

namespace Norn.Tests;

/// <summary>
/// Guards the one failure mode <see cref="SettingsWindow"/>'s reflection-
/// driven generation can't itself catch loudly: a <see cref="Settings"/>
/// property with no <see cref="SettingAttribute"/> just never gets a row —
/// no exception, nothing to notice — unlike an unsupported property *type*,
/// which throws at window-open time and needs no separate test for that
/// reason.
/// </summary>
public class SettingsTests
{
    [Fact]
    public void Every_settings_property_has_a_Setting_attribute()
    {
        var missing = typeof(Settings).GetProperties()
            .Where(p => p.GetCustomAttribute<SettingAttribute>() is null)
            .Select(p => p.Name)
            .ToList();

        Assert.True(missing.Count == 0, $"Settings properties missing [Setting]: {string.Join(", ", missing)}");
    }
}
