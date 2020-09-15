// This file contains mirror structures for Havok SDK structures that are used in Havok Physics code.
// Caution: Defines for 64bit platforms (like UNITY_64) cannot be relied upon when building Android, since ARMv7 and ARM64 can be selected
// at the same time and Unity is doing 1 cpp code generation pass which doesn't carry defines into cpp.

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_XBOXONE
// MSVC compiler cannot use the padding from base class to put derived class members
#define HK_IS_MSVC
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Havok.Physics
{
    // Class containing utility methods related to current build platform
    static class BuildPlatformUtil
    {
        // Returns whether we're building code for 32bit platform (performs a runtime check)
        internal static unsafe bool is32Bit()
        {
            return sizeof(void*) == 4;
        }
    }

    internal static class UnsafeEx
    {
        // Returns the memory offset (in bytes) between the specified pointer and referenced object
        internal unsafe static int CalculateOffset<T>(void* value, ref T baseValue)
            where T : struct
        {
            return (int)((byte*)value - (byte*)UnsafeUtility.AddressOf(ref baseValue));
        }

        // Fills the int array with offsets of specified type's fields, starting from specified index and updating the index after each write into array.
        internal unsafe static void FillFieldOffsetsArray(Type typeContainingFields, List<KeyValuePair<string, bool>> fieldNamesAndAccess, int* fieldOffsets, ref int fieldIndex)
        {
            foreach (KeyValuePair<string, bool> fieldInfo in fieldNamesAndAccess)
            {
                if (fieldInfo.Value)
                {
                    fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeContainingFields.GetField(fieldInfo.Key));
                }
                else
                {
                    fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeContainingFields.GetField(fieldInfo.Key, BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                }

                fieldIndex++;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct HpIntArray
    {
        public int* Data;
        private int m_Size;
        private int m_Flags;

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            const int myFieldCount = 3;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpIntArray).GetField("Data"));
            fieldIndex++;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpIntArray).GetField("m_Size", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpIntArray).GetField("m_Flags", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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

            // SDK has a 16B-aligned array with data here, which means there is
            // 12 Bytes (1 + 11) of padding on 32bit platforms and no padding on 64bit (data starts immediately)
            private fixed byte m_DataStart64Bit[1];
            private fixed byte m_Padding32Bit[11];
            private fixed byte m_DataStart32Bit[1];
#pragma warning restore CS0169, CS0649

            // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
            public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
            {
                // List of (field name, isPublic) pairs
                List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
                {
                    new KeyValuePair<string, bool>("m_NumElementsAndBytesUsed", false),
                    new KeyValuePair<string, bool>("m_BlockIndexInStream", false),
                    new KeyValuePair<string, bool>("m_NextBlock", false),
                    new KeyValuePair<string, bool>("m_AllocatorDebug", false),
                    new KeyValuePair<string, bool>("m_BlockStreamDebug", false)
                };

                // We need to account for m_DataStart* separately
                int myFieldCount = fieldNamesAndAccess.Count + 1;
                if (startFrom + myFieldCount > fieldOffsetsLength)
                    throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

                int fieldIndex = startFrom;
                UnsafeEx.FillFieldOffsetsArray(typeof(Block), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

                // Handle m_DataStart* fields
                Block temp = default;
                fieldOffsets[fieldIndex] = UnsafeEx.CalculateOffset((void*)temp.Start(), ref temp);
                fieldIndex++;

                Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
                return fieldIndex;
            }

            public int NumElements => (int)(m_NumElementsAndBytesUsed & 0xffff);
            public Block* Next => m_NextBlock;
            public byte* Start()
            {
                if (BuildPlatformUtil.is32Bit())
                {
                    // 32bit
                    fixed (byte* d = m_DataStart32Bit)
                    {
                        return d;
                    }
                }
                else
                {
                    // 64bit
                    fixed (byte* d = m_DataStart64Bit)
                    {
                        return d;
                    }
                }
            }
        }

#pragma warning disable CS0649, CS0169
        IntPtr m_Allocator;
        internal int m_NumTotalElements;
        byte m_PartiallyFreed;
        byte m_IsLocked;

        // SDK has a 16B-aligned in-place array here, but we only care about the pointer to the data (actually a Block**)
        // There's 2 Bytes of padding on 64bit platforms and 6B on 32bit, before data pointer
        fixed byte m_CommonPadding[2];

        // Data pointer on 64bit platforms is here
        Block** m_Data64Bit;

        // Data pointer on 32bit platforms is here: we had 2B common padding + 4B for m_Data64Bit(unused in 32bit, effectively padding)
        Block** m_Data32Bit;

        // There's more fields of in-place array here, but we don't care about them
#pragma warning restore CS0649, CS0169

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("m_Allocator", false),
                new KeyValuePair<string, bool>("m_NumTotalElements", false),
                new KeyValuePair<string, bool>("m_PartiallyFreed", false),
                new KeyValuePair<string, bool>("m_IsLocked", false)
            };

            // We need to account for m_Data* separately
            int myFieldCount = fieldNamesAndAccess.Count + 1;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpBlockStream), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpBlockStream).GetField("m_Data32Bit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            }
            else
            {
                // 64bit
                fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpBlockStream).GetField("m_Data64Bit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            }

            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }

        public bool HasElements => m_NumTotalElements != 0;
        public Block* FirstBlock()
        {
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                return m_Data32Bit[0];
            }
            else
            {
                // 64bit
                return m_Data64Bit[0];
            }
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
            m_CurrentBlock = range->m_blockStreamRange.m_block;
            if (m_CurrentBlock != null)
            {
                m_CurrentPtr = m_CurrentBlock->Start() + range->m_blockStreamRange.m_startByteOffset;
            }
            else
            {
                m_CurrentPtr = null;
            }
            m_NumElementsLeftInCurrentBlock = range->m_blockStreamRange.m_startBlockNumElements;
            m_NumElementsLeft = range->m_blockStreamRange.m_numElements;
            m_LastPtr = null;
        }

        public ref T Read<T>() where T : struct
        {
            return ref UnsafeUtility.AsRef<T>(ReadPtr<T>());
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
            ref T prev = ref UnsafeUtility.AsRef<T>(m_LastPtr);
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

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("BodyIds", true),
                new KeyValuePair<string, bool>("NumManifolds", true),
                new KeyValuePair<string, bool>("m_CacheQualityFlags", false),
                new KeyValuePair<string, bool>("m_Padding", false)
            };

            int myFieldCount = fieldNamesAndAccess.Count;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpManifoldStreamHeader), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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

        // Padding is 8B on 64bit and 4B on 32bit
        void* m_Padding2;

        internal fixed float m_Scratch[16];
