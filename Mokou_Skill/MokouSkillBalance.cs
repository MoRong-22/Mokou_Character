using System;
using Terraria;
using Terraria.ModLoader;

namespace Mokou_Character.Skill;

/// <summary>使用当前世界已击败的 BOSS 进度；联机所有玩家共享世界进度。</summary>
internal enum MokouProgressionStage
{
    PreBoss, EvilBoss, Skeletron, Hardmode, FirstMech, AllMechs, Plantera, Golem, Lunar, MoonLord
}

/// <summary>未计装备、强化、暴击与防御的单次命中伤害。火柱/龙卷均为每 0.5 秒一次。</summary>
internal readonly record struct MokouSkillDamageProfile(
    int Claw, int Spin, int Dive, int Pillar, int SacrificeDash, int SacrificeFlame, int Vortex);

/// <summary>妹红技能的阶段数值入口；只调整此表即可平衡各技能，无需修改动作或特效。</summary>
internal static class MokouSkillBalance
{
    internal static MokouProgressionStage CurrentStage
    {
        get
        {
            // 从最高阶段向下查，兼容越级击杀；不根据装备或正在存活的 BOSS 临时升档。
            if (NPC.downedMoonlord) return MokouProgressionStage.MoonLord;
            if (NPC.downedAncientCultist) return MokouProgressionStage.Lunar;
            if (NPC.downedGolemBoss) return MokouProgressionStage.Golem;
            if (NPC.downedPlantBoss) return MokouProgressionStage.Plantera;
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) return MokouProgressionStage.AllMechs;
            if (NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3) return MokouProgressionStage.FirstMech;
            if (Main.hardMode) return MokouProgressionStage.Hardmode;
            if (NPC.downedBoss3) return MokouProgressionStage.Skeletron;
            if (NPC.downedBoss2) return MokouProgressionStage.EvilBoss;
            return MokouProgressionStage.PreBoss;
        }
    }

    internal static MokouSkillDamageProfile ForStage(MokouProgressionStage stage) => stage switch
    {
        //                                   爪   旋踢  飞踢  火柱  冲刺  火弹  龙卷
        MokouProgressionStage.PreBoss   => new( 24,  24,  36,  22,  48,   5,  12),
        MokouProgressionStage.EvilBoss  => new( 36,  38,  58,  34,  70,   8,  18),
        MokouProgressionStage.Skeletron => new( 52,  54,  82,  48, 100,  12,  26),
        MokouProgressionStage.Hardmode  => new( 85,  85, 130,  75, 155,  20,  40),
        MokouProgressionStage.FirstMech => new(115, 115, 175,  95, 205,  28,  52),
        MokouProgressionStage.AllMechs  => new(145, 145, 220, 120, 260,  36,  65),
        MokouProgressionStage.Plantera  => new(190, 185, 280, 155, 335,  46,  85),
        MokouProgressionStage.Golem     => new(230, 225, 340, 185, 405,  56, 100),
        MokouProgressionStage.Lunar     => new(300, 290, 440, 240, 525,  72, 130),
        MokouProgressionStage.MoonLord  => new(400, 380, 580, 320, 690,  95, 175),
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    /// <summary>一次释放只读取一次进度与魔法伤害加成；后续连段、火柱和飞弹伤害不随换装跳变。</summary>
    internal static MokouSkillDamageSnapshot Capture(Player player, bool empowered)
        => new(ForStage(CurrentStage), empowered,
            player.GetTotalDamage(DamageClass.Magic));
}

/// <summary>只用魔法伤害加成，包含通用加成且只计算一次；近战、远程、召唤装备不参与。</summary>
internal readonly record struct MokouSkillDamageSnapshot(
    MokouSkillDamageProfile Base, bool Empowered, StatModifier Magic)
{
    internal int Resolve(int baseDamage, int hitCount = 1)
    {
        float damage = Magic.ApplyTo(baseDamage);
        float multiplier = Empowered ? Character.Mokou.EmpoweredSkillDamageMultiplier : 1f;
        // 强化双手火爪把两次命中合并为一次，额外固定伤害也按两击计，不丢失一只手的加成。
        return Math.Max(1, (int)MathF.Round(damage * multiplier * hitCount));
    }
}
