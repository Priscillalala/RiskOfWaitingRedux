# Risk of Waiting
Load the game faster, thanks to some good old-fashioned optimization and caching!

Risk of Waiting makes no meaningful changes to startup functionality, and is intended to be compatible with every mod.

## Cache Files
This mod stores cache files in your Risk of Rain 2 folder, under `/RiskofWaitingReduxData/`. They are tiny files (>1 KB per mod) and always safe to delete.

## Overview
A brief overview of the improvements included in this mod, for fellow mod devs and other interested persons. See the source code at https://github.com/Priscillalala/RiskOfWaitingRedux (MIT licensed)

Process | Problem | Improvement
-|-|-
SearchableAttribute | Uses a lot of reflection to scan types and members for attributes | Cache the search
ConVars | Uses a lot of reflection to scan fields and methods for ConVars | Cache the search

Additionally, the following improvements were originally built for this mod but now live in RoR2BepInExPack! See the pull requests at: 
* https://github.com/risk-of-thunder/RoR2BepInExPack/pull/48 (AchievementManager)
* https://github.com/risk-of-thunder/RoR2BepInExPack/pull/60 (EntityStateCatalog, PostProcessManager, and VolumeManager)

Process | Problem | Improvement
-|-|-
AchievementManager | Waits for the next frame after processing EACH achievement type | Yield each time 100 achievements are registered
AchievementManager | Scans assemblies which will never contain achievements | Only scan assemblies that reference RoR2; exclude MMHOOK assemblies and RoR2BepInExPack
EntityStateCatalog | Waits for the next frame too often while applying entity state configurations | Yield less often
PostProcessManager | Scans EVERY assembly in the domain for post process effects | Only scan assemblies that reference Unity.Postprocessing.Runtime; exclude MMHOOK assemblies
VolumeManager* | Scans EVERY assembly in the domain for VolumeComponent types | Only scan assemblies that reference Unity.RenderPipelines.Core.Runtime; exclude MMHOOK assemblies

*<sub>The VolumeManager does not init during startup; it is lazily initialized by the Winnowing of Mortality scene (`soul1`)</sub>

## Contact
You can find me in the [RoR2 Modding Server](https://discord.gg/5MbXZvd) @groove_salad

Or, you can post issues and feedback on the [GitHub](https://github.com/Priscillalala/RiskOfWaitingRedux/issues)

## Donations
If this mod saved a few minutes of your life, consider [buying me a coffee](https://www.buymeacoffee.com/groovesalad)!

<a href="https://www.buymeacoffee.com/groovesalad" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" height=60 width=217></a>