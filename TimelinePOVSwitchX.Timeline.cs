using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI.Utilities;
using RuntimeUnityEditor.Core.Utils;
using Studio;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Timeline;
using UnityEngine;
using UnityEngine.UI;

namespace TimelinePOVSwitchX
{
    public partial class TimelinePOVSwitchX
    {

        // =========================================================
        // TIMELINE PLAY
        // =========================================================

        private static bool playCallWasAlreadyPlaying = false;


        [HarmonyPatch(typeof(Timeline.Timeline), "Play")]
        [HarmonyPrefix]
        private static void TimelinePlayPrefix()
        {
            // Capture Timeline's REAL state before Play() changes anything.
            // Timeline.Play() calls Pause() internally when already playing.
            playCallWasAlreadyPlaying =
                Timeline.Timeline.isPlaying;
        }


        [HarmonyPatch(typeof(Timeline.Timeline), "Play")]
        [HarmonyPostfix]
        private static void TimelinePlayPostfix()
        {
            float currentPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();

            // If Play() began while Timeline was already playing, this call
            // was only the Play-button's pause toggle. Pause() has already
            // run and its postfix has already released the camera locks.
            if (playCallWasAlreadyPlaying)
            {
                timelinePlaying = false;
                timelinePaused = true;

                previousPlaybackTime =
                    currentPlaybackTime;

                return;
            }

            // Otherwise this really was Start/Resume.
            bool resumedFromPause =
                timelinePaused;

            timelinePlaying = true;
            timelinePaused = false;

            if (resumedFromPause)
            {
                ReapplyPovStateAtTime(
                    currentPlaybackTime
                );
            }

            previousPlaybackTime =
                currentPlaybackTime;
        }



        // =========================================================
        // TIMELINE PAUSE
        // =========================================================

        [HarmonyPatch(typeof(Timeline.Timeline), "Pause")]
        [HarmonyPostfix]
        private static void TimelinePausePostfix()
        {
            timelinePlaying = false;
            timelinePaused = true;

            // Pausing releases captured camera control immediately so the
            // Studio camera can be moved/scrubbed normally while paused.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            previousPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();
        }



        // =========================================================
        // TIMELINE STOP
        // =========================================================

        [HarmonyPatch(typeof(Timeline.Timeline), "Stop")]
        [HarmonyPostfix]
        private static void TimelineStopPostfix()
        {
            timelinePlaying = false;
            timelinePaused = false;

            previousPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();

            DeleteActivePovMirror();

            // A capture lock belongs only to the current Timeline POV session.
            // Do not let it survive Stop and affect later manual POV use.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            RestorePerspectiveXBackupIfPending();
            povSettingsSessionActive = false;
        }



        // =========================================================
        // STUDIO SCENE LOAD
        // =========================================================

