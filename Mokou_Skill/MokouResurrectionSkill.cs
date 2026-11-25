using Microsoft.Xna.Framework;
using Mokou_Character.Character;
using Terraria;
using Terraria.ID;
using CharacterRely.Content.SkillSystem;

namespace Mokou_Character.Skill;

/// <summary>焚身传送，不经过死亡、涅槃或普通技能的耗血/强化结算。</summary>
public sealed class MokouResurrectionSkill : CharacterRely.Content.SkillSystem.Skill
{
    public const string SkillId = "resurrection";
    private Vector2 targetCenter, heldPosition;
    private Color dark, light;
    private int serial, arrivalTick;
    private bool requested, arrived;
    private Projectile departure, arrival;

    public MokouResurrectionSkill() : base(new SkillInfo(SkillId, "Resurrection",
        "焚烧自身后传送到鼠标位置；打开全屏地图时，可传送到地图鼠标所指的位置。恢复当前人体自燃层数二分之一的生命（向下取整），随后清空全部层数。零层也可释放，不消耗生命，不触发死亡或不死鸟重生。冷却30秒。",
        cooldownTicks: ResurrectionRules.CooldownTicks,
        durationTicks: ResurrectionRules.BurnTicks + ResurrectionRules.AppearTicks + ResurrectionRules.NetworkTimeoutTicks,
        cost: 0)) { }

    protected override bool CanUse()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.CCed || Player.pulley
            || Player.mount.Active || Player.grapCount != 0 || Player.itemAnimation > 0 || Player.heldProj >= 0
            || Owner is not Mokou character || character.IsNirvana) return false;
        return !character.HasBlockingSkillAction(this);
    }

    protected override bool OnUse()
    {
        bool map = Main.mapFullscreen;
        if (map && (!Main.mapReady || !Main.mapEnabled || Main.mapFullscreenScale <= 0f)) return false;
        targetCenter = map
            ? ResurrectionRules.MapToWorld(new Vector2(PlayerInputMouseX, PlayerInputMouseY),
                new Vector2(Terraria.GameInput.PlayerInput.RealScreenWidth, Terraria.GameInput.PlayerInput.RealScreenHeight),
                Main.mapFullscreenPos, Main.mapFullscreenScale)
            : Main.MouseWorld;
        // 联机远端地图可能尚未下载，最终碰撞检测交给服务器；单机先检查，失败不进冷却。
        if (!ResurrectionTeleport.WithinWorld(targetCenter)) return false;
        if (Main.netMode == NetmodeID.SinglePlayer
            && !ResurrectionTeleport.TryFindPosition(Player, targetCenter, out _))
        {
            Main.NewText("目标附近没有容纳角色的空位。", Color.OrangeRed);
            return false;
        }
        var character = (Mokou)Owner;
        dark = character.fire_darkColor;
        light = character.fire_lightColor;
        departure = MokouResurrectionFlameProjectile.Spawn(Player, false, dark, light);
        if (departure == null) return false;
        serial++;
        requested = arrived = false;
        heldPosition = Player.position;
        HoldPosition();
        // 记录地图目标后收起地图，才能看见焚身和重现动画。
        if (map) Main.mapFullscreen = false;
        return true;
    }

    // Mouse.GetState 是原始窗口坐标，避免 ProcessTriggers 中 Main.mouseX 受世界缩放影响。
    private static int PlayerInputMouseX => Microsoft.Xna.Framework.Input.Mouse.GetState().X;
    private static int PlayerInputMouseY => Microsoft.Xna.Framework.Input.Mouse.GetState().Y;

    protected override void UpdateActive()
    {
        if (Player.dead || Player.CCed || Player.mount.Active || Player.pulley || Player.grapCount > 0
            || ((Mokou)Owner).IsNirvana)
        {
            End(interrupted: true);
            return;
        }
        HoldPosition();
        if (!requested && ElapsedTicks >= ResurrectionRules.BurnTicks)
        {
            requested = true;
            ResurrectionTeleport.Request((Mokou)Owner, serial, targetCenter);
        }
        if (arrived && ElapsedTicks - arrivalTick >= ResurrectionRules.AppearTicks) End();
        else if (!arrived && ElapsedTicks >= ResurrectionRules.BurnTicks + ResurrectionRules.NetworkTimeoutTicks)
        {
            Main.NewText("传送未完成，自燃层数已保留。", Color.OrangeRed);
            End(interrupted: true);
        }
    }

    /// <summary>只接收本次传送的确认；拒绝或迟到的包不会重复回血和消耗层数。</summary>
    internal void ReceiveResult(int castSerial, bool success, Vector2 position)
    {
        if (!IsActive || !requested || arrived || serial != castSerial) return;
        if (!success)
        {
            Main.NewText("目标附近无法传送，自燃层数已保留。", Color.OrangeRed);
            End(interrupted: true);
            return;
        }
        arrived = true;
        arrivalTick = ElapsedTicks;
        heldPosition = position;
        // 服务器先完成区域加载和传送；本地确认坐标并立刻结束原地锁定。
        if (Main.netMode == NetmodeID.MultiplayerClient)
            Player.Teleport(position, TeleportationStyleID.DebugTeleport);
        HoldPosition();
        arrival = MokouResurrectionFlameProjectile.Spawn(Player, true, dark, light);
        ((Mokou)Owner).ConsumeResurrectionStacks();
    }

    internal void HoldPosition()
    {
        Player.position = heldPosition;
        Player.velocity = Vector2.Zero;
        Player.fallStart = (int)(Player.position.Y / 16f);
    }

    internal float BodyOpacity => ResurrectionRules.BodyOpacity(arrived ? ElapsedTicks - arrivalTick : ElapsedTicks, arrived);

    protected override void OnEnd(bool interrupted)
    {
        if (interrupted)
        {
            StopVisual(departure);
            StopVisual(arrival);
        }
        departure = arrival = null;
        Player.velocity = Vector2.Zero;
    }

    private void StopVisual(Projectile projectile)
    {
        if (projectile?.active == true && projectile.owner == Player.whoAmI
            && projectile.ModProjectile is MokouResurrectionFlameProjectile) projectile.Kill();
    }
}