#pragma warning restore CS0649
#pragma warning restore CS0169

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("NumPoints", true),
                new KeyValuePair<string, bool>("m_MinimumDistance", false),
                new KeyValuePair<string, bool>("Normal", true),
                new KeyValuePair<string, bool>("m_WeldNormal", false),
                new KeyValuePair<string, bool>("Distances", true),
                new KeyValuePair<string, bool>("Positions", true),
                new KeyValuePair<string, bool>("m_GskPosition", false),
                new KeyValuePair<string, bool>("m_ManifoldType", true),
                new KeyValuePair<string, bool>("m_UseIncreasedIterations", false),
                new KeyValuePair<string, bool>("m_IsNewSurface", false),
                new KeyValuePair<string, bool>("m_IsNewManifold", false),
                new KeyValuePair<string, bool>("m_NumVerticesOfQuad", false),
                new KeyValuePair<string, bool>("m_DataFields", true),
                new KeyValuePair<string, bool>("m_AppliedWeldingTypes", false),
                new KeyValuePair<string, bool>("m_CollisionCache", false),
                new KeyValuePair<string, bool>("m_ShapeKeyA", false),
                new KeyValuePair<string, bool>("m_ShapeKeyB", false),
                new KeyValuePair<string, bool>("m_MaterialA", false),
                new KeyValuePair<string, bool>("m_MaterialB", false),
                new KeyValuePair<string, bool>("m_Scratch", false)
            };

            int myFieldCount = fieldNamesAndAccess.Count;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpManifold), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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

    // This struct contains fields from SDK's hknpManifoldCollisionCache
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

        // hknpManifoldCollisionCache's fields (starts at 32B)
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

        // SDK has 2B padding on 64bit and 6B on 32bit (aligns m_manifoldSolverInfo to start at 60B and end at 64B)
        internal fixed byte m_padding0[2];

        // m_manifoldSolverInfo on 64bit 
        HPHalf m_frictionRhsMultiplier64Bit;
        ushort m_flags64Bit;

        // m_manifoldSolverInfo on 32bit is here, where we currently have automatic padding before m_impulsesApplied

        // m_impulsesApplied is float4 in SDK, but we're using double2 in order to get 4B padding before it on 32bit (m_manifoldSolverInfo, see comment above)
        double2 m_impulsesApplied;
        float4 m_allowedInitialPenetrations;
        float4 m_constraintBiases;
        public float4 m_integratedFrictionRhs;
