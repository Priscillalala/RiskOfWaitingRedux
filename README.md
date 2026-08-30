# Risk of Waiting
Load the game faster, thanks to some good old-fashioned optimization and caching!

Risk of Waiting makes no meaningful changes to startup functionality, and is intended to be compatible with every mod.

## Cache Files
This mod stores cache files in your Risk of Rain 2 folder, under `/RiskofWaitingReduxData/`. 

## Overview
A brief overview of the improvements included in this mod, for fellow mod devs and other interested persons. For more info, see the source code at https://github.com/Priscillalala/RiskOfWaitingRedux (MIT licensed)

Process | Problem | Improvement
-|-|-
PostProcessManager* | Scans EVERY assembly in the domain for post process effects | Only scan assemblies that reference Unity.Postprocessing.Runtime; exclude MMHOOK assemblies
EntityStateCatalog | Waits for the next frame too often while applying entity state configurations | Yield less often
SearchableAttribute | Uses a lot of reflection to scan types and members for attributes | Cache the search
ConVars | Uses a lot of reflection to scan fields and methods for ConVars | Cache the search

*<sub>This mod also includes a cache system for the PostProcessManager; however, the performance gains are nominal compared to any other improvement</sub>

Additionally, the following AchievementManager improvements were originally built for this mod but got added to RoR2BepInExPack instead. See the PR at https://github.com/risk-of-thunder/RoR2BepInExPack/pull/48

Process | Problem | Improvement
-|-|-
AchievementManager | Waits for the next frame after processing EACH achievement type | Yield each time 100 achievements are registered
AchievementManager | Scans assemblies which will never contain achievements | Only scan assemblies that reference RoR2; exclude MMHOOK assemblies and RoR2BepInExPack

## Contact
You can find me in the [RoR2 Modding Server](https://discord.gg/5MbXZvd) @groove_salad

Or, you can post issues and feedback on the [GitHub](https://github.com/Priscillalala/RiskOfWaitingRedux/issues)

## Donations
If this mod saved a few minutes of your life, consider [buying me a coffee](https://www.buymeacoffee.com/groovesalad)!

<a href="https://www.buymeacoffee.com/groovesalad" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" height=60 width=217></a>