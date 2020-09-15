using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Physics;

namespace Havok.Physics
{
    public static class Plugin
    {
        #region C++ functions

#if UNITY_IOS && !UNITY_EDITOR
        // On iOS plugins are statically linked into the executable, so we must use __Internal as the library name.
        const string k_DllPath = "__Internal";

        [DllImport(k_DllPath)]
        static extern unsafe void UnityPluginLoad(void* unityInterfaces);

        static unsafe Plugin()
        {
            // This is not the real type, but we require it to be non-null and do not dereference it on iOS.
            int interfaces = 0;
            UnityPluginLoad(&interfaces);
        }
#else
        const string k_DllPath = "HavokNative";
#endif

        [DllImport(k_DllPath)]
        static extern bool HP_CheckCompatibility(ref TypeCheckInformation checkInfo);
        [DllImport(k_DllPath)]
        static extern int HP_UnlockPlugin(ref AuthInfo token);
        [DllImport(k_DllPath)]
        static extern bool HP_IsPluginUnlocked();
        [DllImport(k_DllPath)]
        static extern unsafe bool HP_Configure(char* key, char* value);

        [DllImport(k_DllPath)]
        internal static extern unsafe int HP_AllocateWorld(ref HavokConfiguration config, HavokSimulation.StepContext* stepContext);
        [DllImport(k_DllPath)]
        internal static extern void HP_DestroyWorld(int worldIndex);
        [DllImport(k_DllPath)]
        internal static extern unsafe void HP_SyncWorldIn(
            int worldIndex,
            void* rigidBodies, int numRigidBodies, int rigidBodyStride,
            MotionData* motionDatas, int numMotionDatas, int motionDataStride,
            MotionVelocity* motionVelocities, int numMotionVelocities, int motionVelocityStride,
            void* joints, int numJoints, int jointStride, bool haveStaticBodiesChanged);
        [DllImport(k_DllPath)]
        internal static extern unsafe void HP_SyncMotionsOut(
            int worldIndex,
            MotionData* motionDatas, int numMotionDatas, int motionDataStride,
            MotionVelocity* motionVelocities, int numMotionVelocities, int motionVelocityStride,
            int startIndex, int num);
        [DllImport(k_DllPath)]
        internal static extern unsafe int HP_StepWorld(int worldIndex, ref HavokSimulation.StepInput input, HavokSimulation.StepContext* context);
        [DllImport(k_DllPath)]
        internal static extern void HP_StepVisualDebugger(int worldIndex, float timeStep, ref HavokSimulation.Camera camera);
        [DllImport(k_DllPath)]
        internal static extern unsafe void HP_ProcessStep(HavokSimulation.Task* task);

        #endregion

        // Hashes of some types shared with C++. Used to check compatibility.
        [StructLayout(LayoutKind.Sequential)]
        unsafe struct TypeCheckInformation
        {
            public ulong m_numTypes;
            public fixed ulong m_typeCheckValues[6];

            // Field offsets we're sending to plugin. Keep up to date with actual usage and C++ plugin!
            public const int fieldOffsetsLength = 99;
            public fixed int m_fieldOffsets[fieldOffsetsLength];

            // Struct sizes we're sending to plugin. Keep up to date with actual usage and C++ plugin!
            public const int structSizesLength = 9;
            public fixed int m_structSizes[structSizesLength];
        }

        // Information about a user
        [StructLayout(LayoutKind.Sequential)]
        unsafe struct UserInfo
        {
            internal fixed char m_IdentityUrl[256];
            internal fixed char m_AccessToken[256];
            internal fixed char m_UserId[256];

            // String setters
            public string IdentityUrl
            {
                set
                {
                    fixed (char* str = m_IdentityUrl)
                    {
                        int i = 0;
                        for (; i < value.Length && i < 255; i++)
                        {
                            str[i] = value[i];
                        }
                        str[i] = '\0';
                    }
                }
            }
            public string AccessToken
            {
                set
                {
                    fixed (char* str = m_AccessToken)
                    {
                        int i = 0;
                        for (; i < value.Length && i < 255; i++)
                        {
                            str[i] = value[i];
                        }
                        str[i] = '\0';
                    }
                }
            }
            public string UserId
            {
                set
                {
                    fixed (char* str = m_UserId)
                    {
                        int i = 0;
                        for (; i < value.Length && i < 255; i++)
                        {
                            str[i] = value[i];
                        }
                        str[i] = '\0';
                    }
                }
            }
        }

