using System;
using CharacterRely.Animation;
using CharacterRely.Content.ModCharacter;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Content.Mokou_Buff;
using Mokou_Character.Skill;
using Mokou_Character.Skill.Mokou_PassiveSkill;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mokou_Character.Character;

/// <summary>妹红角色：站立与移动动画、原版手臂动作及手持物品挂点。</summary>
public partial class Mokou : CharacterLib
{
    /// <summary>基础动作状态，使用轨道 0。</summary>
    public enum MovementState
    {
        /// <summary>站立。</summary>
        Idle,
        /// <summary>移动。</summary>
        Move
    }

    /// <summary>角色选择界面使用站立动画。</summary>
    public override string PreviewAnimationName => "idle";

    public override bool PassiveSkillsEnabled => true;

    /// <summary>四个主动技能依次填入新建装配表，已有存档仍可自行更换技能。</summary>
    protected override CharacterRely.Content.SkillSystem.Skill[] CreateSkills()
        => new CharacterRely.Content.SkillSystem.Skill[]
        {
            new MokouClawSkill(),
            new MokouKafuuKaiseyKickSkill(),
            new MokouPhoenixPossessionSkill(),
            new MokouSacrificeStrikeSkill()
        };

    /// <summary>本玩家的交替斜砍与强化双爪技能。</summary>
    public MokouClawSkill ClawSkill => (MokouClawSkill)GetSkill(MokouClawSkill.SkillId);

    /// <summary>本玩家的凯风快晴踢技能。</summary>
    public MokouKafuuKaiseyKickSkill KafuuKaiseyKickSkill
        => (MokouKafuuKaiseyKickSkill)GetSkill(MokouKafuuKaiseyKickSkill.SkillId);

    /// <summary>吟唱后进入不死鸟形态的主动技能。</summary>
    public MokouPhoenixPossessionSkill PhoenixPossessionSkill
        => (MokouPhoenixPossessionSkill)GetSkill(MokouPhoenixPossessionSkill.SkillId);

    internal bool IsPhoenixChanting => IsSelected && PhoenixPossessionSkill.IsActive;

    /// <summary>不死身的舍身击；后续冲刺次数保存在本玩家的技能实例中。</summary>
    public MokouSacrificeStrikeSkill SacrificeStrikeSkill
        => (MokouSacrificeStrikeSkill)GetSkill(MokouSacrificeStrikeSkill.SkillId);

    private bool IsHandSkillActive => ClawSkill.IsActive;
    private bool IsSkillPoseActive => IsHandSkillActive || KafuuKaiseyKickSkill.IsPerformingAction
        || IsPhoenixChanting || SacrificeStrikeSkill.IsDashing;
    
    protected override CharacterRely.Content.SkillSystem.PassiveSkill[]
        CreatePassiveSkills()
        => new CharacterRely.Content.SkillSystem.PassiveSkill[]
        {
            new Person_Of_Hourai(),
            new Fire_Ctrl(),
            new Nirvana(),
            new Jintai_Shizen_Hakka()
        };
    

    /// <summary>保持当前模型的游戏内显示大小，握点使用同一缩放。</summary>
    public override float CharacterScale => 0.06f;

    /// <summary>本玩家的基础动作状态机；重复选择相同状态不会重置动画。</summary>
    public SpineAnimationStateMachine<MovementState> MovementMachine
        => GetAnimationStateMachine<MovementState>(0, machine =>
        {
            machine.RegisterState(MovementState.Idle, "idle", fadeInSeconds: 0.1f);
            machine.RegisterState(MovementState.Move, "move", fadeInSeconds: 0.1f);
        });

    /// <summary>绑定手臂</summary>
    public VanillaItemActionAdapter VanillaActions
        => GetVanillaItemActions(actions =>
        {
            actions.BindFrontArm("front_arm", "front_hand");
            actions.BindBackArm("back_arm", "back_hand");
            // 主动技能期间由技能代码控制骨骼姿势，避免原版手臂姿势覆盖。
            actions.PoseFilter = _ => !IsSkillPoseActive;
        });

