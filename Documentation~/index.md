# About Havok Physics for Unity

Use the _Havok Physics for Unity_ package to benefit from the Havok Physics engine within Unity.

> Havok Physics offers the fastest, most robust collision detection and physical simulation technology available, which is why it has become the gold standard within the games industry and has been used by more than half of the top selling titles this console generation.

This package brings the power of Havok Physics to Unity's DOTS framework. It builds on top of [Unity Physics](https://docs.unity3d.com/Packages/com.unity.physics@latest), the C# physics engine written for DOTS by Unity and Havok.

## Preview package

This package is marked as "preview" as it's dependent on the Unity Physics package (which is currently in preview).

## Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Manual/upm-ui-install.html).

## Requirements

This version of Havok Physics for Unity is compatible with the following versions of the Unity Editor:
* 2019.3 and later (recommended)

> **Note:** the use of Havok Physics for Unity is subject to a specific [licensing model](licensing.md).

## Features

This package provides a closed source physics simulation backend, using the same Havok Physics engine that powers many industry leading AAA games. This implementation shares the same input and output data formats as Unity Physics, which means that you can simply swap the simulation backend at any time, without needing to change any of your existing physics assets or code.

Compared to Unity Physics simulation, Havok Physics for Unity offers:

* Better **simulation performance**: Havok Physics for Unity is a stateful engine, which means simulation time is over two times faster than Unity Physics in scenes that have a significant number of rigid bodies. This is due to automatic sleeping of inactive rigid bodies and other advanced caching techniques.
* Higher **simulation quality** : Havok Physics for Unity is a mature engine which is robust to many use cases. In particular, it offers welding, a feature that allows for stable stacking and for smoothing out contact points when rigid bodies slide quickly over each other.
* Deep **profiling and debugging** of physics simulations using the Havok Visual Debugger (only available on Windows). This industry leading tool can help you identify fine-grained, real-time multithreaded performance data that shows exactly where cycles are spent across all cores of the target system.

> **Note:** Simulation behavior between Havok Physics for Unity and Unity Physics is similar but not identical. If you have finely tuned your simulations using Unity Physics, you may need to re-tune for Havok Physics for Unity. You can also opt into or out of additional features specific to this implementation to customize your specific physics needs even further.

## Support

* Havok Physics for Unity relies on community-driven support through our [DOTS Physics forum](https://forum.unity.com/forums/dots-physics.422/).
* Customers who have Unity support can continue to rely on Unity's support services for any questions relating to Havok Physics for Unity.
* To get support directly from Havok's developer support team, you need a [Havok Physics SDK license](#havok-physics-sdk-license).

## Havok Physics SDK License

Havok Physics for Unity is a binary-only distribution of Havok Physics (2019.2.0) that has the same industry-standard power but without access to C++ source code (known as "Base and Product" access) or direct support from Havok.

If your project requires either support from Havok or C++ source access, [contact Havok](mailto:hkunitysales@microsoft.com).
