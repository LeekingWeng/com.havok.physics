using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    public static class IHavokTriggerEventsJobExtensions
    {
        // ITriggerEventsJob.Schedule() implementation for when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, ITriggerEventsJob
        {
            if (simulation.Type == SimulationType.UnityPhysics)
            {
                return ITriggerEventJobExtensions.ScheduleImpl(jobData, simulation, ref world, inputDeps);
            }
            else if (simulation.Type == SimulationType.HavokPhysics)
            {
                var data = new TriggerEventJobData<T>
                {
                    UserJobData = jobData,
                    EventStream = ((Havok.Physics.HavokSimulation)simulation).TriggerEventStream,
                    Bodies = world.Bodies
                };

                // Ensure the input dependencies include the end-of-simulation job, so events will have been generated
                inputDeps = JobHandle.CombineDependencies(inputDeps, simulation.FinalSimulationJobHandle);

                var parameters = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref data),
                    TriggerEventJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                return JobsUtility.Schedule(ref parameters);
            }
            return inputDeps;
        }

        private unsafe struct TriggerEventJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpBlockStream* EventStream;
            // Disable aliasing restriction in case T has a NativeSlice of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeSlice<RigidBody> Bodies;
        }

        private struct TriggerEventJobProcess<T> where T : struct, ITriggerEventsJob
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(TriggerEventJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref TriggerEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex);

            public unsafe static void Execute(ref TriggerEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex)
            {
                var reader = new Havok.Physics.HpBlockStreamReader(jobData.EventStream);
                while (reader.HasItems)
                {
                    var eventData = (LowLevel.TriggerEvent*)reader.Peek();

                    jobData.UserJobData.Execute(new TriggerEvent
                    {
                        EventData = *eventData,
                        Entities = new EntityPair
                        {
                            EntityA = jobData.Bodies[eventData->BodyIndices.BodyAIndex].Entity,
                            EntityB = jobData.Bodies[eventData->BodyIndices.BodyBIndex].Entity
                        }
                    });

                    reader.Advance(sizeof(LowLevel.TriggerEvent));
                }
            }
        }
    }
}