        // Information used to unlock the plugin
        [StructLayout(LayoutKind.Sequential)]
        unsafe struct AuthInfo
        {
            internal AuthInfo(UserInfo userInfo)
            {
                this = default;
                fixed (AuthInfo* self = &this)
                {
                    UnsafeUtility.MemCpy(self->m_IdentityUrl, userInfo.m_IdentityUrl, 256 * sizeof(char));
                    UnsafeUtility.MemCpy(self->m_AccessToken, userInfo.m_AccessToken, 256 * sizeof(char));
                    UnsafeUtility.MemCpy(self->m_UserId, userInfo.m_UserId, 256 * sizeof(char));
                }
            }

            // User info. Fixed size buffers so that this can be used in jobs.
            fixed char m_IdentityUrl[256];
            fixed char m_AccessToken[256];
            fixed char m_UserId[256];

            // Havok token
            public ulong HavokToken;
            public long HavokTokenExpiry; // seconds UTC
            public long HavokSubscriptionExpiry; // seconds UTC
        }

#if !UNITY_EDITOR

        internal static void EnsureUnlocked()
        {
            // Plugin should always be available outside of the editor
            if (!HP_IsPluginUnlocked())
                throw new InvalidOperationException("Havok Physics plugin locked");
        }

#else

        // Make sure the plugin is unlocked. Wait for it if necessary.
        internal static void EnsureUnlocked()
        {
            if (HP_IsPluginUnlocked())
            {
                return;
            }

            // Don't create dialogs if in automatic mode or explicitly requested (used by unit tests)
            bool createDialogs = UnityEditorInternal.InternalEditorUtility.isHumanControllingUs
                && !UnityEngine.PlayerPrefs.HasKey("Havok.Auth.SuppressDialogs");

            using (var unlocker = new Unlocker())
            {
                while (true)
                {
                    if (!unlocker.IsCompleted)
                    {
                        if (createDialogs)
                        {
                            if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Checking Havok Physics permission", "Please wait...", 0.0f))
                            {
                                UnityEditor.EditorUtility.ClearProgressBar();
                                break;
                            }
                        }
                    }
                    else
                    {
                        const string assetStoreLink = "https://aka.ms/hkunityassetstore";
                        const string failureTitle = "Havok Physics simulation disabled";
                        const string closeButtonText = "Close";
                        UnityEditor.EditorUtility.ClearProgressBar();
                        switch (unlocker.Result.Value)
                        {
                            case UnlockResult.Status.NoUser:
                                {
                                    string message = "Could not check Havok Physics permissions because no user is signed into Unity.\nPlease sign in.";
                                    if (createDialogs)
                                    {
                                        UnityEditor.EditorUtility.DisplayDialog(failureTitle, message, closeButtonText);
                                    }
                                    else
                                    {
                                        UnityEngine.Debug.LogError(failureTitle + ".\n" + message);
                                    }
                                }
                                break;

                            case UnlockResult.Status.NoSubscription:
                                {
                                    string message = "No Havok Physics subscription found for the current user.\nUnity Pro users must purchase a subscription from the Unity Asset Store.";
                                    if (createDialogs)
                                    {
                                        if (UnityEditor.EditorUtility.DisplayDialog(failureTitle, message, "Go to Asset Store page", closeButtonText))
                                        {
                                            UnityEditor.Help.BrowseURL(assetStoreLink);
                                        }
                                    }
                                    else
                                    {
                                        UnityEngine.Debug.LogError(failureTitle + ".\n" + message);
                                    }
                                }
                                break;

                            case UnlockResult.Status.Unlocked:
                                {
                                    // Show the license expire warning in console (not in a dialog), but only in cases where user is playing a scene from editor
                                    if (createDialogs && unlocker.Result.NumDaysRemaining < 10)
                                    {
                                        UnityEngine.Debug.LogWarningFormat("Havok.Physics license will expire in {0} days. Please extend it from the Unity Asset Store: {1} .",
                                            unlocker.Result.NumDaysRemaining, assetStoreLink);
                                    }
                                }
                                break;

                            default:
                                break;
                        }
                        break;
                    }
                }
            }
        }

