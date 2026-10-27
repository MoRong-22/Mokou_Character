using System;
using CharacterRely.Animation;
using CharacterRely.Content.ModCharacter;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mokou_Character.Skill.Mokou_PassiveSkill;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
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
    
    protected override CharacterRely.Content.SkillSystem.PassiveSkill[]
        CreatePassiveSkills()
        => new CharacterRely.Content.SkillSystem.PassiveSkill[]
        {
            new Person_Of_Hourai()
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
        });

    /// <summary>加载资源</summary>
    protected override SpineAsset LoadAnimationAsset()
        => SpineAsset.Load(Mod, "Assets/mokou.json", "Assets/mokou.atlas");

    /// <summary>按水平移动速度选择动作；动画推进和绘制交给角色库。</summary>
    protected override void UpdateAnimation()
    {
        if (Main.dedServ || Player.dead) return;

        Animator.FlipX = Player.direction < 0;
        MovementMachine.ChangeState(Math.Abs(Player.velocity.X) > 0.1f
            ? MovementState.Move : MovementState.Idle);
        _ = VanillaActions;
    }

    #region 涅槃所需变量
    private const int NirvanaDuration = 5 * 60;

    private int nirvanaTimeLeft;
    private Vector2 nirvanaPosition;
    private Asset<Effect> nirvanaEffect;
    private Asset<Effect> nirvanaVortexEffect;
    private bool IsNirvana => IsSelected && nirvanaTimeLeft > 0;
    #endregion
    
    
    public override void DrawCharacter(GraphicsDevice graphicsDevice)
    {
        if (!IsNirvana || Main.dedServ || Player.dead)
        {
            base.DrawCharacter(graphicsDevice);
            return;
        }

        nirvanaEffect ??= ModContent.Request<Effect>(
            "Mokou_Character/Assets/Effects/NirvanaFlameStorm",
            AssetRequestMode.ImmediateLoad);
        nirvanaVortexEffect ??= ModContent.Request<Effect>(
            "Mokou_Character/Assets/Effects/NirvanaVortex",
            AssetRequestMode.ImmediateLoad);

        // 从本次动画开始计算的秒数
        float elapsed = (NirvanaDuration - nirvanaTimeLeft) / 60f;
        Vector3 fireColor = new Vector3(1f, 0.25f, 0.035f); // 橙红色 后面还需增加因咒火和暗影焰buff的色转
        Effect fire = nirvanaEffect.Value;
        Effect vortex = nirvanaVortexEffect.Value;
        SetNirvanaParameters(fire, elapsed, fireColor);
        SetNirvanaParameters(vortex, elapsed, fireColor);

        // 外层宽度为内层的两倍多。按后半圈、角色、内焰、前半圈的顺序包裹角色。
        const int stormWidth = 340;
        const int stormHeight = 280;
        vortex.Parameters["uLayer"].SetValue(-1f);
        DrawNirvanaLayer(vortex, stormWidth, stormHeight);

        base.DrawCharacter(graphicsDevice);

        DrawNirvanaLayer(fire, 160, 240);

        vortex.Parameters["uLayer"].SetValue(1f);
        DrawNirvanaLayer(vortex, stormWidth, stormHeight);
    }

    /// <summary>两层火焰共用本次涅槃的播放时间和主题颜色。</summary>
    private static void SetNirvanaParameters(Effect effect, float elapsed, Vector3 fireColor)
    {
        effect.Parameters["uTime"].SetValue(elapsed);
        effect.Parameters["uDuration"].SetValue(NirvanaDuration / 60f);
        effect.Parameters["uSize"].SetValue(Vector2.One);
        effect.Parameters["uFireColor"].SetValue(fireColor);
        effect.Parameters["uIntensity"].SetValue(1f);
        effect.Parameters["uSwirlSpeed"].SetValue(1f);
    }

    /// <summary>以脚底为锚点绘制一层，不创建额外 RT 或粒子。</summary>
    private void DrawNirvanaLayer(Effect effect, int width, int height)
    {
        Vector2 feet = Player.Bottom - Main.screenPosition;

        Rectangle area = new Rectangle(
            (int)feet.X - width / 2,
            (int)feet.Y - height,
            width,
            height);

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
        if (KillCD == 0)
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

    public override void UpdateAutopause()
    {
        if(KillCD > 0)
            KillCD -= 1;
    }

    public override void PreUpdate()
    {
        #region 涅槃所需
        //普通的时间自减
        if (nirvanaTimeLeft > 0)
        {
            nirvanaTimeLeft--;
            if (nirvanaTimeLeft == 0)
                Player.statLife = Player.statLifeMax2;
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
        return !IsNirvana;
    }

    public override void PostUpdateEquips()
    {
        Player.lifeRegen += 3 + 2* (Player.statDefense / 8);
        Player.statDefense *= 0f;
    }
    
    public override void SetControls()
    {
        #region 涅槃所需
        //防止后面和别的冲突
        if (IsNirvana)
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

        nirvanaTimeLeft = NirvanaDuration;
        nirvanaPosition = Player.position;
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
