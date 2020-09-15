# Changelog
All notable changes to this project will be documented in this file.

## [0.4.0-preview.1] - 2020-09-15

### Changed
- Updated minimum Unity Editor version from `2019.4.0f1` to `2020.1.0f1`
- [UNI-233] Havok plugin is now based on 2020.1 version of Havok SDK
- [UNI-29] Added `HavokSimulation.SetStaticBodiesChangedFlag(NativeArray<int>)`. This sets the `HaveStaticBodiesChanged` in SimulationContext. This should only be used when `HavokSimulation` is created manually, as it is happening by default when the simulation is created in a standard way, using BuildPhysicsWorld and StepPhysicsWorld.
- [UNI-29] Added `HavokSimulation.SimulationContext.HaveStaticBodiesChanged`. This member can be used to speed up static body synchronization on C++ side. It is a `NativeArray<int>` of size 1, and if `HaveStaticBodiesChanged[0] == 1`, static body sync will happen, otherwise it won't. This should only be used when using `HavokSimulation.StepImmediate()`, use HavokSimulation.SetStaticBodiesChangedFlag() otherwise.

### Fixed
- [UNI-29] Improved performance of Havok Physics synchronization (static bodies are no longer needlessly synced every frame)
- [UNI-232] Reduced size of compound collider/mesh collider AABB in cases where their bodies are rotated by some angle.
- [UNI-241] Fixed a potential issue where events were skipped when happening in multiple parts of the scene, where each part contains both dynamic-dynamic events and dynamic-static events.
- [UNI-247] Fixed no activation in reaction to filter change in the same frame the body gets removed.

## [0.3.1-preview] - 2020-07-28

### Changed
- All systems now inherit `SystemBase` instead of `ComponentSystem`.

### Fixed
- Fixed a crash when `TerrainCollider` uses `CollisionMethod.Triangles`.
- Fixed a bug where Havok Physics plugin did not gracefully handle invalid Joints (e.g. when both Entities are Null).
- Fixed an editor crash when maximum level of composite collider nesting is breached.

## [0.3.0-preview.1] - 2020-06-18

### Changed
- Updated minimum Unity Editor version from `2019.3.0f1` to `2019.4.0f1`
- Updated Unity Physics from `0.3.0-preview` to `0.4.0-preview.4`
- Removed expired API `HavokSimulation.ScheduleStepJobs()` signature without callbacks and thread count hint.

### Fixed
- Fixed a crash when number of solver iterations is bigger than 255.
- Fixed incorrect contact point positions reported through collision events.
- Fixed behavior of joints to properly incorporate provided `Constraint.SpringDamping` and `Constraint.SpringFrequency`.

## [0.2.2-preview] - 2020-04-16

### Changed
- `HavokSimulation.StepImmediate()` is now provided and can be wrapped in a single Burst compiled job for lightweight stepping logic; however, it doesn't support callbacks; if callbacks are needed, one should implement the physics step using a set of function calls that represent different phases of the physics engine and add customization logic in between these calls; this should all be wrapped in a Burst compiled job; check `HavokSimulation.StepImmediate()` for the full list of functions that need to be called

### Fixed
- Fixed a crash that would occur when adding a static body, while the AABB of an existing dynamic body contains world origin (0, 0, 0).

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
