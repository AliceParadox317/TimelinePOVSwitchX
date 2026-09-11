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

        [HarmonyPatch(typeof(Timeline.Timeline), "Play")]
        [HarmonyPrefix]
        private static void TimelinePlayPrefix()
        {
            // A Play after Pause resumes the SAME Timeline POV session.
            // Only a fresh session is allowed to take a new snapshot.
            if (
                !povSettingsSessionActive &&
                TimelineContainsPerspectiveXSettingChanges()
            )
            {
                if (CreatePerspectiveXBackup())
                {
                    povSettingsSessionActive = true;
                }
            }
        }



        [HarmonyPatch(typeof(Timeline.Timeline), "Play")]
        [HarmonyPostfix]
        private static void TimelinePlayPostfix()
        {
            timelinePlaying = true;

            previousPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();
        }



        // =========================================================
        // TIMELINE PAUSE
        // =========================================================

        [HarmonyPatch(typeof(Timeline.Timeline), "Pause")]
        [HarmonyPostfix]
        private static void TimelinePausePostfix()
        {
            timelinePlaying = false;

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

            previousPlaybackTime =
                TimelineCompatibility.GetPlaybackTime();

            DeleteActivePovMirror();

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
            RestorePerspectiveXBackupIfPending();

            povSettingsSessionActive = false;
            timelinePlaying = false;
            previousPlaybackTime = -1f;
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
                        "POVSwitch"
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


                        bool crossedBackward =
                            previousPlaybackTime > keyframeTime &&
                            keyframeTime >= currentPlaybackTime;


                        if (
                            !crossedForward &&
                            !crossedBackward
                        )
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
