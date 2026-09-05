using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Navy1851;

public sealed class ItemNavy : Item
{
    private const string RoundsKey = "navy1851:rounds";
    private const string NextShotKey = "navy1851:nextshot";
    private readonly Dictionary<long, Use> uses = new();
    private long cleanupListener;
    private sealed class Use(ItemSlot slot)
    {
        public readonly ItemSlot Slot = slot;
        public readonly ItemStack Stack = slot.Itemstack!;
        public readonly WeaponControls Controls = new();
    }
    private ModelTransform? aimTransform;
    private static int Rounds(ItemStack stack) => Chamber.Clamp(stack.Attributes.GetInt(RoundsKey));
    private static AssetLocation Sound(string name) => new("navy1851", "sounds/" + name);

    public override void OnLoaded(ICoreAPI coreApi)
    {
        base.OnLoaded(coreApi);
        aimTransform = Attributes["aimTransform"].AsObject<ModelTransform>();
        cleanupListener = coreApi.Event.RegisterGameTickListener(TickHeldControls, 20);
    }

    public override void OnUnloaded(ICoreAPI coreApi)
    {
        coreApi.Event.UnregisterGameTickListener(cleanupListener);
        uses.Clear();
        base.OnUnloaded(coreApi);
    }

    private void TickHeldControls(float dt)
    {
        var present = new HashSet<long>();
        foreach (var player in api.World.AllOnlinePlayers)
        {
            var entity = player.Entity;
            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (!entity.Alive || slot.Empty || !ReferenceEquals(slot.Itemstack.Collectible, this)) continue;
            if (api is ICoreClientAPI client && player.PlayerUID != client.World.Player.PlayerUID) continue;
            present.Add(entity.EntityId);
            if (!uses.TryGetValue(entity.EntityId, out var use) || !ReferenceEquals(use.Stack, slot.Itemstack))
                uses[entity.EntityId] = use = new Use(slot);
            long now = api.World.ElapsedMilliseconds;
            bool inWorld = api is not ICoreClientAPI capi || capi.Input.MouseGrabbed;
            bool left = inWorld && entity.Controls.LeftMouseDown;
            bool right = inWorld && entity.Controls.RightMouseDown;
            bool pressed = use.Controls.Update(left, right, entity.Controls.Sneak, now);
            if (api.Side != EnumAppSide.Server) continue;
            if (pressed) api.World.PlaySoundAt(Sound(Rounds(use.Stack) > 0 ? "cock" : "empty"), entity, null, false, 12, 0.6f);
            if (use.Controls.CanLoad(Rounds(use.Stack), now))
            {
                int loaded = 0;
                int needed = Chamber.ReloadAmount(Rounds(use.Stack), Chamber.Capacity);
                while (loaded < needed)
                {
                    var ammo = FindAmmo(entity);
                    var taken = ammo?.TakeOut(needed - loaded);
                    if (taken == null || taken.StackSize <= 0) break;
                    loaded += taken.StackSize;
                    ammo!.MarkDirty();
                }
                if (loaded > 0)
                {
                    use.Stack.Attributes.SetInt(RoundsKey, Rounds(use.Stack) + loaded);
                    slot.MarkDirty();
                    use.Controls.Loaded(now);
                    api.World.PlaySoundAt(Sound("load"), entity, null, false, 12, 0.7f);
                }
            }
            if (use.Controls.Trigger is { } trigger &&
                trigger.TryFire(Rounds(use.Stack), now, entity.Attributes.GetLong(NextShotKey), out int remaining))
            {
                entity.Attributes.SetLong(NextShotKey, trigger.NextShot);
                use.Stack.Attributes.SetInt(RoundsKey, remaining);
                slot.MarkDirty();
                Fire(entity, use.Controls.Spread(now));
            }
        }
        foreach (long id in uses.Keys.ToArray()) if (!present.Contains(id)) uses.Remove(id);
    }

