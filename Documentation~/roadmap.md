# Roadmap

This first release is focused on ensuring the core simulation and interoperability with Unity Physics works well.

What to expect in future versions:

* Optimizations to the per-frame synchronization of Havok Physics with the Unity Physics world. This is currently a single threaded job which could become a bottleneck in some use cases. We expect to multithread and optimize it further.

* More options for trading rigid body quality vs performance. In particular we expect to add a super fast, low quality, type of rigid body designed for secondary effects with large numbers of dynamic objects – such as particle or debris systems that need to interact with the physics world.

* More options for trading joint quality vs performance. For example, an option to solve long chains of joints using a dedicated solver.

* Tools for creating physics assets. For example, a convex decomposition tool designed for producing optimized colliders from arbitrary meshes.
