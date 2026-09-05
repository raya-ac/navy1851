# changelog

## 0.1.1

Holding right mouse now fires repeatedly after the 0.35-second cocking delay, with 0.7 seconds between shots. Releasing or switching slots stops the cycle. Empty cylinders remain stopped until the player releases and starts a new action. All ammunition and damage changes remain server-authoritative.

State-machine regression checks cover six shots, no seventh shot, interrupted firing, cooldown, and no catch-up burst on a delayed tick. In-game client acceptance remains unverified.
