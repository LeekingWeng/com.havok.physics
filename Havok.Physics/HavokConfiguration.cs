using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Havok.Physics
{
    // A configuration for HavokSimulation
    [StructLayout(LayoutKind.Sequential)]
    public struct HavokConfiguration : IComponentData
    {
        // The Havok broad phase uses quantized AABBs, which requires an explicit world extent.
        // This AABB should be big enough to encompass all rigid bodies, otherwise collisions will not happen.
        public Unity.Physics.Aabb WorldBounds;

        // Whether sleeping of dynamic rigid bodies is enabled.
        public int EnableSleeping;  // int for PInvoke

        // Configuration for the Havok Visual Debugger.
        public VisualDebuggerConfiguration VisualDebugger;
        
        // A mask of Unity.Physics.RigidBody.CustomTags values which enable contact welding for the body.
        public byte BodyTagsForContactWelding;

        [StructLayout(LayoutKind.Sequential)]
        public struct VisualDebuggerConfiguration
        {
            // Collect profiling information and make this simulation visible to the VDB application.
            public int Enable;  // int for PInvoke

            // The port on which to send the data to the VDB application.
            public int Port;

            // The number of bytes to allocate per thread to collect profiling information.
            public int TimerBytesPerThread;
        }

        public static readonly HavokConfiguration Default = new HavokConfiguration
        {
            WorldBounds = new Unity.Physics.Aabb
            {
                Max = new float3(5000f),
                Min = new float3(-5000f)
            },
            EnableSleeping = 1,
            VisualDebugger = new VisualDebuggerConfiguration
            {
                Enable = 0,
                Port = 25001,
                TimerBytesPerThread = 1024 * 1024
            },
            BodyTagsForContactWelding = 0,
        };
    };
}