#pragma warning restore CS0649
#pragma warning restore CS0169

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("m_bodyPair", false),
                new KeyValuePair<string, bool>("m_type", false),
                new KeyValuePair<string, bool>("m_sizeDiv16", false),
                new KeyValuePair<string, bool>("m_qualityFlags", false),
                new KeyValuePair<string, bool>("m_linearTim", false),
                new KeyValuePair<string, bool>("m_lodInfo", false),
                new KeyValuePair<string, bool>("m_scratch", false),
                new KeyValuePair<string, bool>("m_gskCache", false),
                new KeyValuePair<string, bool>("m_propertyKeysUsed", false),
                new KeyValuePair<string, bool>("m_shapeBMaterialId", false),
                new KeyValuePair<string, bool>("m_separatingNormal", false),
                new KeyValuePair<string, bool>("m_propertiesStartOffsetDiv16", true),
                new KeyValuePair<string, bool>("m_propertyOffsets", true),
                new KeyValuePair<string, bool>("m_contactJacobianBlock", false),
                new KeyValuePair<string, bool>("m_contactJacobianOffset", false),
                new KeyValuePair<string, bool>("m_fractionOfClippedImpulseToApply", false),
                new KeyValuePair<string, bool>("m_numContactPoints", false),
                new KeyValuePair<string, bool>("m_collisionFlags", true),
                new KeyValuePair<string, bool>("m_friction", true),
                new KeyValuePair<string, bool>("m_extraStaticFriction", false),
                new KeyValuePair<string, bool>("m_restitution", true),
                new KeyValuePair<string, bool>("m_maxImpulse", true),
                new KeyValuePair<string, bool>("m_maximumPenetration", true),
                new KeyValuePair<string, bool>("m_padding0", false),
                // m_manifoldSolverInfo will be handled separately at the end
                new KeyValuePair<string, bool>("m_impulsesApplied", false),
                new KeyValuePair<string, bool>("m_allowedInitialPenetrations", false),
                new KeyValuePair<string, bool>("m_constraintBiases", false),
                new KeyValuePair<string, bool>("m_integratedFrictionRhs", true)
            };

            // Account for m_manifoldSolverInfo, which is handled separately
            int myFieldCount = fieldNamesAndAccess.Count + 1;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HPManifoldCollisionCache), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            // Handle m_manifoldSolverInfo
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                HPManifoldCollisionCache temp = default;
                fieldOffsets[fieldIndex] = UnsafeEx.CalculateOffset((void*)temp.accessFrictionRhsMultiplier32Bit(), ref temp);
            }
            else
            {
                // 64bit
                fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HPManifoldCollisionCache).GetField("m_frictionRhsMultiplier64Bit", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            }

            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }

        public float getFrictionRhsMultiplierValue()
        {
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                return accessFrictionRhsMultiplier32Bit()->Value;
            }
            else
            {
                // 64bit
                return m_frictionRhsMultiplier64Bit.Value;
            }
        }

        public void setFrictionRhsMultiplierValue(float value)
        {
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                accessFrictionRhsMultiplier32Bit()->Value = value;
            }
            else
            {
                // 64bit
                m_frictionRhsMultiplier64Bit.Value = value;
            }
        }

        public ushort getFlags()
        {
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                return *accessFlags32Bit();
            }
            else
            {
                // 64bit
                return m_flags64Bit;
            }
        }

        public void setFlags(ushort value)
        {
            if (BuildPlatformUtil.is32Bit())
            {
                // 32bit
                *accessFlags32Bit() = value;
            }
            else
            {
                // 64bit
                m_flags64Bit = value;
            }
        }

        public float4 getImpulsesApplied()
        {
            // Interpret m_impulsesApplied properly (as float4)
            fixed (double2* impulsesAppliedDouble2Ptr = &m_impulsesApplied)
            {
                return *((float4*)impulsesAppliedDouble2Ptr);
            }
        }

        public void setImpulsesApplied(float4 value)
        {
            // Interpret m_impulsesApplied properly (as float4)
            fixed (double2* impulsesAppliedDouble2Ptr = &m_impulsesApplied)
            {
                *((float4*)impulsesAppliedDouble2Ptr) = value;
            }
        }

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

        private HPHalf* accessFrictionRhsMultiplier32Bit()
        {
            fixed (ushort* flagsPtr = &m_flags64Bit)
            {
                // m_frictionRhsMultiplier on 32bit is right after m_flags64Bit (they're both 2B long), so increment flagsPtr by 1
                ushort* frictionRhsMultiplierShortPtr = flagsPtr + 1;
                return (HPHalf*)frictionRhsMultiplierShortPtr;
            }
        }

        private ushort* accessFlags32Bit()
        {
            fixed (ushort* flagsPtr = &m_flags64Bit)
            {
                // m_flags on 32bit starts 4B after m_flags64Bit (m_frictionRhsMultiplier for 32bit is between them, all 3 are 2B long), so increment flagsPtr by 2
                return flagsPtr + 2;
            }
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

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            const int myFieldCount = 2;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpJacAngular).GetField("m_angular0", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpJacAngular).GetField("m_angular1", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("m_jacDir0_linear0", true),
                new KeyValuePair<string, bool>("m_jacDir0_angular0", true),
                new KeyValuePair<string, bool>("m_jacDir0_angular1", true),
                new KeyValuePair<string, bool>("m_jacDir1_linear0", true),
                new KeyValuePair<string, bool>("m_jacDir1_angular0", true),
                new KeyValuePair<string, bool>("m_jacDir1_angular1", true),
                new KeyValuePair<string, bool>("m_jacAng_angular0", true),
                new KeyValuePair<string, bool>("m_jacAng_angular1", true),
                new KeyValuePair<string, bool>("m_buffer", false)
            };

            int myFieldCount = fieldNamesAndAccess.Count;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpJac3dFriction), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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

        // 4B padding on 32bit

        // m_solverVelIdA and m_solverVelIdB are uint in SDK, but we're using them together as double in order to get 4B padding on 32bit automatically (before the field)
        // Please use accessSolverVelIdA() and accessSolverVelIdB() to read/write values
        double m_solverVelIdAB;

        fixed ushort m_referenceOrientation[4];
        fixed ushort m_normalDotArm[4];