    /// <summary>加载资源</summary>
    protected override SpineAsset LoadAnimationAsset()
        => SpineAsset.Load(Mod, "Assets/mokou.json", "Assets/mokou.atlas");

    /// <summary>按水平移动速度选择动作；动画推进和绘制交给角色库。</summary>
    protected override void UpdateAnimation()
    {
        if (Main.dedServ || Player.dead) return;

        Animator.FlipX = Player.direction < 0;
        // 冲刺全身姿势以站立为基底，避免移动轨道的摆腿叠加到突进动作。
        MovementMachine.ChangeState(!SacrificeStrikeSkill.IsDashing && Math.Abs(Player.velocity.X) > 0.1f
            ? MovementState.Move : MovementState.Idle);
        _ = VanillaActions;
        UpdateFrontHandFire();
        UpdatePhoenixWings();
    }

    #region 人体自燃
    /// <summary>
    /// 人体自燃所累计的生命值
    /// </summary>
    public float Jintai_Shizen_Hakka_Life = 0;
    /// <summary>
    /// 人体自燃状态是否可用
    /// </summary>
    public bool Jintai_Shizen_Hakka_Active => Jintai_Shizen_Hakka_Life >= SpontaneousCombustionLifeRequired;

    #endregion
    
    

    #region 涅槃所需变量
    private const int NirvanaDuration = 6 * 60;

    private int nirvanaTimeLeft;
    private Vector2 nirvanaPosition;
    private Asset<Effect> nirvanaVortexEffect;
    internal bool IsNirvana => IsSelected && nirvanaTimeLeft > 0;

    /// <summary>是否启用重生与吟唱周围的热浪；关闭后停止请求场景 RT。</summary>
    public bool NirvanaHeatWaveEnabled { get; set; } = true;

    internal bool IsNirvanaHeatWaveActive => NirvanaHeatWaveEnabled && (IsNirvana || IsPhoenixChanting) &&
        IsDrawEnabled && Player.active && !Player.dead;

    /// <summary>热浪与旋风使用同一计时，兼容 6 秒重生和 3 秒吟唱。</summary>
    internal float PhoenixEffectDuration => IsNirvana ? NirvanaDuration / 60f
        : MokouPhoenixPossessionSkill.ChantTicks / 60f;

    private float PhoenixEffectElapsed => IsNirvana ? (NirvanaDuration - nirvanaTimeLeft) / 60f
        : PhoenixPossessionSkill.ElapsedTicks / 60f;
    #endregion
    
    /// <summary>火爪手部火焰开关；普通交替前后手，强化双手同时绘制。</summary>
    public bool FrontHandFireEnabled { get; set; } = true;

    private bool IsFrontHandFireActive => FrontHandFireEnabled && IsSelected
        && !Player.dead && !IsNirvana && IsHandSkillActive;

    private SpineBoneDrawHandle handDraw;
    private SpineBoneDrawHandle backHandDraw;
    private Asset<Effect> frontHandFireEffect;

    /// <summary>在更新阶段注册一次，实际绘制由骨骼渲染回调执行。</summary>
    private void UpdateFrontHandFire()
    {
        UpdateClawHandFire(ref handDraw, "front_hand", IsFrontHandFireActive && ClawSkill.FrontHandActive);
        UpdateClawHandFire(ref backHandDraw, "back_hand", IsFrontHandFireActive && ClawSkill.BackHandActive);
    }

    private void UpdateClawHandFire(ref SpineBoneDrawHandle handle, string bone, bool active)
    {
        if (!active)
        {
            handle?.Dispose();
            handle = null;
            return;
        }

        if (handle == null || handle.IsDisposed)
        {
            handle = RegisterBoneDraw(
                bone, BoneDrawOrder.OverBone,
                new Vector2(100f, 0f), // 沿前手骨骼接近末端的位置，随动画和镜像变化。
                DrawFrontHandFire, drawRadius: 48f);
        }
    }

