using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    public static class IHavokCollisionEventsJobExtensions
    {
        // ICollisionEventsJob.Schedule() implementation for when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, ICollisionEventsJob
        {
            if (simulation.Type == SimulationType.UnityPhysics)
            {
                return ICollisionEventJobExtensions.ScheduleImpl(jobData, simulation, ref world, inputDeps);
            }
            else if (simulation.Type == SimulationType.HavokPhysics)
            {
                var data = new CollisionEventJobData<T>
                {
                    UserJobData = jobData,
                    EventStream = ((Havok.Physics.HavokSimulation)simulation).CollisionEventStream,
                    Bodies = world.Bodies,
                    TimeStep = ((Havok.Physics.HavokSimulation)simulation).TimeStep,
                    InputVelocities = ((Havok.Physics.HavokSimulation)simulation).InputVelocities
                };

                // Ensure the input dependencies include the end-of-simulation job, so events will have been generated
                inputDeps = JobHandle.CombineDependencies(inputDeps, simulation.FinalSimulationJobHandle);

                var parameters = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref data),
                    CollisionEventJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                return JobsUtility.Schedule(ref parameters);
            }
            return inputDeps;
        }

        private unsafe struct CollisionEventJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpBlockStream* EventStream;
            // Disable aliasing restriction in case T has a NativeSlice of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeSlice<RigidBody> Bodies;
            [ReadOnly] public float TimeStep;
            [ReadOnly] public NativeSlice<Velocity> InputVelocities;
        }

        private struct CollisionEventJobProcess<T> where T : struct, ICollisionEventsJob
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(CollisionEventJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref CollisionEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex);

            public unsafe static void Execute(ref CollisionEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex)
            {
                var reader = new Havok.Physics.HpBlockStreamReader(jobData.EventStream);
                while (reader.HasItems)
                {
                    // Read the size first
                    int size = reader.Read<int>();

                    // Peek the event data
                    var eventData = (LowLevel.CollisionEvent*)reader.Peek();

                    int numNarrowPhaseContactPoints = eventData->NumNarrowPhaseContactPoints;
                    var narrowPhaseContactPoints = new NativeArray<ContactPoint>(numNarrowPhaseContactPoints, Allocator.Temp);
                    for (int i = 0; i < numNarrowPhaseContactPoints; i++)
                    {
                        narrowPhaseContactPoints[i] = eventData->AccessContactPoint(i);
                    }

                    int bodyAIndex = eventData->BodyIndices.BodyAIndex;
                    int bodyBIndex = eventData->BodyIndices.BodyBIndex;
                    jobData.UserJobData.Execute(new CollisionEvent
                    {
                        EventData = *eventData,
                        Entities = new EntityPair
                        {
                            EntityA = jobData.Bodies[bodyAIndex].Entity,
                            EntityB = jobData.Bodies[bodyBIndex].Entity
                        },
                        TimeStep = jobData.TimeStep,
                        InputVelocityA = bodyAIndex < jobData.InputVelocities.Length ? jobData.InputVelocities[bodyAIndex] : Velocity.Zero,
                        InputVelocityB = bodyBIndex < jobData.InputVelocities.Length ? jobData.InputVelocities[bodyBIndex] : Velocity.Zero,
                        NarrowPhaseContactPoints = narrowPhaseContactPoints
                    });

                    reader.Advance(size);
                }
            }
        }
    }
}