#pragma warning restore CS0649
#pragma warning restore CS0169

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // List of (field name, isPublic) pairs
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                // Need to skip m_flagsAndDimB, since it's a bitfield in C++ and can't be verified
                new KeyValuePair<string, bool>("m_sizeDiv16", true),
                new KeyValuePair<string, bool>("m_numPoints", false),
                new KeyValuePair<string, bool>("m_modTypes", false),
                new KeyValuePair<string, bool>("m_manifoldType", false),
                new KeyValuePair<string, bool>("m_clipMode", false),
                new KeyValuePair<string, bool>("m_clipImpulseFraction", false),
                new KeyValuePair<string, bool>("m_bodyIdA", false),
                new KeyValuePair<string, bool>("m_bodyIdB", false),
                new KeyValuePair<string, bool>("m_normal", false),
                new KeyValuePair<string, bool>("m_manifoldCollisionCache", false),
                new KeyValuePair<string, bool>("m_solverVelIdAB", false),
                new KeyValuePair<string, bool>("m_referenceOrientation", false),
                new KeyValuePair<string, bool>("m_normalDotArm", false),
            };

            int myFieldCount = fieldNamesAndAccess.Count;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpJacHeader), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }

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

        unsafe public uint* accessSolverVelIdA()
        {
            fixed (double* solverVelIdsPtr = &m_solverVelIdAB)
            {
                // solverVelIdA is stored first in m_solverVelIdAB
                return (uint*)solverVelIdsPtr;
            }
        }

        unsafe public uint* accessSolverVelIdB()
        {
            fixed (double* solverVelIdsPtr = &m_solverVelIdAB)
            {
                // solverVelIdB is stored second in m_solverVelIdAB, so increment solverVelIdsPtr by 1
                return ((uint*)solverVelIdsPtr) + 1;
            }
        }
    };

    // This struct doesn't really exist in SDK, it's how we interpret free-form properties referenced from hknpManifoldCollisionCache
    [StructLayout(LayoutKind.Sequential)]
    struct HpPerManifoldProperty
    {
#pragma warning disable CS0649
        public Unity.Physics.CustomTagsPair m_bodyTagsPair;
        public byte m_jacobianFlags;
#pragma warning restore CS0649
    };

    // This struct contains fields from SDK's hkBlockStream::Range