        [HarmonyPatch(typeof(Studio.SceneInfo), "Load", new System.Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static void StudioSceneLoadPrefix(string _path)
        {
            // Loading another scene without pressing Timeline Stop abandons
            // the old Timeline POV session, so remove the temporary mirror
            // and give the user's settings back before the new scene is loaded.
            DeleteActivePovMirror();

            // The old scene's captured camera transform must never leak into
            // the scene being loaded. The PerspectiveX render postfix remains
            // installed globally, so clear both runtime lock flags here.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            RestorePerspectiveXBackupIfPending();

            povSettingsSessionActive = false;
            timelinePlaying = false;
            timelinePaused = false;
            previousPlaybackTime = -1f;
        }



        // =========================================================
        // REBUILD POV STATE WHEN RESUMING AFTER PAUSE
        // =========================================================

        private static void ReapplyPovStateAtTimeIfCharaMatches(
            ChaControl enabledCharacter
        )
        {
            try
            {
                if (enabledCharacter == null)
                    return;

                float currentPlaybackTime =
                    TimelineCompatibility.GetPlaybackTime();

                Timeline.Keyframe applicableKeyframe =
                    GetApplicablePovKeyframeAtTime(
                        currentPlaybackTime
                    );

                if (applicableKeyframe == null)
                    return;

                string storedValue =
                    applicableKeyframe.value as string;

                if (string.IsNullOrEmpty(storedValue))
                    return;

                string[] storedParts =
                    storedValue.Split('|');

                int timelineSceneId;

                if (
                    storedParts.Length == 0 ||
                    !int.TryParse(
                        storedParts[0],
                        out timelineSceneId
                    ) ||
                    timelineSceneId < 0
                )
                {
                    return;
                }

                ObjectCtrlInfo obj;

                if (
                    !Studio.Studio.Instance.dicObjectCtrl.TryGetValue(
                        timelineSceneId,
                        out obj
                    )
                )
                {
                    return;
                }

                OCIChar timelineCharacter =
                    obj as OCIChar;

                if (timelineCharacter == null)
                    return;

                // Exact identity match:
                // PerspectiveX.chara is the ChaControl assigned by EnablePov().
                // OCIChar.charInfo is that Studio character's ChaControl.
                if (
                    !object.ReferenceEquals(
                        timelineCharacter.charInfo,
                        enabledCharacter
                    )
                )
                {
                    return;
                }

                string viewMode =
                    storedParts.Length > 1
                        ? storedParts[1]
                        : "none";

                SwitchPOV(
                    timelineSceneId,
                    viewMode,
                    storedParts
                );
            }
            catch
            {
            }
        }


        private static Timeline.Keyframe GetApplicablePovKeyframeAtTime(
            float currentPlaybackTime
        )
        {
            if (_timeline == null)
            {
                _timeline =
                    Singleton<Timeline.Timeline>.Instance;

                if (_timeline == null)
                    return null;
            }

            Dictionary<int, Interpolable> interpolables =
                (Dictionary<int, Interpolable>)
                _timeline.GetPrivate(
                    "_interpolables"
                );

            Timeline.Keyframe applicableKeyframe =
                null;

            float applicableTime =
                float.MinValue;

            foreach (
                Interpolable interpolable
                in interpolables.Values
            )
            {
                if (
                    interpolable.id !=
                    "POVSwitch" ||
                    !interpolable.enabled
                )
                {
                    continue;
                }

                foreach (
                    KeyValuePair<float, Timeline.Keyframe> pair
                    in interpolable.keyframes
                )
                {
                    if (
                        pair.Key <= currentPlaybackTime &&
                        pair.Key >= applicableTime
                    )
                    {
                        applicableTime =
                            pair.Key;

                        applicableKeyframe =
                            pair.Value;
                    }
                }
            }

            return applicableKeyframe;
        }



        private static void ReapplyPovStateAtTime(
            float currentPlaybackTime
        )
        {
            // Start unlocked. If there is no POV Switch keyframe at or
            // before the current timestamp, no old capture lock should return.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            Timeline.Keyframe applicableKeyframe =
                GetApplicablePovKeyframeAtTime(
                    currentPlaybackTime
                );

            if (applicableKeyframe == null)
                return;

            string storedValue =
                applicableKeyframe.value as string;

            if (string.IsNullOrEmpty(storedValue))
                return;

            string[] storedParts =
                storedValue.Split('|');

            int sceneId;

            if (
                !int.TryParse(
                    storedParts[0],
                    out sceneId
                )
            )
            {
                return;
            }

            string viewMode =
                storedParts.Length > 1
                    ? storedParts[1]
                    : "none";

            SwitchPOV(
                sceneId,
                viewMode,
                storedParts
            );
        }


        // =========================================================
        // CHECK WHEN TIMELINE CROSSES A POV KEYFRAME
        // =========================================================

        [HarmonyPatch(typeof(Timeline.Timeline), "UpdateCursor2")]
        [HarmonyPostfix]
        private static void TimelineUpdateCursorPostfix()
        {
            if (_timeline == null)
            {
                _timeline =
                    Singleton<Timeline.Timeline>.Instance;

                if (_timeline == null)
                    return;
            }


            float currentPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();

            float duration =
                TimelineCompatibility.GetDuration();


            if (!timelinePlaying)
            {
                previousPlaybackTime =
                    currentPlaybackTime;

                return;
            }


            const float LOOP_EPSILON = 0.5f;


            bool isLoopWrap =
                previousPlaybackTime > currentPlaybackTime &&
                previousPlaybackTime >= duration - LOOP_EPSILON &&
                currentPlaybackTime <= LOOP_EPSILON;


            // When Timeline moves backwards, the keyframe we crossed is
            // NOT necessarily the state that should remain active. Rebuild
            // from the latest POV Switch keyframe at/before the destination.
            //
            // Example:
            //   0.01s = Direction Lock ON
            //   13.00s = another POV event
            //   seek 15s -> 12s
            // The correct state at 12s is the 0.01s keyframe, not the 13s one.
            if (
                !isLoopWrap &&
                previousPlaybackTime > currentPlaybackTime
            )
            {
                ReapplyPovStateAtTime(
                    currentPlaybackTime
                );

                previousPlaybackTime =
                    currentPlaybackTime;

                return;
            }


            if (!isLoopWrap)
            {
                Dictionary<int, Interpolable> interpolables =
                    (Dictionary<int, Interpolable>)
                    _timeline.GetPrivate(
                        "_interpolables"
                    );


                foreach (
                    Interpolable interpolable
                    in interpolables.Values
                )
                {
                    if (
                    interpolable.id !=
                    "POVSwitch" ||
                    !interpolable.enabled
                )
                {
                    continue;
                }


                    foreach (
                        KeyValuePair<float, Timeline.Keyframe> pair
                        in interpolable.keyframes
                    )
                    {
                        float keyframeTime =
                            pair.Key;


                        bool crossedForward =
                            previousPlaybackTime < keyframeTime &&
                            keyframeTime <= currentPlaybackTime;


                        if (!crossedForward)
                        {
                            continue;
                        }


                        string storedValue =
                            pair.Value.value as string;


                        if (string.IsNullOrEmpty(storedValue))
                            continue;


                        string[] storedParts =
                            storedValue.Split('|');


                        int sceneId;


                        if (
                            int.TryParse(
                                storedParts[0],
                                out sceneId
                            )
                        )
                        {
                            string viewMode =
                                storedParts.Length > 1
                                    ? storedParts[1]
                                    : "none";


                            SwitchPOV(
                                sceneId,
                                viewMode,
                                storedParts
                            );
                        }
                    }


                    break;
                }
            }


            previousPlaybackTime =
                currentPlaybackTime;
        }



        // =========================================================
        // START
        // =========================================================


        private static string ReadPovValueFromXml(
            string parameter,
            XmlNode node
        )
        {
            if (
                node == null ||
                node.Attributes == null ||
                node.Attributes["value"] == null
            )
            {
                return "";
            }


            return node.Attributes["value"].Value;
        }



        private static void WritePovValueToXml(
            string parameter,
            XmlTextWriter writer,
            string value
        )
        {
            if (writer == null)
                return;


            writer.WriteAttributeString(
                "value",
                value ?? ""
            );
        }



        // =========================================================
        // TIMELINE COMPATIBILITY
        // =========================================================

        private static bool IsCompatible(
            ObjectCtrlInfo oci
        )
        {
            return true;
        }



        // =========================================================
        // DEFAULT VALUE FOR NEW KEYFRAME
        // =========================================================

        private static string GetValue(
            ObjectCtrlInfo oci,
            string parameter
        )
        {
            int lowestSceneId =
                int.MaxValue;


            bool foundCharacter =
                false;


            foreach (
                KeyValuePair<int, ObjectCtrlInfo> pair
                in Studio.Studio.Instance.dicObjectCtrl
            )
            {
                OCIChar character =
                    pair.Value as OCIChar;


                if (character == null)
                    continue;


                if (
                    pair.Key <
                    lowestSceneId
                )
                {
                    lowestSceneId =
                        pair.Key;


                    foundCharacter =
                        true;
                }
            }


            if (!foundCharacter)
                return "";


            return lowestSceneId.ToString() + "|none|force1|mirror0";
        }



    }
}
