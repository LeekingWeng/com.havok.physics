// This file contains a trivial implementation of the plugin interfaces
// with an additional initialization function which dynamically loads
// the real plugin and extracts the functions we need.
#include <dlfcn.h>

#define EXPORT_API __attribute__ ((visibility("default")))

extern "C"
{
    static struct PluginFuncs
    {
        typedef int (*HP_AllocateWorldFn)(void* config, void* context);
        typedef void (*HP_DestroyWorldFn)(int worldIdx);
        typedef void (*HP_SyncWorldInFn)(int worldIdx, void* bodies, int numBodies, int bodyStride,
            void* motionDatas, int numMotionData, int motionDataStride,
            void* motionVelocities, int numMotionVelocities, int motionVelocityStride,
            void* joints, int numJoints, int jointStride);
        typedef void (*HP_SyncMotionsOutFn)(int worldIdx, void* motionDatas, int numMotionDatas, int motionDataStride,
            void* motionVelocity, int numMotionVelocities, int motionVelocityStride,
            int startIndex, int num);
        typedef void (*HP_StepWorldFn)(int worldIndex, void* stepInput, void* stepContext);
        typedef void (*HP_ProcessStepFn)(void* task);
        typedef void (*HP_StepVisualDebuggerFn)(int worldIndex, float timestep, void* camera);
        typedef void (*HP_InjectContactsFn)(int worldIndex, void* firstBlock, int totalNumItems, int blockSize);
        typedef bool (*HP_CheckCompatibilityFn)(void* typeCheckInfo);
        typedef bool (*HP_UnlockPluginFn)(void* token);
        typedef bool (*HP_IsPluginUnlockedFn)();

        HP_AllocateWorldFn m_allocateWorld;
        HP_DestroyWorldFn m_destroyWorld;
        HP_SyncWorldInFn m_syncWorldIn;
        HP_SyncMotionsOutFn m_syncMotionsOut;
        HP_StepWorldFn m_stepWorld;
        HP_ProcessStepFn m_processStep;
        HP_StepVisualDebuggerFn m_stepVisualDebugger;
        HP_InjectContactsFn m_injectContacts;
        HP_CheckCompatibilityFn m_checkCompatibility;
        HP_UnlockPluginFn m_unlockPlugin;
        HP_IsPluginUnlockedFn m_isPluginUnlocked;
    } s_loadedFuncs;

    void EXPORT_API HP_InitStaticPlugin()
    {
        const char* dynamicLibName = "HavokNative.bundle/HavokNative.dylib";
        void* libHandle = dlopen(dynamicLibName, RTLD_LAZY);

        s_loadedFuncs.m_allocateWorld = (PluginFuncs::HP_AllocateWorldFn)dlsym(libHandle, "HP_AllocateWorld");
        s_loadedFuncs.m_destroyWorld = (PluginFuncs::HP_DestroyWorldFn)dlsym(libHandle, "HP_DestroyWorld");
        s_loadedFuncs.m_syncWorldIn = (PluginFuncs::HP_SyncWorldInFn)dlsym(libHandle, "HP_SyncWorldIn");
        s_loadedFuncs.m_syncMotionsOut = (PluginFuncs::HP_SyncMotionsOutFn)dlsym(libHandle, "HP_SyncMotionsOut");
        s_loadedFuncs.m_stepWorld = (PluginFuncs::HP_StepWorldFn)dlsym(libHandle, "HP_StepWorld");
        s_loadedFuncs.m_processStep = (PluginFuncs::HP_ProcessStepFn)dlsym(libHandle, "HP_ProcessStep");
        s_loadedFuncs.m_stepVisualDebugger = (PluginFuncs::HP_StepVisualDebuggerFn)dlsym(libHandle, "HP_StepVisualDebugger");
        s_loadedFuncs.m_injectContacts = (PluginFuncs::HP_InjectContactsFn)dlsym(libHandle, "HP_InjectContacts");
        s_loadedFuncs.m_checkCompatibility = (PluginFuncs::HP_CheckCompatibilityFn)dlsym(libHandle, "HP_CheckCompatibility");
        s_loadedFuncs.m_unlockPlugin = (PluginFuncs::HP_UnlockPluginFn)dlsym(libHandle, "HP_UnlockPlugin");
        s_loadedFuncs.m_isPluginUnlocked = (PluginFuncs::HP_IsPluginUnlockedFn)dlsym(libHandle, "HP_IsPluginUnlocked");

        // Emulate Unity calling UnityPluginLoad
        typedef void (*HP_PluginOnLoad)(void* interfaces);
        HP_PluginOnLoad onLoadFunc = (HP_PluginOnLoad)dlsym(libHandle, "UnityPluginLoad");
        int interfaces = 0; //<todo.eoin.hpi This is not the real type, but we require it to be non-null and do not dereference it on iOS.
        onLoadFunc(&interfaces);
    }

    int EXPORT_API HP_AllocateWorld(void* config, void* stepContext)
    {
        return s_loadedFuncs.m_allocateWorld(config, stepContext);
    }

    void EXPORT_API HP_DestroyWorld(int worldIndex)
    {
        return s_loadedFuncs.m_destroyWorld(worldIndex);
    }

    void EXPORT_API HP_SyncWorldIn(
        int worldIndex,
        void* const unityBodies, int numBodies, int bodyStride,
        void* const unityMotionDatas, int numMotionDatas, int motionDataStride,
        void* const unityMotionVelocities, int numMotionVelocities, int motionVelocityStride,
        void* const unityJoints, int numJoints, int jointStride)
    {
        return s_loadedFuncs.m_syncWorldIn(worldIndex,
            unityBodies, numBodies, bodyStride,
            unityMotionDatas, numMotionDatas, motionDataStride,
            unityMotionVelocities, numMotionVelocities, motionVelocityStride,
            unityJoints, numJoints, jointStride);
    }

    void EXPORT_API HP_SyncMotionsOut(
        int worldIndex,
        void* unityMotionDatas, int numMotionDatas, int motionDataStride,
        void* unityMotionVelocities, int numMotionVelocities, int motionVelocityStride,
        int startIndex, int num)
    {
        return s_loadedFuncs.m_syncMotionsOut(worldIndex,
            unityMotionDatas, numMotionDatas, motionDataStride,
            unityMotionVelocities, numMotionVelocities, motionVelocityStride,
            startIndex, num);
    }

    void EXPORT_API HP_StepWorld(int worldIndex, void* stepInput, void* stepContext)
    {
        return s_loadedFuncs.m_stepWorld(worldIndex, stepInput, stepContext);
    }

    void EXPORT_API HP_ProcessStep(void* ptask)
    {
        return s_loadedFuncs.m_processStep(ptask);
    }

    void EXPORT_API HP_StepVisualDebugger(int worldIndex, float timestep, void* camera)
    {
        return s_loadedFuncs.m_stepVisualDebugger(worldIndex, timestep, camera);
    }

    void EXPORT_API HP_InjectContacts(int worldIndex, void* firstBlock, int totalNumItems, int blockSize)
    {
        return s_loadedFuncs.m_injectContacts(worldIndex, firstBlock, totalNumItems, blockSize);
    }

    bool EXPORT_API HP_CheckCompatibility(void* typeCheckInfo)
    {
        return s_loadedFuncs.m_checkCompatibility(typeCheckInfo);
    }

    bool EXPORT_API HP_UnlockPlugin(void* token)
    {
        return s_loadedFuncs.m_unlockPlugin(token);
    }

    bool EXPORT_API HP_IsPluginUnlocked()
    {
        return s_loadedFuncs.m_isPluginUnlocked();
    }
}