#if HK_IS_MSVC
    // On MSVC (both 32 and 64bit) size is 16B (4B padding automatically added at the end on 32bit).
    [StructLayout(LayoutKind.Sequential, Size = 16)]
#else
    // On non-MSVC size is default (12B on 32bit, 16B on 64bit).
    [StructLayout(LayoutKind.Sequential)]
#endif
    unsafe struct HpBlockStreamRange
    {
        internal HpBlockStream.Block* m_block;
        internal ushort m_startByteOffset;
        internal ushort m_startBlockNumElements;
        internal int m_numElements;
    };

    // This struct contains fields from SDK's hkBlockStream::LinkedRange
#if HK_IS_MSVC
    // On MSVC (both 32 and 64bit) size is 32B (automatic padding at the end is 12B on 32bit and 8B on 64bit).
    [StructLayout(LayoutKind.Sequential, Size = 32)]
#else
    // On non-MSVC size is default (16B on 32bit, 24B on 64bit).
    [StructLayout(LayoutKind.Sequential)]
#endif
    unsafe struct HpLinkedRange
    {
#pragma warning disable CS0649
        // hkBlockStream::Range is base class in SDK
        internal HpBlockStreamRange m_blockStreamRange;

        // hkBlockStream::LinkedRange fields
        internal HpLinkedRange* m_next;
#pragma warning restore CS0649
    };

    // This struct contains fields from SDK's hknpCsJacRange
#if HK_IS_MSVC
    // On MSVC (both 32 and 64bit) size is 48B (automatic padding 8B at the end on both)
    [StructLayout(LayoutKind.Sequential, Size = 48)]
#else
    // On 64bit non-MSVC size is 32B. On 32bit non-MSVC, struct dsize is 24, but sizeof is 32 (because of packing alignment), so there's 8B automatic padding at the end.
    [StructLayout(LayoutKind.Sequential, Size = 32)]
#endif
    unsafe struct HpCsContactJacRange
    {
#pragma warning disable CS0649
        internal HpLinkedRange m_range;
        byte m_solverId;
        // Padding after m_solverId
        fixed byte m_padding0[3];
        int m_solveStateBytes;
#pragma warning restore CS0649

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            const int myFieldCount = 7;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            // HpBlockStreamRange fields
            List<KeyValuePair<string, bool>> fieldNamesAndAccess = new List<KeyValuePair<string, bool>>()
            {
                new KeyValuePair<string, bool>("m_block", false),
                new KeyValuePair<string, bool>("m_startByteOffset", false),
                new KeyValuePair<string, bool>("m_startBlockNumElements", false),
                new KeyValuePair<string, bool>("m_numElements", false)
            };

            // Just fill in offsets of HpBlockStreamRange fields, since m_range.m_blockStreamRange is at offset 0
            int fieldIndex = startFrom;
            UnsafeEx.FillFieldOffsetsArray(typeof(HpBlockStreamRange), fieldNamesAndAccess, fieldOffsets, ref fieldIndex);

            // Add offset of m_range.m_next, since m_range is the first field in this struct
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpLinkedRange).GetField("m_next", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;

            // Now add offset of the other 2 fields in this struct (m_padding0 is not important)
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpCsContactJacRange).GetField("m_solverId", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpCsContactJacRange).GetField("m_solveStateBytes", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
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
        // We don't care about the following hknpCsGrid fields: m_occupancy, m_tempsSize, m_tempsBuffer, m_tempsBufferSize
#pragma warning restore CS0649

        // Fills the specified array (given as pointer and length) with offsets of this struct's fields that need to be in sync with plugin struct fields.
        public unsafe static int FillFieldOffsetsArray(int startFrom, int* fieldOffsets, int fieldOffsetsLength)
        {
            // m_entries and m_size are members of 1 field in C++ (hknpGrid::m_entries), so we can only send offset of m_entries
            const int myFieldCount = 1;
            if (startFrom + myFieldCount > fieldOffsetsLength)
                throw new ArgumentException($"{nameof(fieldOffsets)} (length {fieldOffsetsLength}) is not big enough for all {myFieldCount} fields of this struct (starting from {startFrom})!");

            int fieldIndex = startFrom;
            fieldOffsets[fieldIndex] = UnsafeUtility.GetFieldOffset(typeof(HpGrid).GetField("m_entries", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            fieldIndex++;

            Assert.AreEqual(myFieldCount, fieldIndex - startFrom);
            return fieldIndex;
        }
    };
}
