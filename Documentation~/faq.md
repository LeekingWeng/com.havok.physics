# Frequently Asked Questions

#### Does Havok Physics for Unity replace PhysX?
No. This Havok Physics for Unity integration works in tandem with Unity Physics in the DOTS framework, while PhysX continues to be the built-in physics engine used by Unity GameObjects.

#### Can Havok Physics for Unity integration work with GameObjects?
Not directly. As with Unity Physics, they must be converted to ECS entities first.

#### Is Havok Physics for Unity integration exactly the same as what is used in my favorite AAA game?
This integration exposes a fixed subset of the Havok SDK functionality – currently just the simulation backend and the visual debugger.
The core simulation is the same as that used by many AAA games, however the full Havok SDK offers many more features and flexibility, allowing users to further customize physics to their exact use cases.

#### How does Havok Physics for Unity work with networking use cases since it's not stateless?
Havok Physics for Unity is a deterministic but stateful engine.
This implies that a copy of a physics world will not simulate identically to the original world unless all of the internal simulation caches are also copied.
Therefore, for networking use cases that depend on deterministic simulation of a "rolled back" physics world, you should rather use the Unity Physics simulation since it is stateless.

#### How do I get the C++ source for the Havok Physics for Unity integration?
The C++ source is only available to Havok customers with a full SDK license.
For more information, [contact Havok](mailto:hkunitysales@microsoft.com).

#### "HAVOK_PHYSICS_EXISTS is defined but Havok.Physics is missing from your package's asmdef references" – what does this error mean?
This compilation error will occur if you have the simulation type set to "Havok Physics" and implemented an `ITriggerJob` or `IContactsJob` but haven't added the Havok.Physics plugin to your projects assembly definitions (ASMDEF).

#### "Your Havok.Physics plugin is incompatible with this version" - what does this error mean?
This runtime error occurs if the Havok Physics DLL in the Plugins directory does not match the C# codebase.
This may happen from a stale package cache. Try reimporting the Havok Physics for Unity package.

#### Where should I report issues or ask questions?
Please report any feedback using the Unity forum.
Users with a full Havok SDK license should use the private Havok support portal.