    public Color fire_lightColor
    {
        get
        {
            if (lockedSkillLightColor.HasValue) return lockedSkillLightColor.Value;
            //开启人体自燃后颜色修改
            if (Jintai_Shizen_Hakka_Active)
            {
                if (Player.HasBuff(BuffID.WeaponImbueCursedFlames))
                {
                    return new Color(130, 200, 255);
                }
                else if (Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
                {
                    return new Color(204, 204, 255);
                }
                else
                {
                    return Color.GhostWhite;
                }
            }
            if (Player.HasBuff(BuffID.WeaponImbueCursedFlames))
            {
                return Color.LightGreen;
            }
            else if (Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
            {
                return Color.BlueViolet;
            }
            else
            {
                return new Color(255, 184, 51);
            }
        }
    }
    public Color fire_darkColor
    {
        get
        {
            if (lockedSkillDarkColor.HasValue) return lockedSkillDarkColor.Value;
            //开启人体自燃后颜色修改
            if (Jintai_Shizen_Hakka_Active)
            {
                if (Player.HasBuff(BuffID.WeaponImbueCursedFlames))
                {
                    return new Color(64, 224, 208);
                }
                else if (Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
                {
                    return new Color(111, 0, 255);
                }
                else
                {
                    return Color.DodgerBlue;
                }
            }
            if (Player.HasBuff(BuffID.WeaponImbueCursedFlames))
            {
                return Color.Green;
            }
            else if (Player.HasBuff(ModContent.BuffType<WeaponImbueShadowFlame>()))
            {
                return Color.Purple;
            }
            else
            {
                return new Color(166, 15, 3);
            }
        }
    }

    /// <summary>火焰根部对准前手，向上燃烧；使用回调坐标，兼容角色像素 RT。</summary>
    private void DrawFrontHandFire(SpineBoneDrawContext ctx)
    {
        if (!IsFrontHandFireActive) return;

        frontHandFireEffect ??= ModContent.Request<Effect>(
            "Mokou_Character/Assets/Effects/FireFlame", AssetRequestMode.ImmediateLoad);
        Effect effect = frontHandFireEffect.Value;
        effect.Parameters["uTime"].SetValue((float)Main.GlobalTimeWrappedHourly);
        effect.Parameters["uDarkColor"].SetValue(fire_darkColor.ToVector3());
        effect.Parameters["uLightColor"].SetValue(fire_lightColor.ToVector3());
        effect.Parameters["uDirection"].SetValue(new Vector2(0f, -1f));

        Vector2 size = new Vector2(32f, 40f); // 火焰绘制区域，单位为游戏像素。
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 textureSize = new Vector2(pixel.Width, pixel.Height);
        // FireFlame 中向上燃烧时，height=0 对应 UV=(0.5,0.92)。
        Vector2 origin = new Vector2(0.5f, 0.92f) * textureSize;
        SpriteBatch sb = ctx.SpriteBatch;
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
            effect, ctx.TransformMatrix);
        try
        {
            sb.Draw(pixel, ctx.Position, null, Color.White, 0f, origin,
                size / textureSize, SpriteEffects.None, 0f);
        }
        finally
        {
            sb.End();
        }
    }

    public override void DrawCharacter(GraphicsDevice graphicsDevice)
    {
        if ((!IsNirvana && !IsPhoenixChanting) || Main.dedServ || Player.dead)
        {
            base.DrawCharacter(graphicsDevice);
            return;
        }

        nirvanaVortexEffect ??= ModContent.Request<Effect>(
            "Mokou_Character/Assets/Effects/NirvanaVortex",
            AssetRequestMode.ImmediateLoad);

        // 从本次动画开始计算的秒数
        float elapsed = PhoenixEffectElapsed;
        float duration = PhoenixEffectDuration;
        Effect vortex = nirvanaVortexEffect.Value;
        Vector3 darkColor = fire_darkColor.ToVector3();
        Vector3 lightColor = fire_lightColor.ToVector3();
        SetNirvanaParameters(vortex, elapsed, duration, darkColor, lightColor);

        // 透视柱面的远侧、角色、近侧依次绘制；近侧用实心火焰完整包住角色。
        const int stormWidth = 340;
        const int stormHeight = 280;
        // 热浪在场景绘制结束时统一处理，这里不再抓屏或切换 RT。
        vortex.Parameters["uLayer"].SetValue(-1f);
        DrawNirvanaLayer(vortex, stormWidth, stormHeight);

        base.DrawCharacter(graphicsDevice);

        vortex.Parameters["uLayer"].SetValue(1f);
        DrawNirvanaLayer(vortex, stormWidth, stormHeight);
    }

    /// <summary>提供本帧热浪参数；场景系统复用原版两张 RT，不在角色绘制中备份整屏。</summary>
    internal bool TryGetNirvanaHeatWave(out Vector2 center, out Vector2 halfSize, out float elapsed)
    {
        center = default;
        halfSize = default;
        elapsed = 0f;
        if (!IsNirvanaHeatWaveActive)
            return false;
        elapsed = PhoenixEffectElapsed;

        const int width = 340;
        const int height = 280;
        Matrix view = Main.GameViewMatrix.TransformationMatrix;
        center = Vector2.Transform(
            Player.Bottom - Main.screenPosition - new Vector2(0f, height * 0.5f), view);
        halfSize = new Vector2(
            Vector2.TransformNormal(new Vector2(width * 0.5f, 0f), view).Length(),
            Vector2.TransformNormal(new Vector2(0f, height * 0.5f), view).Length());
        return center.X + halfSize.X > 0f && center.X - halfSize.X < Main.screenWidth &&
               center.Y + halfSize.Y > 0f && center.Y - halfSize.Y < Main.screenHeight;
    }

    /// <summary>旋风前后表面共用本次重生/吟唱的播放时间、暗焰色和亮焰色。</summary>
    private static void SetNirvanaParameters(Effect effect, float elapsed, float duration, Vector3 darkColor, Vector3 lightColor)
    {
        effect.Parameters["uTime"].SetValue(elapsed);
        effect.Parameters["uDuration"].SetValue(duration);
        effect.Parameters["uSize"].SetValue(Vector2.One);
        effect.Parameters["uDarkColor"].SetValue(darkColor);
        effect.Parameters["uLightColor"].SetValue(lightColor);
        effect.Parameters["uIntensity"].SetValue(1f);
        effect.Parameters["uSwirlSpeed"].SetValue(1f);
    }

    /// <summary>以脚底为锚点绘制一层，不创建额外 RT 或粒子。</summary>
    private void DrawNirvanaLayer(Effect effect, int width, int height)
    {
        Vector2 feet = Player.Bottom - Main.screenPosition;
        // 留出炸散的空间；UV 换算保持成型时的龙卷尺寸和脚底锚点不变。
        const int paddingX = 110;
        const int paddingY = 80;
        effect.Parameters["uCanvas"].SetValue(new Vector4(
            (width + paddingX * 2f) / width, (height + paddingY * 2f) / height,
            -paddingX / (float)width, -paddingY / (float)height));

        Rectangle area = new Rectangle(
            (int)feet.X - width / 2 - paddingX,
            (int)feet.Y - height - paddingY,
            width + paddingX * 2,
            height + paddingY * 2);

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            effect,
            Main.GameViewMatrix.TransformationMatrix);

        try
        {
            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                area,
                Color.White);
        }
        finally
        {
            Main.spriteBatch.End();
        }
    }

