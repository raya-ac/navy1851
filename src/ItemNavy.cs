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
    private sealed class Use(ItemSlot slot, long now, bool reload)
    {
        public readonly ItemSlot Slot = slot;
        public readonly ItemStack Stack = slot.Itemstack!;
        public readonly long Started = now;
        public long LastLoad = now;
        public readonly bool Reload = reload;
        public readonly FiringCycle Firing = new(now);
    }
    private static int Rounds(ItemStack stack) => Chamber.Clamp(stack.Attributes.GetInt(RoundsKey));
    private static AssetLocation Sound(string name) => new("navy1851", "sounds/" + name);

    public override void OnLoaded(ICoreAPI coreApi)
    {
        base.OnLoaded(coreApi);
        cleanupListener = coreApi.Event.RegisterGameTickListener(_ =>
        {
            foreach (long id in uses.Keys.ToArray())
            {
                var entity = coreApi.World.GetEntityById(id) as EntityPlayer;
                if (entity == null || !entity.Alive || !Valid(entity, uses[id])) EndUse(id);
            }
        }, 1000);
    }

    public override void OnUnloaded(ICoreAPI coreApi)
    {
        coreApi.Event.UnregisterGameTickListener(cleanupListener);
        uses.Clear();
        base.OnUnloaded(coreApi);
    }

    private static bool Valid(EntityAgent entity, Use use)
        => entity.Alive && ReferenceEquals(use.Slot.Itemstack, use.Stack)
           && entity is EntityPlayer player
           && ReferenceEquals(player.Player.InventoryManager.ActiveHotbarSlot, use.Slot);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent entity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent || slot.Empty || entity is not EntityPlayer || !entity.Alive) return;
        EndUse(entity.EntityId);
        uses[entity.EntityId] = new Use(slot, entity.World.ElapsedMilliseconds, entity.Controls.Sneak);
        if (entity.World.Side == EnumAppSide.Server && !entity.Controls.Sneak)
            entity.World.PlaySoundAt(Sound(Rounds(slot.Itemstack) > 0 ? "cock" : "empty"), entity, null, false, 12, 0.6f);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (!uses.TryGetValue(entity.EntityId, out var use) || !Valid(entity, use)) return false;
        if (!use.Reload)
        {
            if (entity.World.Side == EnumAppSide.Server &&
                use.Firing.TryFire(Rounds(use.Stack), entity.World.ElapsedMilliseconds,
                    entity.Attributes.GetLong(NextShotKey), out int remaining))
            {
                entity.Attributes.SetLong(NextShotKey, use.Firing.NextShot);
                use.Stack.Attributes.SetInt(RoundsKey, remaining);
                use.Slot.MarkDirty();
                Fire(entity, use.Stack, entity.World.ElapsedMilliseconds - use.Started);
            }
            // Keep the completed hold latched until release; do not restart on empty.
            return true;
        }
        if (entity.World.Side == EnumAppSide.Client) return true;
        long now = entity.World.ElapsedMilliseconds;
        if (!Chamber.CanLoad(Rounds(use.Stack), now - use.LastLoad)) return Rounds(use.Stack) < Chamber.Capacity;
        var ammo = FindAmmo(entity);
        if (ammo == null) return false;
        // One completed interval consumes exactly one inventory item, on the server.
        if (ammo.TakeOut(1) == null) return false;
        ammo.MarkDirty();
        use.Stack.Attributes.SetInt(RoundsKey, Rounds(use.Stack) + 1);
        use.Slot.MarkDirty();
        use.LastLoad = now;
        entity.World.PlaySoundAt(Sound("load"), entity, null, false, 12, 0.7f);
        return Rounds(use.Stack) < Chamber.Capacity;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason reason)
    {
        EndUse(entity.EntityId);
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent entity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        EndUse(entity.EntityId);
    }

    private void EndUse(long entityId)
    {
        if (uses.Remove(entityId, out var use)) use.Firing.Stop();
    }

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

    private void Fire(EntityAgent entity, ItemStack stack, long held)
    {
        var world = entity.World;
        var player = ((EntityPlayer)entity).Player;
        // Game-unit tuning; this is a short-range hitscan weapon, not a ballistics simulation.
        float range = Attributes["range"].AsFloat(40);
        float spread = held >= 900 ? 0.003f : 0.016f;
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
        info.Transform.Rotation.Z += kick * 13;
        info.Transform.Translation.Z += kick * 0.08f;
        if (uses.TryGetValue(capi.World.Player.Entity.EntityId, out var use) && ReferenceEquals(use.Stack, stack))
        {
            if (use.Reload)
            {
                info.Transform.Rotation.X -= 24;
                info.Transform.Rotation.Z += 16 + (float)Math.Sin((now - use.Started) / 180.0) * 3;
            }
            else
            {
                float aim = Math.Min(1, (now - use.Started) / 250f);
                info.Transform.Translation.X -= 0.12f * aim;
                info.Transform.Translation.Y += 0.05f * aim;
            }
        }
    }

    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder text, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, text, world, withDebugInfo);
        text.AppendLine(Lang.Get("navy1851:rounds", Rounds(slot.Itemstack!), Chamber.Capacity));
        text.AppendLine(Lang.Get("navy1851:description"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot slot) =>
    [
        new() { ActionLangCode = "navy1851:help-fire", MouseButton = EnumMouseButton.Right },
        new() { ActionLangCode = "navy1851:help-reload", MouseButton = EnumMouseButton.Right, HotKeyCode = "sneak" }
    ];
}
