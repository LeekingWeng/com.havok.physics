using System;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics.Tests.Dynamics.PhysicsWorld
{
    class PhysicsWorldTests
    {
        [Test]
        public void WorldTest()
        {
            var world = new Physics.PhysicsWorld(1, 1, 0);
            Assert.IsTrue((world.NumStaticBodies == 1) && (world.NumDynamicBodies == 1) && (world.NumBodies == 2));

            // TODO: add test for dynamics

            world.Dispose();
        }
    }
}
