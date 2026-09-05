using Vintagestory.API.Common;

namespace Navy1851;

public sealed class NavySystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass("Navy1851Revolver", typeof(ItemNavy));
    }
}