        // Create a background job to unlock the plugin. Don't wait for it.
        static void TryUnlockAsync()
        {
            if (HP_IsPluginUnlocked())
            {
                return;
            }

            var unlocker = new Unlocker();
            void OnUpdate()
            {
                if (unlocker.IsCompleted)
                {
                    UnityEditor.EditorApplication.update -= OnUpdate;
                    unlocker.Dispose();
                }
            };
            UnityEditor.EditorApplication.update += OnUpdate;
        }

        static UserInfo? GetUserInfo()
        {
            var editorAssembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.EditorWindow));

            Type unityConnectType = editorAssembly.GetType("UnityEditor.Connect.UnityConnect");
            object unityConnect = unityConnectType.GetProperty("instance").GetValue(null, null);

            //bool isLoggedIn = (bool)unityConnectType.GetProperty("loggedIn").GetValue(unityConnect, null);
            //if (!isLoggedIn)
            //    return null;

            UserInfo userInfoOut;
            
            int cloudIdentityEnum = 6; // TODO: Use reflection to get this from UnityEditor.Connect.CloudConfigUrl.CloudIdentity
            userInfoOut.IdentityUrl = unityConnectType.GetMethod("GetConfigurationURL").Invoke(unityConnect, new object[] { cloudIdentityEnum }) as string;

            object userInfo = unityConnectType.GetProperty("userInfo").GetValue(unityConnect, null);
            Type userInfoType = userInfo.GetType();
            userInfoOut.AccessToken = userInfoType.GetProperty("accessToken").GetValue(userInfo, null) as string;
            userInfoOut.UserId = userInfoType.GetProperty("userId").GetValue(userInfo, null) as string;

