using Vintagestory.API.Client;

namespace Navy1851;

public static class WeaponPose
{
    // The engine uses HandTp for first-person hands too. Camera offsets must not
    // reach the third-person body or either shadow pass.
    public static bool UseViewTransform(EnumCameraMode camera, EnumRenderStage stage)
        => camera == EnumCameraMode.FirstPerson &&
           stage is not (EnumRenderStage.ShadowNear or EnumRenderStage.ShadowFar);
}
