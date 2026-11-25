using System;
using CharacterRely.Animation;
using Microsoft.Xna.Framework;
using Mokou_Character.Animation;
using Terraria;

namespace Mokou_Character.Character;

public partial class Mokou
{
    private SpinePlayer locomotionAnimator;
    private MokouLocomotion locomotion;
    private MokouLocomotionSettings locomotionSettings;
    private int flightPoseHold;

    /// <summary>本角色基础动作参数，可调整呼吸、迈步、倾斜、过渡和拍翼速度。</summary>
    public MokouLocomotionSettings LocomotionSettings => locomotionSettings ??= new();

    private bool SuspendLocomotion => IsNirvana || IsPhoenixChanting || IsResurrecting
        || KafuuKaiseyKickSkill.IsPerformingAction || SacrificeStrikeSkill.IsDashing
        || Player.mount.Active || Player.pulley || Player.grapCount > 0 || Player.CCed;

    /// <summary>持物入口可能早于 PostUpdate；保证任何创建顺序下基础动作都先于手臂适配器。</summary>
    private void EnsureLocomotion()
    {
        SpinePlayer animator = Animator;
        if (!ReferenceEquals(locomotionAnimator, animator))
        {
            if (locomotionAnimator != null && !locomotionAnimator.IsDisposed)
                locomotionAnimator.WorldPoseApplied -= ApplyLocomotion;
            locomotionAnimator = animator;
            locomotion = new MokouLocomotion(animator.Skeleton, CharacterScale, LocomotionSettings);
            // 基础动作先注册，VanillaActions 最后覆盖持物/射击的手臂。
            animator.WorldPoseApplied += ApplyLocomotion;
            flightPoseHold = 0;
        }
    }

    private void UpdateLocomotion()
    {
        EnsureLocomotion();
        Vector2 probe = new(0f, 2f * Player.gravDir);
        bool grounded = Math.Abs(Player.velocity.Y) < 0.15f
            && Collision.TileCollision(Player.position, probe, Player.width, Player.height,
                false, false, (int)Player.gravDir).Y != probe.Y;
        bool powered = !grounded && Player.controlJump
            && ((Player.wingsLogic > 0 && Player.wingTime > 0f)
                || (Player.rocketBoots > 0 && Player.rocketTime > 0 && Player.jump == 0));
        // 松开飞行键短暂保留滑翔姿势，避免收腿/伸腿闪切。
        if (powered) flightPoseHold = 8;
        else if (grounded || SuspendLocomotion) flightPoseHold = 0;
        else if (flightPoseHold > 0) flightPoseHold--;
        var input = new LocomotionInput(new Vector2(Player.velocity.X, Player.velocity.Y * Player.gravDir),
            grounded, flightPoseHold > 0, SuspendLocomotion, IsHandSkillActive);
        locomotion.Tick(input, 1f / 60f);
        MovementMachine.ChangeState(locomotion.State switch
        {
            LocomotionState.Move => MovementState.Move,
            LocomotionState.Rise => MovementState.Rise,
            LocomotionState.Fall => MovementState.Fall,
            LocomotionState.Fly => MovementState.Fly,
            _ => MovementState.Idle
        });
    }

    private void ApplyLocomotion()
        => locomotion?.Apply(SuspendLocomotion || startingSkillLifeUse, IsHandSkillActive);
}