            return userInfoOut;
        }

        struct UnlockResult
        {
            public enum Status
            {
                // Fail
                Invalid,
                NoUser,
                NoSubscription,

                // Success
                Unlocked
            }

            public Status Value;

            // Number of days remaining if unlocked
            public int NumDaysRemaining;
        }

        // Manages a job which tries to unlock the plugin, based on the current user's entitlements.
        class Unlocker : IDisposable
        {
            readonly NativeArray<AuthInfo> m_AuthInfo = new NativeArray<AuthInfo>(1, Allocator.Persistent);
            readonly NativeArray<UnlockResult> m_Result = new NativeArray<UnlockResult>(1, Allocator.Persistent);
            readonly JobHandle m_jobHandle = new JobHandle();

            public bool IsCompleted => m_jobHandle.IsCompleted;

            public UnlockResult Result
            {
                get
                {
                    if (!m_jobHandle.IsCompleted)
                        throw new InvalidOperationException("Job is not completed yet");
                    m_jobHandle.Complete(); // to satisfy safety checks
                    return m_Result[0];
                }
            }

            // Schedule the job
            public Unlocker()
            {
                UserInfo? userInfo = GetUserInfo();
                if (!userInfo.HasValue)
                {
                    m_Result[0] = new UnlockResult { Value = UnlockResult.Status.NoUser };
                    return;
                }

                var authInfo = new AuthInfo(userInfo.Value);
                if (UnityEngine.PlayerPrefs.HasKey("Havok.Auth.Token") && UnityEngine.PlayerPrefs.HasKey("Havok.Auth.TokenExpiry"))
                {
                    try
                    {
                        authInfo.HavokToken = Convert.ToUInt64(UnityEngine.PlayerPrefs.GetString("Havok.Auth.Token"));
                        authInfo.HavokTokenExpiry = Convert.ToInt64(UnityEngine.PlayerPrefs.GetString("Havok.Auth.TokenExpiry"));
                        authInfo.HavokSubscriptionExpiry = Convert.ToInt64(UnityEngine.PlayerPrefs.GetString("Havok.Auth.SubscriptionExpiry"));
                    }
                    catch (FormatException)
                    {
                        // The saved prefs don't match what we expect, just ignore them
                    }
                }

                m_AuthInfo[0] = authInfo;
                m_Result[0] = new UnlockResult { Value = UnlockResult.Status.Invalid };
                m_jobHandle = new UnlockJob() { AuthInfo = m_AuthInfo, Result = m_Result }.Schedule();
            }

            public void Complete()
            {
                m_jobHandle.Complete();
            }

            // Clean up
            public void Dispose()
            {
                // TODO: Cancel the job instead of waiting for it (avoid stalling due to network issues)
                m_jobHandle.Complete();
                if (m_Result[0].Value == UnlockResult.Status.Unlocked)
                {
                    // Store the new token
                    UnityEngine.PlayerPrefs.SetString("Havok.Auth.Token", Convert.ToString(m_AuthInfo[0].HavokToken));
                    UnityEngine.PlayerPrefs.SetString("Havok.Auth.TokenExpiry", Convert.ToString(m_AuthInfo[0].HavokTokenExpiry));
                    UnityEngine.PlayerPrefs.SetString("Havok.Auth.SubscriptionExpiry", Convert.ToString(m_AuthInfo[0].HavokSubscriptionExpiry));
                }
                m_AuthInfo.Dispose();
                m_Result.Dispose();
            }

            struct UnlockJob : IJob
            {
                public NativeArray<AuthInfo> AuthInfo;
                public NativeArray<UnlockResult> Result;

                public void Execute()
                {
                    AuthInfo info = AuthInfo[0];
                    int unlockResult = HP_UnlockPlugin(ref info);
                    Result[0] = new UnlockResult
                    {
                        Value = unlockResult < 0 ? UnlockResult.Status.NoSubscription : UnlockResult.Status.Unlocked,
                        NumDaysRemaining = unlockResult
                    };

                    AuthInfo[0] = info;
                }
            }
        }

        // Polls the login state and raise callbacks whenever a user logs in to or out of Unity.
        // This is necessary because Unity does not provide that callback itself.
        class UserLoginMonitor
        {
            // Whether a user is currently logged in
            public bool UserLoggedIn { get; private set; } = false;

            // A callback fired after a user logs in or out
            public event EventHandler<bool> UserLoginStateChanged;

            // Throttling
            int m_numChecksSoFar = 0;
            int m_framesToNextCheck = 1;

            public UserLoginMonitor()
            {
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }

            ~UserLoginMonitor()
            {
                UnityEditor.EditorApplication.update -= OnEditorUpdate;
            }

            void OnEditorUpdate()
            {
                // For the first minute, check every 4s to see if the initial login completes
                // After that, check every 30s
                m_framesToNextCheck--;
                if (m_framesToNextCheck == 0)
                {
                    m_numChecksSoFar++;

                    var editorAssembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.EditorWindow));
                    Type unityConnectType = editorAssembly.GetType("UnityEditor.Connect.UnityConnect");
                    object unityConnect = unityConnectType.GetProperty("instance").GetValue(null, null);

                    bool isLoggedInProp = (bool)unityConnectType.GetProperty("loggedIn").GetValue(unityConnect, null);

                    object userInfo = unityConnectType.GetProperty("userInfo").GetValue(unityConnect, null);
                    Type userInfoType = userInfo.GetType();

                    // The loggedIn property changes before the access token is available. For our purposes we consider
                    // the user logged in when both are available
                    string accessToken = userInfoType.GetProperty("accessToken").GetValue(userInfo, null) as string;

                    bool isLoggedIn = isLoggedInProp && ((accessToken ?? "").Length > 0);

                    if (isLoggedIn != UserLoggedIn)
                    {
                        UserLoggedIn = isLoggedIn;
                        UserLoginStateChanged?.Invoke(this, UserLoggedIn);
                    }

                    // Initially we check quite frequently, after a while we taper off
                    if (m_numChecksSoFar < 100)
                    {
                        // One second approximately
                        m_framesToNextCheck = 100;
                    }
                    else
                    {
                        // 10 seconds approximately
                        m_framesToNextCheck = 1000;
                    }
                }
            }
        }

        // This is created when this C# DLL is loaded by the editor
        [UnityEditor.InitializeOnLoad]
        class EditorInitialization
        {
            //static UserLoginMonitor s_userLoginMonitor;

            static EditorInitialization()
            {
                // Need to wait until the editor has finished initializing
                UnityEditor.EditorApplication.update += InitializePlugin;
            }

            static unsafe void InitializePlugin()
            {
                // Check if the shared types are compatible with the plugin
                {
                    TypeCheckInformation typeHashes;
                    typeHashes.m_numTypes = 6;
                    typeHashes.m_typeCheckValues[0] = TypeHasher.CalculateTypeCheckValue(typeof(HavokConfiguration));
                    typeHashes.m_typeCheckValues[1] = TypeHasher.CalculateTypeCheckValue(typeof(HavokSimulation.StepInput));
                    typeHashes.m_typeCheckValues[2] = TypeHasher.CalculateTypeCheckValue(typeof(HavokSimulation.StepContext));
                    typeHashes.m_typeCheckValues[3] = TypeHasher.CalculateTypeCheckValue(typeof(HavokSimulation.Task));
                    typeHashes.m_typeCheckValues[4] = TypeHasher.CalculateTypeCheckValue(typeof(AuthInfo));
                    typeHashes.m_typeCheckValues[5] = TypeHasher.CalculateTypeCheckValue(typeof(Material));

                    // Populate struct sizes and field offsets
                    int fieldOffsetIndex = 0;
                    int structSizeIndex = 0;
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpIntArray);
                    fieldOffsetIndex = HpIntArray.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    // We don't care about sizes of HpBlockStream and HpBlockStream.Block
                    fieldOffsetIndex = HpBlockStream.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    fieldOffsetIndex = HpBlockStream.Block.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpManifoldStreamHeader);
                    fieldOffsetIndex = HpManifoldStreamHeader.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpManifold);
                    fieldOffsetIndex = HpManifold.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HPManifoldCollisionCache);
                    fieldOffsetIndex = HPManifoldCollisionCache.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpJacAngular);
                    fieldOffsetIndex = HpJacAngular.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpJac3dFriction);
                    fieldOffsetIndex = HpJac3dFriction.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);                    
                    // Only size is important for HpJacModHeader (fields are not used)
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpJacModHeader);
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpJacHeader);
                    fieldOffsetIndex = HpJacHeader.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    // HpCsContactJacRange also covers HpLinkedRange and HpBlockStreamRange
                    typeHashes.m_structSizes[structSizeIndex++] = sizeof(HpCsContactJacRange);
                    fieldOffsetIndex = HpCsContactJacRange.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);
                    // We don't care about the size of HpGrid
                    fieldOffsetIndex = HpGrid.FillFieldOffsetsArray(fieldOffsetIndex, typeHashes.m_fieldOffsets, TypeCheckInformation.fieldOffsetsLength);

                    bool isCompatible = false;
                    try
                    {
                        isCompatible = HP_CheckCompatibility(ref typeHashes);
                        UnityEditor.EditorApplication.update -= InitializePlugin;
                    }
                    catch (DllNotFoundException)
                    {
                        // Try again during next editor update; DLL copy might be in progress
                        return;
                    }

                    if (!isCompatible)
                    {
                        throw new InvalidOperationException("Your Havok.Physics plugin is incompatible with this version");
                    }
                }

                // Try to unlock the plugin in the background whenever a user has logged in (including during startup)
                //s_userLoginMonitor = new UserLoginMonitor();
                //s_userLoginMonitor.UserLoginStateChanged += (object o, bool newLoginState) =>
                //{
                //    if (newLoginState)
                //    {
                //        TryUnlockAsync();
                //    }
                //};
            }
        }
        
        public static void Configure(string key, string value)
        {
            unsafe
            {
                fixed (char* k = key)
                {
                    fixed (char* v = value)
                    {
                        HP_Configure(k, v);
                    }
                }
            }
        }

#endif
        }
}