    /// <summary>将原版手持物品移到前臂末端，返回世界坐标。</summary>
    public override bool TryGetHeldItemDrawPosition(out Vector2 worldPosition)
    {
        worldPosition = default;
        if (!IsSelected || !IsDrawEnabled || Main.dedServ || !Player.active || Player.dead)
            return false;

        var actions = VanillaActions;
        Animator.FlipX = Player.direction < 0;
        Animator.RefreshWorldPose();
        if (!actions.TryGetHandPosition(true, out Vector2 hand)) return false;
        
        
        worldPosition = Player.Bottom + hand * CharacterScale;
        return true;
    }

    private int KillCD = 0;
    private int? NPCIndex = null;
    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust,
        ref PlayerDeathReason damageSource)
    {
        if (IsNirvana)
        {
            Player.statLife = 1;
            return false;
        }
        if (IsSelected && (KillCD == 0 || sacrificeRebirthPending))
        {
            NPCIndex = damageSource.SourceNPCIndex;
            KillCD = 3600;
            Player.statLife = 1;
            //涅槃期间无敌
            
            StartNirvana();
            return false;
        }

        return true;
    }

    public override void PreUpdate()
    {
        // 冷却按游戏实际运行时间推进；暂停不消耗重生冷却。
        if (KillCD > 0) KillCD--;
        if (!IsSelected || Player.dead)
        {
            nirvanaTimeLeft = 0;
            return;
        }
        #region 涅槃所需
        //普通的时间自减
        if (nirvanaTimeLeft > 0)
        {
            nirvanaTimeLeft--;
            if (nirvanaTimeLeft == 0)
            {
                Player.statLife = Player.statLifeMax2;
                EnterPhoenixForm();
            }
        }
        
        if (IsNirvana)
        {
            Player.immune = true;
            Player.immuneTime = 2;
            Player.immuneNoBlink = true;
        }
        #endregion
    }

    public override bool CanUseItem(Item item)
    {
        return !IsNirvana && (!IsSelected || !IsSkillPoseActive);
    }

    public override void PostUpdateEquips()
    {
        #region 玩家选取妹红后修改其防御力以及生命恢复
        if (IsSelected)
        {
            CapturePassiveRecoveryRate();
            Player.statDefense *= 0f;
        }
        #endregion
        ApplyPhoenixFlight();
    }
    
    public override void SetControls()
    {
        // 技能只占用手部操作，移动仍由基础动作轨道处理。
        if (IsSelected && IsSkillPoseActive)
        {
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        if (IsSelected && (KafuuKaiseyKickSkill.IsPerformingAction || SacrificeStrikeSkill.IsDashing))
        {
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
        }

        #region 涅槃所需
        //防止后面和别的冲突
        if (IsNirvana || IsPhoenixChanting)
        {
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false; 
            Player.controlUseTile = false; 
        }
        #endregion
    }

    #region 涅槃所需方法
    /// <summary>
    /// 开启涅槃 保持玩家位置并且静止不动
    /// </summary>
    public void StartNirvana()
    {
        if (!IsSelected || IsNirvana)
            return;

        // 先标记已在重生，避免舍身技能的中断回调再次触发死亡与重生。
        nirvanaTimeLeft = NirvanaDuration;
        nirvanaPosition = Player.position;
        foreach (var skill in Skills) skill.End(interrupted: true);
        Player.ClearBuff(ModContent.BuffType<PhoenixFormBuff>());
        Player.velocity = Vector2.Zero;
    }
    #endregion
    
}

#region 限伤
public class Mokou_DamageModify_Projectile : GlobalProjectile
{
    public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
    {
        if (target.GetModPlayer<Mokou>().IsSelected)
            return;
        int maxDamage = Math.Max(
            1, (int)(target.statLifeMax2 * 0.3f));

        modifiers.SetMaxDamage(maxDamage);
    }
}

public class Mokou_DamageModify_NPC : GlobalNPC
{
    public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
    {
        if (target.GetModPlayer<Mokou>().IsSelected)
            return;
        int maxDamage = Math.Max(
            1, (int)(target.statLifeMax2 * 0.3f));

        modifiers.SetMaxDamage(maxDamage);
    }
}
#endregion
