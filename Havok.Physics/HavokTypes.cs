// This file contains mirror structures for Havok SDK structures that are used in Havok Physics code.

#if UNITY_64 || UNITY_EDITOR_64 || UNITY_PS4 || UNITY_SWITCH || UNITY_XBOXONE || UNITY_IOS || UNITY_ANDROID
#define HK_IS_64BIT
#endif

#if HK_ANDROID_32
// This is a custom define that tells us we're building Android ARMv7 (ARM64 is assumed for Android otherwise)
#undef HK_IS_64BIT
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_XBOXONE
// MSVC compiler cannot use the padding from base class to put derived class members
#define HK_NEEDS_EXTRA_STRUCT_PACKING
#endif
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace Havok.Physics
{
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpIntArray
    {
        public int* Data;
        private int m_Size;
        private int m_Flags;
    }

    // A wrapper around Havok block streams (owned by the C++ plugin)
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpBlockStream
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Block
        {
#pragma warning disable CS0649, CS0169
            private uint m_NumElementsAndBytesUsed;
            private int m_BlockIndexInStream;
            private Block* m_NextBlock;
            private IntPtr m_AllocatorDebug;
            private IntPtr m_BlockStreamDebug;

#if !HK_IS_64BIT
            private fixed byte m_padding32[12];
#endif

            public fixed byte Data[1];
#pragma warning restore CS0169, CS0649

            public int NumElements => (int)(m_NumElementsAndBytesUsed & 0xffff);
            public Block* Next => m_NextBlock;
            public byte* Start()
            {
                fixed (byte* d = Data)
                {
                    return d;
                }
            }
        }

#pragma warning disable CS0649, CS0169
        IntPtr m_Allocator;
        internal int m_NumTotalElements;
        byte m_PartiallyFreed;
        byte m_IsLocked;

        // m_Data needs to be 16B-aligned
#if HK_IS_64BIT
        fixed byte m_Padding16[2];
#else
        fixed byte m_Padding16[6];
#endif

        Block** m_Data;
#pragma warning restore CS0649, CS0169

        public bool HasElements => m_NumTotalElements != 0;
        public Block* FirstBlock()
        {
            return m_Data[0];
        }
    }

    // A reader for Havok block streams
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpBlockStreamReader
    {
        [NativeDisableUnsafePtrRestriction]
        HpBlockStream.Block* m_CurrentBlock;
        [NativeDisableUnsafePtrRestriction]
        byte* m_CurrentPtr;
        [NativeDisableUnsafePtrRestriction]
        byte* m_LastPtr;

        int m_NumElementsLeftInCurrentBlock;
        int m_NumElementsLeft;

        public bool IsCreated => m_CurrentBlock != null || m_LastPtr != null;
        public bool HasItems => m_NumElementsLeft != 0;

        public HpBlockStreamReader(HpBlockStream* stream)
        {
            m_CurrentBlock = stream->FirstBlock();
            m_LastPtr = null;
            if (m_CurrentBlock != null)
            {
                m_NumElementsLeftInCurrentBlock = m_CurrentBlock->NumElements;
                m_CurrentPtr = m_CurrentBlock->Start();
            }
            else
            {
                m_NumElementsLeftInCurrentBlock = 0;
                m_CurrentPtr = null;
            }

            m_NumElementsLeft = stream->m_NumTotalElements;
        }

        internal HpBlockStreamReader(HpLinkedRange* range)
        {
            m_CurrentBlock = range->m_block;
            if (m_CurrentBlock != null)
            {
                m_CurrentPtr = m_CurrentBlock->Data + range->m_startByteOffset;
            }
            else
            {
                m_CurrentPtr = null;
            }
            m_NumElementsLeftInCurrentBlock = range->m_startBlockNumElements;
            m_NumElementsLeft = range->m_numElements;
            m_LastPtr = null;
        }

        public ref T Read<T>() where T : struct
        {
            return ref UnsafeUtilityEx.AsRef<T>(ReadPtr<T>());
        }

        unsafe public byte* ReadPtr<T>() where T : struct
        {
            byte* cur = m_CurrentPtr;
            int size = UnsafeUtility.SizeOf<T>();
            Advance(size);
            m_LastPtr = cur;
            return cur;
        }

        unsafe public byte* Peek()
        {
            return m_CurrentPtr;
        }

        unsafe public void Advance(int size)
        {
            m_CurrentPtr += size;

            m_NumElementsLeft--;
            m_NumElementsLeftInCurrentBlock--;
            if (m_NumElementsLeftInCurrentBlock == 0)
            {
                m_CurrentBlock = m_CurrentBlock->Next;
                if (m_CurrentBlock != null)
                {
                    m_NumElementsLeftInCurrentBlock = m_CurrentBlock->NumElements;
                    m_CurrentPtr = m_CurrentBlock->Start();
                }
                else
                {
                    m_NumElementsLeftInCurrentBlock = 0;
                    m_CurrentPtr = null;
                }
            }
        }

        public void Write<T>(T d) where T : struct
        {
            ref T prev = ref UnsafeUtilityEx.AsRef<T>(m_LastPtr);
            prev = d;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HpManifoldStreamHeader
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        public Unity.Physics.BodyIndexPair BodyIds;
        public int NumManifolds;
        ushort m_CacheQualityFlags;
        ushort m_Padding;
#pragma warning restore CS0649
#pragma warning restore CS0169
    }

    // This struct contains fields from SDK's hknpManifold
    unsafe struct HpManifold
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        // Inherited from hkcdManifold4
        public int NumPoints;
        float m_MinimumDistance;
        fixed byte m_Padding0[8];
        public float4 Normal;
        float4 m_WeldNormal;
        public fixed float Distances[4];
        public fixed float Positions[4 * 4];

        // hknpManifold fields
        float4 m_GskPosition;
        public byte m_ManifoldType;
        byte m_UseIncreasedIterations;
        byte m_IsNewSurface;
        byte m_IsNewManifold;
        byte m_NumVerticesOfQuad;
        public byte m_DataFields;
        byte m_AppliedWeldingTypes;
        byte m_Padding1;
        [NativeDisableUnsafePtrRestriction] internal HPManifoldCollisionCache* m_CollisionCache;
        internal uint m_ShapeKeyA;
        internal uint m_ShapeKeyB;
        [NativeDisableUnsafePtrRestriction] IntPtr m_MaterialA;
        [NativeDisableUnsafePtrRestriction] IntPtr m_MaterialB;

#if HK_IS_64BIT
        fixed byte m_Padding2[8];
#else
        fixed byte m_Padding2[4];
#endif
        internal fixed float m_Scratch[16];
#pragma warning restore CS0649
#pragma warning restore CS0169
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HPHalf
    {
        ushort m_data;
        public float Value
        {
            get
            {
                float ret = 0.0f;
                byte* retStorage = (byte*)UnsafeUtility.AddressOf(ref ret);
                retStorage[3] = (byte)(m_data >> 8);
                retStorage[2] = (byte)(m_data & 0xff);
                retStorage[1] = 0;
                retStorage[0] = 0;
                return ret;
            }
            set
            {
                float f = value;
                byte* v = (byte*)UnsafeUtility.AddressOf(ref f);
                m_data = (ushort)((v[3] << 8) | v[2]);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HPManifoldCollisionCache
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        // Inherited from hknpCollisionCache
        Unity.Physics.BodyIndexPair m_bodyPair;
        byte m_type;
        byte m_sizeDiv16;
        internal ushort m_qualityFlags;
        ushort m_linearTim;
        byte m_lodInfo;
        byte m_scratch;

        // Inherited from hknpGskCollisionCache
        fixed byte m_gskCache[5];
        byte m_propertyKeysUsed;
        ushort m_shapeBMaterialId;
        fixed byte m_separatingNormal[3];
        public byte m_propertiesStartOffsetDiv16;
        public uint m_propertyOffsets;

        // hknpManifoldCollisionCache's fields
        IntPtr m_contactJacobianBlock;
        ushort m_contactJacobianOffset;
        byte m_fractionOfClippedImpulseToApply;
        byte m_numContactPoints;
        public uint m_collisionFlags;


        public HPHalf m_friction;
        HPHalf m_extraStaticFriction;
        public HPHalf m_restitution;
        public HPHalf m_maxImpulse;
        public HPHalf m_maximumPenetration;
#if HK_IS_64BIT
        internal fixed byte m_padding0[2];
#else
        internal fixed byte m_padding0[6];
#endif

        // m_manifoldSolverInfo
        public HPHalf m_frictionRhsMultiplier;
        ushort m_flags;

        float4 m_impulsesApplied;
        float4 m_allowedInitialPenetrations;
        float4 m_constraintBiases;
        public float4 m_integratedFrictionRhs;
#pragma warning restore CS0649
#pragma warning restore CS0169

        internal unsafe HpPerManifoldProperty* GetCustomPropertyStorage()
        {
            const int propertyKey = 4; //USER_PROPERTY_0
            const int propertyAlignment = 4;

            byte propertiesStartDiv16 = m_propertiesStartOffsetDiv16;
            uint propertyOffsets = m_propertyOffsets;
            uint offset = (propertyOffsets >> ((propertyKey * 4) - 2)) & (0x0f * propertyAlignment);
            uint customDataOffset = (uint)(propertiesStartDiv16 * 16) + offset;

            HpPerManifoldProperty* cdp = (Havok.Physics.HpPerManifoldProperty*)(((byte*)UnsafeUtility.AddressOf(ref this)) + customDataOffset);
            return cdp;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HpJacAngular
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        internal float4 m_angular0;
        internal float4 m_angular1;
#pragma warning restore CS0649
#pragma warning restore CS0169
    };

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpJac3dFriction
    {
        public float4 m_jacDir0_linear0;
        public float4 m_jacDir0_angular0;
        public float4 m_jacDir0_angular1;
        public float4 m_jacDir1_linear0;
        public float4 m_jacDir1_angular0;
        public float4 m_jacDir1_angular1;
        public float4 m_jacAng_angular0;
        public float4 m_jacAng_angular1;
#pragma warning disable CS0169
#pragma warning disable CS0649
        // hkReals m_invJac01, m_maxImpulse, m_contactRadius and m_iterativeFriction
        fixed byte m_buffer[16];
#pragma warning restore CS0649
#pragma warning restore CS0169
    };

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpJacModHeader
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        fixed byte m_padding[16];
#pragma warning restore CS0649
#pragma warning restore CS0169
    };

    // This struct contains fields from SDK's hknpStreamContactJacobian
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpJacHeader
    {
#pragma warning disable CS0169
#pragma warning disable CS0649
        // Bitfields m_flags and m_dimB
        public ushort m_flagsAndDimB;

        public byte m_sizeDiv16;
        internal byte m_numPoints;
        internal byte m_modTypes;
        internal byte m_manifoldType;
        internal byte m_clipMode;
        internal byte m_clipImpulseFraction;

        internal uint m_bodyIdA;
        internal uint m_bodyIdB;

        internal float4 m_normal;

        internal HPManifoldCollisionCache* m_manifoldCollisionCache;
#if !HK_IS_64BIT
        uint m_padding;
#endif

        uint m_solverVelIdA;
        uint m_solverVelIdB;

        fixed ushort m_referenceOrientation[4];
        fixed ushort m_normalDotArm[4];
#pragma warning restore CS0649
#pragma warning restore CS0169

        enum ModifierType
        {
            NONE = 0,
            SURFACEVEL = 1 << 0,
            NORMALVEL = 1 << 1,
            MASSCHANGER = 1 << 2,
            COMCHANGER = 1 << 3,
        };

        internal int sizeOfUpToFriction(int numContactPoints)
        {
            return sizeof(HpJacHeader) + numContactPoints * sizeof(HpJacAngular);
        }

        public static bool hasAnyFriction(ushort flags)
        {
            // Value from SDK's JacHeaderFlags
            ushort jhFrictionAny = (1 << 4 | 1 << 5);
            return (flags & jhFrictionAny) > 0;
        }

        int sizeOfJacFriction(ushort flags)
        {
            return hasAnyFriction(flags) ? sizeof(HpJac3dFriction) : 0;
        }

        int sizeOfUpToModHdr(int numContactPoints, ushort flags)
        {
            return sizeOfUpToFriction(numContactPoints) + sizeOfJacFriction(flags);
        }

        int sizeOfUpToSurfaceVel(int numContactPoints, ushort flags, byte modifierTypes)
        {
            return sizeOfUpToModHdr(numContactPoints, flags) + ((modifierTypes == 0) ? 0 : sizeof(HpJacModHeader));
        }

        int sizeOfUpToNormalVel(int numContactPoints, ushort flags, byte modifierTypes)
        {
            return sizeOfUpToSurfaceVel(numContactPoints, flags, modifierTypes) + (((modifierTypes & (byte)ModifierType.SURFACEVEL) == 0) ? 0 : sizeof(Unity.Physics.SurfaceVelocity));
        }

        int sizeOfUpToInertiaFactor(int numContactPoints, ushort flags, byte modifierTypes)
        {
            return sizeOfUpToNormalVel(numContactPoints, flags, modifierTypes) + (((modifierTypes & (byte)ModifierType.NORMALVEL) == 0) ? 0 : sizeof(float4));
        }

        unsafe public HpJacAngular* accessJacAngular(int i)
        {
            byte* baseJac = (byte*)UnsafeUtility.AddressOf(ref this);
            int offset = sizeof(HpJacHeader) + i * sizeof(HpJacAngular);
            return (HpJacAngular*)(baseJac + offset);
        }

        unsafe public float3* accessSurfaceVelocity()
        {
            byte* baseJac = (byte*)UnsafeUtility.AddressOf(ref this);
            int offset = sizeOfUpToSurfaceVel(m_numPoints, m_flagsAndDimB, m_modTypes);
            return (float3*)(baseJac + offset);
        }

        unsafe public HpJac3dFriction* accessJacFriction()
        {
            byte* baseJac = (byte*)UnsafeUtility.AddressOf(ref this);
            int offset = sizeOfUpToFriction(m_numPoints);
            return (HpJac3dFriction*)(baseJac + offset);
        }

        unsafe public Unity.Physics.MassFactors* accessMassFactors()
        {
            byte* baseJac = (byte*)UnsafeUtility.AddressOf(ref this);
            int offset = sizeOfUpToInertiaFactor(m_numPoints, m_flagsAndDimB, m_modTypes);
            return (Unity.Physics.MassFactors*)(baseJac + offset);
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    struct HpPerManifoldProperty
    {
#pragma warning disable CS0649
        public Unity.Physics.CustomTagsPair m_bodyTagsPair;
        public byte m_jacobianFlags;
#pragma warning restore CS0649
    };

    // This struct contains fields from SDK's hkBlockStream::LinkedRange
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpLinkedRange
    {
#pragma warning disable CS0649
        // hkBlockStream::Range fields
        internal HpBlockStream.Block* m_block;
        internal ushort m_startByteOffset;
        internal ushort m_startBlockNumElements;
        internal int m_numElements;
#if HK_NEEDS_EXTRA_STRUCT_PACKING && !HK_IS_64BIT
        // Additional padding for 32bit MSVC
        fixed byte m_padding0[4];
#endif

        // hkBlockStream::LinkedRange fields
        internal HpLinkedRange* m_next;
#if HK_NEEDS_EXTRA_STRUCT_PACKING
    #if HK_IS_64BIT
        // Additional padding for 64bit MSVC
        fixed byte m_padding1[8];
    #else
        // Additional padding for 32bit MSVC
        fixed byte m_padding1[12];
    #endif
#endif

#pragma warning restore CS0649
    };

    // This struct contains fields from SDK's hknpCsJacRange
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpCsContactJacRange
    {
#pragma warning disable CS0649
        internal HpLinkedRange m_range;
        byte m_solverId;
        // Padding after m_solverId
        fixed byte m_padding0[3];
        int m_solveStateBytes;

#if HK_NEEDS_EXTRA_STRUCT_PACKING || !HK_IS_64BIT
        // Padding to make this struct 16B-aligned on MSVC (both 32 and 64bit) and 32bit non-MSVC
        // On 32bit non-MSVC, struct dsize is 24, but sizeof is 32 (because of packing alignment), so we need to add padding here.
        fixed byte m_padding1[8];
#endif

#pragma warning restore CS0649
    };

    // This struct contains fields from SDK's hknpCsGrid that are important for HavokPhysics
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpGrid
    {
#pragma warning disable CS0649
        // hknpGrid::m_entries
        internal HpCsContactJacRange* m_entries;
        internal int m_size;

        // We don't care about hknpGrid::m_lastEntries
#pragma warning restore CS0649

        // We don't care about the following hknpCsGrid fields: m_occupancy, m_tempsSize, m_tempsBuffer, m_tempsBufferSize
    };
}