    // Default melee/block interactions are suppressed. The two in-world mouse-button
    // bits are synchronized by the engine independently, so aiming never cancels fire.
    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent entity, BlockSelection blockSel,
        EntitySelection entitySel, ref EnumHandHandling handling) => handling = EnumHandHandling.PreventDefault;
    public override bool OnHeldAttackStep(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel) => true;
    public override bool OnHeldAttackCancel(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason reason) => true;
    public override void OnHeldAttackStop(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel) { }
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent entity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) => handling = EnumHandHandling.PreventDefault;
    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel) => true;
    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason reason) => true;
    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel) { }

    // The generic use/hit poses twist the weapon away from the sights.
    public override string GetHeldTpUseAnimation(ItemSlot slot, Entity entity) => null!;
    public override string GetHeldTpHitAnimation(ItemSlot slot, Entity entity) => null!;
    public override string GetHeldReadyAnimation(ItemSlot slot, Entity entity, EnumHand hand) => null!;

    private static ItemSlot? FindAmmo(EntityAgent entity)
    {
        ItemSlot? found = null;
        entity.WalkInventory(slot =>
        {
            if (slot is not ItemSlotCreative && !slot.Empty && slot.Itemstack.Collectible.Code == new AssetLocation("navy1851:papercharge"))
            { found = slot; return false; }
            return true;
        });
        return found;
    }

    private void Fire(EntityAgent entity, float spread)
    {
        var world = entity.World;
        var player = ((EntityPlayer)entity).Player;
        // Game-unit tuning; this is a short-range hitscan weapon, not a ballistics simulation.
        float range = Attributes["range"].AsFloat(40);
        float pitch = entity.Pos.Pitch + (float)(world.Rand.NextDouble() - 0.5) * spread;
        float yaw = entity.Pos.Yaw + (float)(world.Rand.NextDouble() - 0.5) * spread;
        Vec3d eye = entity.Pos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
        BlockSelection? block = null;
        EntitySelection? hit = null;
        world.RayTraceForSelection(eye, pitch, yaw, range, ref block, ref hit, null,
            candidate => candidate.EntityId != entity.EntityId && candidate.IsInteractable);
        // Non-damageable targets still stop the ray, rather than letting shots pass through them.
        if (hit?.Entity is Entity target && target.Alive && CanDamage(player, target))
        {
            target.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Player, Type = EnumDamageType.PiercingAttack,
                SourceEntity = entity, CauseEntity = entity, SourcePos = eye,
                DamageTier = Attributes["damageTier"].AsInt(2), KnockbackStrength = 0.45f
            }, Attributes["damage"].AsFloat(10));
        }
        world.PlaySoundAt(Sound("shot"), entity, null, false, 64, 1);
        var muzzle = eye.AheadCopy(0.65, entity.Pos.Pitch, entity.Pos.Yaw);
        world.SpawnParticles(18, ColorUtil.ToRgba(150, 193, 187, 167), muzzle, muzzle.AddCopy(0.12, 0.12, 0.12),
            new Vec3f(-0.12f, 0.18f, -0.12f), new Vec3f(0.12f, 0.6f, 0.12f), 0.7f, -0.04f, 0.3f);
        world.SpawnParticles(4, ColorUtil.ToRgba(255, 255, 204, 112), muzzle, muzzle,
            new Vec3f(-0.08f, -0.08f, -0.08f), new Vec3f(0.08f, 0.08f, 0.08f), 0.055f, 0, 0.4f);
    }

    private bool CanDamage(IPlayer player, Entity target)
    {
        if (api is not ICoreServerAPI server) return false;
        if (target is EntityPlayer && (!server.Server.Config.AllowPvP || !player.HasPrivilege(Privilege.attackplayers))) return false;
        if (target is not EntityPlayer && !player.HasPrivilege(Privilege.attackcreatures)) return false;
        return api.World.Claims.TryAccess(player, target.Pos.AsBlockPos, EnumBlockAccessFlags.Use);
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack stack, EnumItemRenderTarget target, ref ItemRenderInfo info)
    {
        base.OnBeforeRender(capi, stack, target, ref info);
        if (target != EnumItemRenderTarget.HandTp || !ReferenceEquals(capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack, stack)) return;
        info.Transform = info.Transform.Clone();
        long now = capi.World.ElapsedMilliseconds;
        int rounds = Rounds(stack);
        int previousRounds = stack.TempAttributes.GetInt("navy1851:seenRounds", rounds);
        if (rounds < previousRounds) stack.TempAttributes.SetLong("navy1851:kick", now);
        stack.TempAttributes.SetInt("navy1851:seenRounds", rounds);
        long kickTime = stack.TempAttributes.GetLong("navy1851:kick", -1000);
        float kick = Math.Max(0, 1 - (now - kickTime) / 240f);
        if (uses.TryGetValue(capi.World.Player.Entity.EntityId, out var use) && ReferenceEquals(use.Stack, stack))
        {
            if (use.Controls.Reloading)
            {
                info.Transform.Rotation.X -= 18;
                info.Transform.Rotation.Z += 12;
            }
            else if (aimTransform != null)
            {
                float amount = use.Controls.AimFraction(now);
                info.Transform.Translation.X += (aimTransform.Translation.X - info.Transform.Translation.X) * amount;
                info.Transform.Translation.Y += (aimTransform.Translation.Y - info.Transform.Translation.Y) * amount;
                info.Transform.Translation.Z += (aimTransform.Translation.Z - info.Transform.Translation.Z) * amount;
                info.Transform.Rotation.X += (aimTransform.Rotation.X - info.Transform.Rotation.X) * amount;
                info.Transform.Rotation.Y += (aimTransform.Rotation.Y - info.Transform.Rotation.Y) * amount;
                info.Transform.Rotation.Z += (aimTransform.Rotation.Z - info.Transform.Rotation.Z) * amount;
            }
        }
        info.Transform.Rotation.Z += kick * 1.5f;
        info.Transform.Translation.Z += kick * 0.01f;
    }

    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder text, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, text, world, withDebugInfo);
        text.AppendLine(Lang.Get("navy1851:rounds", Rounds(slot.Itemstack!), Chamber.Capacity));
        text.AppendLine(Lang.Get("navy1851:description"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot) =>
    [
        new() { ActionLangCode = "navy1851:help-fire", MouseButton = EnumMouseButton.Left },
        new() { ActionLangCode = "navy1851:help-aim", MouseButton = EnumMouseButton.Right },
        new() { ActionLangCode = "navy1851:help-reload", MouseButton = EnumMouseButton.Right, HotKeyCode = "sneak" }
    ];
}
