using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: Skills
// source:  Valheim 1.0.16
// note:    TYPE DIVERGENCE. Source keys a Dictionary<SkillType, Skill>;
//          held here as an ordered List<Skill> instead: .NET does not
//          contract Dictionary's enumeration order.
//          OrderedCollections.SetByKey reproduces the indexer's
//          overwrite-in-place-preserving-position semantics, keyed by
//          Skill.m_type rather than a separate dictionary key.
public partial class Skills
{
    public List<Skill> m_skillData = new List<Skill>();

    // mirrors: Skills.Load(ZPackage)
    // source:  Valheim 1.0.16
    // note:    Entries failing Enum.IsDefined are read (bytes consumed) but
    //          not stored, matching source's IsSkillValid check.
    public void Load(ZPackage pkg)
    {
        int version = pkg.ReadInt();
        m_skillData.Clear();
        int count = pkg.ReadInt();
        for (int i = 0; i < count; i++)
        {
            SkillType type = (SkillType)pkg.ReadInt();
            float level = pkg.ReadSingle();
            float accumulator = (version >= 2) ? pkg.ReadSingle() : 0f;

            if (IsSkillValid(type))
            {
                Skill skill = new Skill
                {
                    m_type = type,
                    m_level = level,
                    m_accumulator = accumulator
                };

                OrderedCollections.SetByKey(m_skillData, type, skill, s => s.m_type);
            }
        }
    }

    // mirrors: Skills.IsSkillValid(SkillType)
    // source:  Valheim 1.0.16
    private static bool IsSkillValid(SkillType type)
    {
        return Enum.IsDefined(typeof(SkillType), type);
    }

    // mirrors: Skills.Save(ZPackage)
    // source:  Valheim 1.0.16
    // note:    Source writes (int)keyValuePair.Value.m_info.m_skill — sourced
    //          from the value's SkillDef reference, not the dictionary key
    //          (see the TYPE DIVERGENCE note on Skill.m_type). Identical
    //          whenever the game has a SkillDef for the type. Source's GetSkill
    //          stores a null m_info when its asset list has none (plausibly
    //          None/All), and source's Save would then throw; this mirror
    //          writes the key instead, which is what keeps R1. Asset-dependent,
    //          not answerable from source alone.
    public void Save(ZPackage pkg)
    {
        pkg.Write(2);
        pkg.Write(m_skillData.Count);
        foreach (Skill skill in m_skillData)
        {
            pkg.Write((int)skill.m_type);
            pkg.Write(skill.m_level);
            pkg.Write(skill.m_accumulator);
        }
    }
}
