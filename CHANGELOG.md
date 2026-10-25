# Changelog

## 2.1.0

Some things are now part of RoR2BepInExPack, yay! You can safely delete the PostProcessingCache data at `/Risk of Rain 2/RiskOfWaitingReduxData/PostProcessingCache` since it will no longer be used.

### Removed

- Remove PostProcessManager fix, it has moved to RoR2BepInExPack
- Remove PostProcessManager cache
- Remove EntityStateCatalog fix, it has also moved to RoR2BepInExPack!

## 2.0.2

### Fixed

- Increase the severity of some `SearchableAttributeCache` log messages from debug to error, and adjust log messages

## 2.0.1

### Fixed

- Reduce the severity of some log messages from error to info
- Fix an incompatibility with UnityExplorer caused by some duplicate assembly nonsense 

## 2.0.0

Completely rewrote the mod with a focus on tangible optimizations and caching; it is much faster now!

### Added

- Optimize PostProcessManager initialization
- Implement PostProcessManager cache
- Implement SearchableAttribute cache
- Implement ConVar cache
- Optimize EntityStateCatalog initialization

### Removed

- Remove everything from version 1.0.0

## 1.0.0

Initial release