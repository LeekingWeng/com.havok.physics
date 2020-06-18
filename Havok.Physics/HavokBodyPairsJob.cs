using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    // Interface for jobs that iterate through the list of potentially overlapping body pairs produced by the broad phase
    [JobProducerType(typeof(IHavokBodyPairsJobExtensions.BodyPairsJobProcess<>))]
    public interface IBodyPairsJob : IBodyPairsJobBase
    {
    }

    public static class IHavokBodyPairsJobExtensions
    {
        // Schedule() implementation for IBodyPairsJob when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IBodyPairsJobBase
        {
            switch (simulation.Type)
            {
                case SimulationType.UnityPhysics:
                    // Call the scheduling method for Unity.Physics
                    return IBodyPairsJobExtensions.ScheduleUnityPhysicsBodyPairsJob(jobData, simulation, ref world, inputDeps);

                case SimulationType.HavokPhysics:
                {
                    var data = new BodyPairsJobData<T>
                    {
                        UserJobData = jobData,
                        BlockStreamStart = ((Havok.Physics.HavokSimulation)simulation).NewBodyPairsStream,
                        PluginIndexToLocal = ((Havok.Physics.HavokSimulation)simulation).PluginIndexToLocal,
                        Bodies = world.Bodies
                    };
                    var parameters = new JobsUtility.JobScheduleParameters(
                        UnsafeUtility.AddressOf(ref data),
                        BodyPairsJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                    return JobsUtility.Schedule(ref parameters);
                }

                default:
                    return inputDeps;
            }
        }

        internal unsafe struct BodyPairsJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpBlockStream* BlockStreamStart;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpIntArray* PluginIndexToLocal;
            // Disable aliasing restriction in case T has a NativeArray of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> Bodies;
        }

        internal struct BodyPairsJobProcess<T> where T : struct, IBodyPairsJobBase
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(BodyPairsJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref BodyPairsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref BodyPairsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                if (jobData.BlockStreamStart == null || !jobData.BlockStreamStart->HasElements)
                {
                    return;
                }

                var blockStreamReader = new Havok.Physics.HpBlockStreamReader(jobData.BlockStreamStart);
                int* pluginIndexToLocal = jobData.PluginIndexToLocal->Data;
                while (blockStreamReader.HasItems)
                {
                    BodyIndexPair indices = blockStreamReader.Read<BodyIndexPair>(); // Really an hknpBodyIdPair
                    int bodyIndexA = pluginIndexToLocal[indices.BodyIndexA & 0x00ffffff];
                    int bodyIndexB = pluginIndexToLocal[indices.BodyIndexB & 0x00ffffff];

                    var pair = new ModifiableBodyPair
                    {
                        BodyIndexPair = new BodyIndexPair { BodyIndexA = bodyIndexA, BodyIndexB = bodyIndexB },
                        EntityPair = new EntityPair
                        {
                            EntityA = jobData.Bodies[bodyIndexA].Entity,
                            EntityB = jobData.Bodies[bodyIndexB].Entity
                        }
                    };
                    jobData.UserJobData.Execute(ref pair);

                    if (pair.BodyIndexA == -1 || pair.BodyIndexB == -1)
                    {
                        blockStreamReader.Write(BodyIndexPair.Invalid);
                    }
                }
            }
        }
    }
}
