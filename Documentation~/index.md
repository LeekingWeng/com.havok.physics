# Havok Physics for Unity

Havok Physics offers the fastest, most robust collision detection and physical simulation technology available, which is why it has become the gold standard within the games industry and has been used by more than half of the top selling titles this console generation.

This package brings the power of Havok Physics to Unity's DOTS framework. It builds on top of the [Unity.Physics](https://docs.unity3d.com/Packages/com.unity.physics@latest) package, the C# physics engine written for DOTS by Unity and Havok.

Jump to the [quickstart guide](quickstart.md) to dive right in, or continue reading for more details.

## Features

This package provides a closed source physics simulation backend, using the same Havok Physics engine that powers many industry leading AAA games. This implemention shares the same input and output data formats as Unity.Physics, which means that you can simply swap the simulation backend at any time, without needing to change any of your existing physics assets or code.

Compared to the standard Unity.Physics simulation, Havok.Physics offers:

* Higher **simulation performance** : Havok Physics is a stateful engine, which makes it more performant than Unity.Physics for scenes with significant numbers of rigid bodies, due to automatic sleeping of inactive rigid bodies and other advanced caching techniques (typically 2x or more faster).

* Higher **simulation quality** : Havok Physics is a mature engine which is robust to many use cases. In particular, it offers stable stacking and a solution for smoothing out contact points when rigid bodies slide quickly over each other (known as "welding").

* Deep **profiling and debugging** of physics simulations using the Havok "Visual Debugger" standalone application (available on Windows only).

Note that the simulation behavior will be similiar but not be identical to that of Unity.Physics, so if you have finely tuned your simulation you may need to re-tune it.
You can also opt into or out of additional features specific to this implementation to fine tune it further.

## Contents

* [Licensing model](licensing.md)
* [Supported platforms](platforms.md)
* [Configuring the simulation](configuration.md)
* [Getting started with the Visual Debugger](vdb_quickstart.md)
* [Frequently asked questions](faq.md)
* [Roadmap](roadmap.md)
