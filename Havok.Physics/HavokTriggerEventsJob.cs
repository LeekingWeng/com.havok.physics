using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    // Interface for jobs that iterate through the list of trigger events produced by the solver.
    [JobProducerType(typeof(IHavokTriggerEventsJobExtensions.TriggerEventJobProcess<>))]
    public interface ITriggerEventsJob : ITriggerEventsJobBase
    {
    }

    public static class IHavokTriggerEventsJobExtensions
    {
        // Schedule() implementation for ITriggerEventsJob when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, ITriggerEventsJob
        {
            switch (simulation.Type)
            {
                case SimulationType.UnityPhysics:
                    // Call the scheduling method for Unity.Physics
                    return ITriggerEventJobExtensions.ScheduleUnityPhysicsTriggerEventsJob(jobData, simulation, ref world, inputDeps);

                case SimulationType.HavokPhysics:
                {
                    var data = new TriggerEventJobData<T>
                    {
                        UserJobData = jobData,
                        EventReader = ((Havok.Physics.HavokSimulation)simulation).TriggerEvents
                    };

                    // Ensure the input dependencies include the end-of-simulation job, so events will have been generated
                    inputDeps = JobHandle.CombineDependencies(inputDeps, simulation.FinalSimulationJobHandle);

                    var parameters = new JobsUtility.JobScheduleParameters(
                        UnsafeUtility.AddressOf(ref data),
                        TriggerEventJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                    return JobsUtility.Schedule(ref parameters);
                }

                default:
                    return inputDeps;
            }
        }

        internal unsafe struct TriggerEventJobData<T> where T : struct
        {
            public T UserJobData;
            public HavokTriggerEvents EventReader;
        }

        internal struct TriggerEventJobProcess<T> where T : struct, ITriggerEventsJobBase
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
                foreach (var triggerEvent in jobData.EventReader)
                {
                    jobData.UserJobData.Execute(triggerEvent);
                }
            }
        }
    }
}
