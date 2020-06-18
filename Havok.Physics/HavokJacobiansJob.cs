using Havok.Physics;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Physics
{
    // Interface for jobs that iterate through the list of Jacobians before they are solved
    [JobProducerType(typeof(IHavokJacobiansJobExtensions.JacobiansJobProcess<>))]
    public interface IJacobiansJob : IJacobiansJobBase
    {
    }

    public static class IHavokJacobiansJobExtensions
    {
        // Schedule() implementation for IJacobiansJob when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IJacobiansJobBase
        {
            switch (simulation.Type)
            {
                case SimulationType.UnityPhysics:
                    // Call the scheduling method for Unity.Physics
                    return IJacobiansJobExtensions.ScheduleUnityPhysicsJacobiansJob(jobData, simulation, ref world, inputDeps);

                case SimulationType.HavokPhysics:
                {
                    var data = new JacobiansJobData<T>
                    {
                        UserJobData = jobData,
                        FixedJacobianGrid = ((Havok.Physics.HavokSimulation)simulation).FixedJacobianGrid,
                        MovingJacobianGrid = ((Havok.Physics.HavokSimulation)simulation).MovingJacobianGrid,
                        PluginIndexToLocal = ((Havok.Physics.HavokSimulation)simulation).PluginIndexToLocal,
                        Bodies = world.Bodies,
                        TimeStep = ((Havok.Physics.HavokSimulation)simulation).TimeStep
                    };
                    var parameters = new JobsUtility.JobScheduleParameters(
                        UnsafeUtility.AddressOf(ref data),
                        JacobiansJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                    return JobsUtility.Schedule(ref parameters);
                }

                default:
                    return inputDeps;
            }
        }

        internal unsafe struct JacobiansJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpGrid* FixedJacobianGrid;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpGrid* MovingJacobianGrid;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpIntArray* PluginIndexToLocal;
            // Disable aliasing restriction in case T has a NativeArray of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> Bodies;

            public float TimeStep;
        };

        internal struct JacobiansJobProcess<T> where T : struct, IJacobiansJobBase
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JacobiansJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref JacobiansJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public unsafe static void Execute(ref JacobiansJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                ModifiableJacobianHeader modifiableHeader;
                ModifiableContactJacobian modifiableContact;
                byte* headerBuffer = stackalloc byte[JacobianHeader.CalculateSize(JacobianType.Contact, (JacobianFlags)0xff, 4)]; //<todo.eoin.modifier How to verify correct sizes?
                byte* contactBuffer = stackalloc byte[sizeof(ContactJacobian)];

                Havok.Physics.HpGrid* curGrid = jobData.FixedJacobianGrid;
                int* pluginIndexToLocal = jobData.PluginIndexToLocal->Data;
                for (int g = 0; g < 2; g++)
                {
                    for (int i = 0; i < curGrid->m_size; i++)
                    {
                        HpCsContactJacRange* gridRange = curGrid->m_entries + i;
                        var range = (Havok.Physics.HpLinkedRange*)UnsafeUtility.AddressOf(ref gridRange->m_range);
                        while (range != null)
                        {
                            var reader = new Havok.Physics.HpBlockStreamReader(range);
                            while (reader.HasItems)
                            {
                                var hpHeader = (Havok.Physics.HpJacHeader*)reader.Peek();
                                modifiableHeader = new ModifiableJacobianHeader { };
                                modifiableContact = new ModifiableContactJacobian { };
                                modifiableHeader.m_Header = (JacobianHeader*)headerBuffer;
                                modifiableContact.m_ContactJacobian = (ContactJacobian*)contactBuffer;

                                int bodyIndexA = pluginIndexToLocal[hpHeader->m_bodyIdA & 0x00ffffff];
                                int bodyIndexB = pluginIndexToLocal[hpHeader->m_bodyIdB & 0x00ffffff];

                                modifiableHeader.m_Header->BodyPair = new BodyIndexPair
                                {
                                    BodyIndexA = bodyIndexA,
                                    BodyIndexB = bodyIndexB
                                };

                                modifiableHeader.EntityPair = new EntityPair
                                {
                                    EntityA = jobData.Bodies[bodyIndexA].Entity,
                                    EntityB = jobData.Bodies[bodyIndexB].Entity
                                };

                                modifiableHeader.m_Header->Type = JacobianType.Contact;
                                modifiableContact.m_ContactJacobian->BaseJacobian.NumContacts = hpHeader->m_numPoints;
                                modifiableContact.m_ContactJacobian->BaseJacobian.Normal = hpHeader->m_normal.xyz;
                                Havok.Physics.HPManifoldCollisionCache* manifoldCache = hpHeader->m_manifoldCollisionCache;
                                modifiableContact.m_ContactJacobian->CoefficientOfFriction = manifoldCache->m_friction.Value;

                                // Fill in friction data
                                if (HpJacHeader.hasAnyFriction(hpHeader->m_flagsAndDimB))
                                {
                                    Havok.Physics.HpJac3dFriction* jf = hpHeader->accessJacFriction();
                                    modifiableContact.m_ContactJacobian->Friction0.AngularA = jf->m_jacDir0_angular0.xyz;
                                    modifiableContact.m_ContactJacobian->Friction0.AngularB = jf->m_jacDir0_angular1.xyz;
                                    modifiableContact.m_ContactJacobian->Friction1.AngularA = jf->m_jacDir1_angular0.xyz;
                                    modifiableContact.m_ContactJacobian->Friction1.AngularB = jf->m_jacDir1_angular1.xyz;
                                    modifiableContact.m_ContactJacobian->AngularFriction.AngularA = jf->m_jacAng_angular0.xyz;
                                    modifiableContact.m_ContactJacobian->AngularFriction.AngularB = jf->m_jacAng_angular1.xyz;
                                }

                                Havok.Physics.HpPerManifoldProperty* cdp = manifoldCache->GetCustomPropertyStorage();
                                modifiableHeader.m_Header->Flags = (JacobianFlags)cdp->m_jacobianFlags;

                                if ((cdp->m_jacobianFlags & (byte)JacobianFlags.EnableMassFactors) != 0)
                                {
                                    modifiableHeader.MassFactors = *hpHeader->accessMassFactors();
                                }

                                for (int p = 0; p < hpHeader->m_numPoints; p++)
                                {
                                    Havok.Physics.HpJacAngular* hpAng = hpHeader->accessJacAngular(p);
                                    var ang = new ContactJacAngAndVelToReachCp
                                    {
                                        Jac = new ContactJacobianAngular
                                        {
                                            AngularA = hpAng->m_angular0.xyz,
                                            AngularB = hpAng->m_angular1.xyz,
                                            EffectiveMass = hpAng->m_angular0.w,
                                        },
                                        VelToReachCp = hpAng->m_angular1.w,
                                    };

                                    // Access the angular jacobian from the header directly,
                                    // to avoid the modifiable header marking itself dirty.
                                    modifiableHeader.m_Header->AccessAngularJacobian(p) = ang;
                                }

                                jobData.UserJobData.Execute(ref modifiableHeader, ref modifiableContact);

                                if (((byte)modifiableHeader.Flags & (byte)JacobianFlags.Disabled) != 0)
                                {
                                    // Don't check the "changed" state of the jacobian - this flag is set on the contact
                                    hpHeader->m_flagsAndDimB |= 1 << 10; // JH_MANIFOLD_IS_NOT_NORMAL
                                    hpHeader->m_manifoldType = 3; // hknpManifoldType::DISABLED
                                }

                                if (modifiableHeader.AngularChanged || modifiableHeader.ModifiersChanged)
                                {
                                    // Need to disable jacobian caching, since we can't tell what modifications the user has done
                                    manifoldCache->m_qualityFlags &= (0xffff ^ (1 << 10)); //hknpBodyQuality::ENABLE_CONTACT_CACHING
                                }

                                if (modifiableHeader.AngularChanged)
                                {
                                    for (int p = 0; p < hpHeader->m_numPoints; p++)
                                    {
                                        Havok.Physics.HpJacAngular* hpAng = hpHeader->accessJacAngular(p);
                                        ContactJacAngAndVelToReachCp ang = modifiableHeader.GetAngularJacobian(p);

                                        hpAng->m_angular0 = new float4(ang.Jac.AngularA, ang.Jac.EffectiveMass);
                                        hpAng->m_angular1 = new float4(ang.Jac.AngularB, ang.VelToReachCp);
                                    }
                                }

                                if (modifiableHeader.ModifiersChanged && (cdp->m_jacobianFlags & (byte)JacobianFlags.EnableMassFactors) != 0)
                                {
                                    *hpHeader->accessMassFactors() = modifiableHeader.MassFactors;
                                }

                                if (modifiableHeader.ModifiersChanged && (cdp->m_jacobianFlags & (byte)JacobianFlags.EnableSurfaceVelocity) != 0)
                                {
                                    var surfVel = modifiableHeader.SurfaceVelocity;

                                    float angVelProj = math.dot(surfVel.AngularVelocity, modifiableContact.Normal);
                                    if (manifoldCache != null)
                                    {
                                        float frictionRhs = manifoldCache->getFrictionRhsMultiplierValue();
                                        float frictRhsMul = frictionRhs * jobData.TimeStep;

                                        // Update cached integrated friction rhs
                                        float4 vel = new float4(-surfVel.LinearVelocity, -angVelProj);
                                        float4 rhs4 = manifoldCache->m_integratedFrictionRhs;
                                        rhs4 += frictRhsMul * vel;
                                        manifoldCache->m_integratedFrictionRhs = rhs4;
                                    }

                                    if (HpJacHeader.hasAnyFriction(hpHeader->m_flagsAndDimB))
                                    {
                                        Math.CalculatePerpendicularNormalized(modifiableContact.Normal, out float3 dir0, out float3 dir1);
                                        float linVel0 = math.dot(surfVel.LinearVelocity, dir0);
                                        float linVel1 = math.dot(surfVel.LinearVelocity, dir1);

                                        // Check JH_SURFACE_VELOCITY_DIRTY flag and clear it if it was set
                                        const ushort jhSurfaceVelocityDirty = 1 << 3;
                                        if ((hpHeader->m_flagsAndDimB & jhSurfaceVelocityDirty) != 0)
                                        {
                                            *hpHeader->accessSurfaceVelocity() = new float3(linVel0, linVel1, angVelProj);
                                            hpHeader->m_flagsAndDimB &= 0xffff ^ jhSurfaceVelocityDirty;
                                        }
                                        else
                                        {
                                            *hpHeader->accessSurfaceVelocity() += new float3(linVel0, linVel1, angVelProj);
                                        }

                                        // Update friction Jacobian
                                        {
                                            Havok.Physics.HpJac3dFriction* jf = hpHeader->accessJacFriction();

                                            float dRhs0 = jf->m_jacDir0_linear0.w;
                                            float dRhs1 = jf->m_jacDir1_linear0.w;
                                            float dRhs = jf->m_jacAng_angular1.w;

                                            float frictRhsMul = hpHeader->m_manifoldCollisionCache->getFrictionRhsMultiplierValue();
                                            dRhs0 -= frictRhsMul * linVel0;
                                            dRhs1 -= frictRhsMul * linVel1;
                                            dRhs -= frictRhsMul * angVelProj;

                                            jf->m_jacDir0_linear0.w = dRhs0;
                                            jf->m_jacDir1_linear0.w = dRhs1;
                                            jf->m_jacAng_angular1.w = dRhs;
                                        }
                                    }
                                }

                                if (modifiableContact.Modified)
                                {
                                    hpHeader->m_normal.xyz = modifiableContact.Normal;
                                    manifoldCache->m_friction.Value = modifiableContact.CoefficientOfFriction;

                                    if (HpJacHeader.hasAnyFriction(hpHeader->m_flagsAndDimB))
                                    {
                                        Havok.Physics.HpJac3dFriction* jf = hpHeader->accessJacFriction();
                                        jf->m_jacDir0_angular0.xyz = modifiableContact.Friction0.AngularA;
                                        jf->m_jacDir0_angular1.xyz = modifiableContact.Friction0.AngularB;
                                        jf->m_jacDir1_angular0.xyz = modifiableContact.Friction1.AngularA;
                                        jf->m_jacDir1_angular1.xyz = modifiableContact.Friction1.AngularB;
                                        jf->m_jacAng_angular0.xyz = modifiableContact.AngularFriction.AngularA;
                                        jf->m_jacAng_angular1.xyz = modifiableContact.AngularFriction.AngularB;
                                    }
                                }

                                reader.Advance(hpHeader->m_sizeDiv16 * 16);
                            }

                            range = range->m_next;
                        }
                    }

                    curGrid = jobData.MovingJacobianGrid;
                }
            }
        }
    }
}
