namespace Norn.GameCore;

public partial class Skills
{
    // mirrors: Skills.SkillType
    // source:  Valheim 1.0.7
    // note:    Explicit values only on Jump, Ride, and All — the rest are
    //          implicit sequential, exactly as source declares them. Gap at
    //          109 (between Dodge=108 and the explicit Ride=110) is in
    //          source too.
    /// <summary>A skill's type, per <see cref="Skill"/>.</summary>
    public enum SkillType
    {
        None,
        Swords,
        Knives,
        Clubs,
        Polearms,
        Spears,
        Blocking,
        Axes,
        Bows,
        ElementalMagic,
        BloodMagic,
        Unarmed,
        Pickaxes,
        WoodCutting,
        Crossbows,
        Jump = 100,
        Sneak,
        Run,
        Swim,
        Fishing,
        Cooking,
        Farming,
        Crafting,
        Dodge,
        Ride = 110,
        All = 999
    }
}
