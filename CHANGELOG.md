# changelog

## 0.1.3 — weapon hold and aiming

corrected the grip mount and orientation using the vanilla falx reference, and removed the two-handed carry pose. right mouse now aims down sights; hold left mouse to fire. aimed shots settle over 0.4 seconds, while hip-fire stays less accurate. sneak + right mouse still reloads. removed the generic interaction and melee animations that twisted the gun away while aiming or firing, and raised the resting pose. aligned the sight pose in the Windows 1.22.3 client, reduced the upward kick, and removed the generic ready animation. reloading fills all missing chambers in one action, consuming only the available charges needed. this remains a prerelease while gameplay and multiplayer testing continues.

## 0.1.2 — vintage story 1.22.3

a separate compatibility build for vintage story 1.22.3, compiled and checked against its own official API assembly. same gun, six-round continuous fire and reloading. replace any older navy1851 zip before installing this one.

## 0.1.1

Holding right mouse now fires repeatedly after the 0.35-second cocking delay, with 0.7 seconds between shots. Releasing or switching slots stops the cycle. Empty cylinders remain stopped until the player releases and starts a new action. All ammunition and damage changes remain server-authoritative.

State-machine regression checks cover six shots, no seventh shot, interrupted firing, cooldown, and no catch-up burst on a delayed tick. In-game client acceptance remains unverified.
