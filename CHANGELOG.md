# Changelog
All notable changes to this project will be documented in this file.

## [0.2.1-preview] - 2020-03-19

### Fixed
- Changing motion type of many bodies in a single frame no longer results in a crash.
- Trigger events are now consistently raised for penetrating bodies.
- Asking for collision/trigger events in scenes with no dynamic bodies no longer throws errors.
- Android ARM64 and ARMv7 can now be built together without any special actions. Android-specific instructions are removed from [Supported platforms](Documentation~/platforms.md).

## [0.2.0-preview] - 2020-03-12

### Changed
- `HavokSimulation.ScheduleStepJobs()` now takes `SimulationCallbacks` and `threadCountHint` separately as input arguments.
- `Simulation.CollisionEvents` and `Simulation.TriggerEvents` can now be used to iterate through the events directly using a `foreach` loop, rather than only via `ICollisionEventsJob` and `ITriggerEventsJob`.

### Fixed
- Android ARM64 platform is now supported. Please see [Supported platforms](Documentation~/platforms.md) for details.
- Jobs implementing ICollisionEventsJob, ITriggerEventsJob, IContactsJob, IBodyPairsJob or IJacobiansJob can now be Burst-compiled in both editor and standalone player.
- Duplicate jobs for debug display (like FinishDisplayCollisionEventsJob) are no longer being scheduled, since only HavokPhysics variant of their systems (DisplayCollisionEventsSystem) is now running and covers both HavokPhysics and UnityPhysics simulation.

## [0.1.2-preview] - 2020-01-10

### Changed
- Unity Pro users now require a subscription, which is available in the [Asset Store](https://aka.ms/hkunityassetstore).

### Fixed
- Reduced the sync tolerance. Now handling smaller deltas in position, rotation and velocity.
- Fixed the issue of uninitialized array when scheduling collision event jobs with no dynamic bodies in the scene.
- Fixed an issue where contacts were not being correctly disabled in an IContactsJob.
- The Havok Visual Debugger (VDB) is now always stepped, even when there are no dynamic bodies in the scene.
- Fixed the job handle ordering during step. This fixes errors when simulation callbacks were added.
- Fixed the VDB initialization to use the supplied port.

## [0.1.1-preview] - 2019-09-20

- First public release

### Changed
- The plugins are now free to use for everyone until January 15th 2020

### Fixed
- Fixed potential IndexOutOfRangeException when executing IBodyPairsJob, IContactsJob or IJacobiansJob

## [0.1.0-preview.2] - 2019-09-05

### Fixed
- Bodies tagged for contact welding now actually get welding applied

## [0.1.0-preview.1] - 2019-08-29

- First pre-release package version
