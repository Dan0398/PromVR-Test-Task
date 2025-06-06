#if AR_FOUNDATION_5||AR_FOUNDATION_6
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Unity.XR.PXR
{
    public class PICOAnchorSubsystem : XRAnchorSubsystem
    {
        internal const string k_SubsystemId = "PICOAnchorSubsystem";

        class AnchorProvider : Provider
        {
            private Dictionary<TrackableId, ulong> trackableIdToHandleMap;
            private Dictionary<ulong, XRAnchor> handleToXRAnchorMap;
            private HashSet<ulong> managedAnchorHandles;
            private Dictionary<Guid, ulong> lastAnchorToTime;
            private bool isInit = false;

            public override void Start()
            {
                StartSpatialAnchorProvider();
            }

            private async void StartSpatialAnchorProvider()
            {
                var result = await PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor);
                if (result == PxrResult.SUCCESS)
                {
                    if (!isInit)
                    {
                        trackableIdToHandleMap = new Dictionary<TrackableId, ulong>();
                        handleToXRAnchorMap = new Dictionary<ulong, XRAnchor>();
                        managedAnchorHandles = new HashSet<ulong>();
                        isInit = true;
                    }
                }
                else
                {
                    Debug.LogError("Spatial Anchor Provider Start Failed:" + result);
                }
            }

            public override void Stop()
            {
                var result = PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.SpatialAnchor);
                if (result == PxrResult.SUCCESS)
                {

                }
                else
                {
                    Debug.LogError("Spatial Anchor Provider Stop Failed:" + result);
                }
            }

            public override void Destroy()
            {
                
            }

            public override TrackableChanges<XRAnchor> GetChanges(XRAnchor defaultAnchor, Allocator allocator)
            {
                return new TrackableChanges<XRAnchor>();
            }

            public override bool TryAddAnchor(Pose pose, out XRAnchor anchor)
            {
                var tcs = new TaskCompletionSource<(PxrResult result, ulong anchorHandle, Guid uuid)>();
                var tcs2 = new TaskCompletionSource<PxrResult>();
                Task.Run(() =>
                {
                    var (pxrResult, handle, guid) = PXR_MixedReality.CreateSpatialAnchorAsync(pose.position, pose.rotation).Result;

                    tcs.SetResult((pxrResult, handle, guid));
                });
                var (result, anchorHandle, uuid) = tcs.Task.Result;
                if (result == PxrResult.SUCCESS)
                {
                    Task.Run(() =>
                    {
                        var pxrResult = PXR_MixedReality.PersistSpatialAnchorAsync(anchorHandle).Result;

                        tcs2.SetResult(pxrResult);
                    });

                    var result2 = tcs2.Task.Result;
                    if (result2 == PxrResult.SUCCESS)
                    {
                        var bytes = uuid.ToByteArray();
                        var trackabledId = new TrackableId(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
                        var nativePtr = new IntPtr((long)anchorHandle);
                        anchor = new XRAnchor(trackabledId, pose, TrackingState.Tracking, nativePtr);
                        trackableIdToHandleMap[trackabledId] = anchorHandle;
                        handleToXRAnchorMap[anchorHandle] = anchor;
                        return true;
                    }
                    else
                    {
                        anchor = XRAnchor.defaultValue;
                        return false;
                    }
                }
                else
                {
                    anchor = XRAnchor.defaultValue;
                    return false;
                }
            }

            public async Task<bool> QuerySpatialAnchors()
            {
                return false;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId)
            {
                var anchorHandle = trackableIdToHandleMap[anchorId];
                var result = PXR_MixedReality.DestroyAnchor(anchorHandle);
                if (result == PxrResult.SUCCESS)
                {
                    var tcs = new TaskCompletionSource<PxrResult>();
                    Task.Run(() =>
                    {
                        var pxrResult = PXR_MixedReality.UnPersistSpatialAnchorAsync(anchorHandle).Result;

                        tcs.SetResult(pxrResult);
                    });
                    var result1 = tcs.Task.Result;
                    if (result1 == PxrResult.SUCCESS)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            var cInfo = new XRAnchorSubsystemDescriptor.Cinfo()
            {
                id = k_SubsystemId,
                providerType = typeof(AnchorProvider),
                subsystemTypeOverride = typeof(PICOAnchorSubsystem),
                supportsTrackableAttachments = false
            };
     
#if AR_FOUNDATION_6
            XRAnchorSubsystemDescriptor.Register(cInfo);
#elif AR_FOUNDATION_5
            XRAnchorSubsystemDescriptor.Create(cInfo);
#endif
        }
    }
}
#endif
