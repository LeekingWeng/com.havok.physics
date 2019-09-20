
## Frequently Asked Questions

#### Does this replace PhysX?
No. This Havok Physics integration works in tandem with Unity.Physics in the DOTS framework, while PhysX continues to be the built-in physics engine used by Unity GameObjects.

#### Can Havok Physics integration work with GameObjects?
Not directly. As with Unity.Physics, they must be converted to ECS entities first.

#### Is Havok Physics integration for Unity exactly the same as what is used in (insert favorite AAA game)?
This integration exposes a fixed subset of the Havok SDK functionality - currently just the simulation backend and the visual debugger.
The core simulation is the same as that used by many AAA games, however the full Havok SDK offers many more features and flexibility, allowing users to further customize physics to their exact use cases.

#### How does Havok Physics work with networking use cases since it's not stateless?
Havok Physics is a deterministic but stateful engine.
This implies that a copy of a physics world will not simulate identically to the original world unless all of the internal simulation caches are also copied.
Therefore for networking use cases that depend on deterministic simulation of a "rolled back" physics world, we currently recommend using the Unity.Physics simulation since it is stateless.

#### How do I get the C++ source for the Havok Physics integration?
The C++ source is available only to Havok customers with a full SDK license.
Please contact sales@havok.com for information.

#### "HAVOK_PHYSICS_EXISTS is defined but Havok.Physics is missing from your package's asmdef references" - what does this error mean?
This compile error will occur if you have the simulation type set to "Havok Physics" and implemented an `ITriggerJob` or `IContactsJob` but have not added the Havok.Physics plugin to your projects assembly definitions (ASMDEF).

#### "Your Havok.Physics plugin is incompatible with this version" - what does this error mean?
This runtime error occurs if the Havok Physics DLL in the Plugins directory does not match the C# codebase.
This may happen from a stale package cache. Try reimporting the Havok.Physics package.

#### Where should I report issues or ask questions?
Please report any feedback using the Unity forum.
Users with a full Havok SDK license should use the private Havok support portal.
