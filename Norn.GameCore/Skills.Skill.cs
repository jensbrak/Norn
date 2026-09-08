namespace Norn.GameCore;

public partial class Skills
{
    // mirrors: Skills.Skill (fields relevant to the wire format only)
    // source:  Valheim 1.0.7
    // note:    TYPE DIVERGENCE. Source's Skill does not carry a skill type
    //          directly — it holds `m_info`, a reference to a SkillDef
    //          resolved by linear-scanning a Unity-inspector-populated asset
    //          list (Player.m_skills) this project does not have. m_type is
    //          placed directly on Skill here instead of modelling SkillDef at
    //          all. See the note on Skills.Save for why this is
    //          behaviourally identical on the load/save path.
    public class Skill
    {
        public SkillType m_type;

        public float m_level;

        public float m_accumulator;
    }
}
