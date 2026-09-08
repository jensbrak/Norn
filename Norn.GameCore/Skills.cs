using Norn.GameCore.Primitives;

namespace Norn.GameCore;

// mirrors: Skills
// source:  Valheim 1.0.7
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
    // source:  Valheim 1.0.7
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
            float accumulator = 0f;
            if (version >= 2)
            {
                accumulator = pkg.ReadSingle();
            }

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
    // source:  Valheim 1.0.7
    private static bool IsSkillValid(SkillType type)
    {
        return Enum.IsDefined(typeof(SkillType), type);
    }

    // mirrors: Skills.Save(ZPackage)
    // source:  Valheim 1.0.7
    // note:    Source writes (int)keyValuePair.Value.m_info.m_skill — sourced
    //          from the value's SkillDef reference, not the dictionary key
    //          (see the TYPE DIVERGENCE note on Skill.m_type). Behaviourally
    //          identical: source's own GetSkill always resolves m_info such
    //          that m_info.m_skill equals the dictionary key.
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
