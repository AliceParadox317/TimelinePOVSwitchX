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
        private static bool TimelineContainsPerspectiveXSettingChanges()
        {
            try
            {
                if (_timeline == null)
                {
                    _timeline =
                        Singleton<Timeline.Timeline>.Instance;

                    if (_timeline == null)
                        return false;
                }


                Dictionary<int, Interpolable> interpolables =
                    (Dictionary<int, Interpolable>)
                    _timeline.GetPrivate(
                        "_interpolables"
                    );


                if (interpolables == null)
                    return false;


                foreach (Interpolable interpolable in interpolables.Values)
                {
                    if (interpolable.id != "POVSwitch")
                        continue;


                    foreach (
                        KeyValuePair<float, Timeline.Keyframe> pair
                        in interpolable.keyframes
                    )
                    {
                        string storedValue =
                            pair.Value.value as string;


                        if (string.IsNullOrEmpty(storedValue))
                            continue;


                        string[] parts =
                            storedValue.Split('|');


                        string viewMode =
                            parts.Length > 1
                                ? parts[1]
                                : "none";


                        if (
                            viewMode == "slot1" ||
                            viewMode == "slot2" ||
                            viewMode == "slot3" ||
                            viewMode == "custom"
                        )
                        {
                            return true;
                        }
                    }


                    break;
                }
            }
            catch
            {
            }


            return false;
        }



        private static bool CreatePerspectiveXBackup()
        {
            try
            {
                float fov;
                bool hideHead;
                bool alignWithBody;
                float headSway;
                float pitchLimit;
                float positionSmoothing;
                float forwardOffset;
                float nearClip;
                float upOffset;


                if (
                    !ReadPerspectiveXSettings(
                        out fov,
                        out hideHead,
                        out alignWithBody,
                        out headSway,
                        out pitchLimit,
                        out positionSmoothing,
                        out forwardOffset,
                        out nearClip,
                        out upOffset
                    )
                )
                {
                    return false;
                }


                string backupText =
                    "[Recovery]\n" +
                    "Pending=true\n" +
                    "DefaultFov=" + FloatText(fov) + "\n" +
                    "HideHead=" + (hideHead ? "true" : "false") + "\n" +
                    "AlignWithBody=" + (alignWithBody ? "true" : "false") + "\n" +
                    "HeadSway=" + FloatText(headSway) + "\n" +
                    "PitchLimit=" + FloatText(pitchLimit) + "\n" +
                    "PositionSmoothing=" + FloatText(positionSmoothing) + "\n" +
                    "ForwardOffset=" + FloatText(forwardOffset) + "\n" +
                    "NearClip=" + FloatText(nearClip) + "\n" +
                    "UpOffset=" + FloatText(upOffset) + "\n";


                System.IO.File.WriteAllText(
                    perspectiveXBackupPath,
                    backupText
                );


                return true;
            }
            catch (System.Exception)
            {

                return false;
            }
        }



        private static void RestorePerspectiveXBackupIfPending()
        {
            try
            {
                if (!System.IO.File.Exists(perspectiveXBackupPath))
                    return;


                string[] lines =
                    System.IO.File.ReadAllLines(
                        perspectiveXBackupPath
                    );


                Dictionary<string, string> values =
                    new Dictionary<string, string>();


                foreach (string rawLine in lines)
                {
                    if (rawLine == null)
                        continue;


                    string line =
                        rawLine.Trim();


                    if (
                        line.Length == 0 ||
                        line.StartsWith("[") ||
                        line.StartsWith("#") ||
                        line.StartsWith(";")
                    )
                    {
                        continue;
                    }


                    int equalsIndex =
                        line.IndexOf('=');


                    if (equalsIndex <= 0)
                        continue;


                    string key =
                        line.Substring(0, equalsIndex).Trim();

                    string value =
                        line.Substring(equalsIndex + 1).Trim();


                    values[key] = value;
                }


                string pendingText;


                if (
                    !values.TryGetValue(
                        "Pending",
                        out pendingText
                    ) ||
                    !string.Equals(
                        pendingText,
                        "true",
                        System.StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }


                BepInEx.PluginInfo pluginInfo;


                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }


                object plugin =
                    pluginInfo.Instance;

                System.Type pluginType =
                    plugin.GetType();


                float fov;
                bool hideHead;
                bool alignWithBody;
                float headSway;
                float pitchLimit;
                float positionSmoothing;
                float forwardOffset;
                float nearClip;
                float upOffset;


                if (
                    !TryGetBackupFloat(values, "DefaultFov", out fov) ||
                    !TryGetBackupBool(values, "HideHead", out hideHead) ||
                    !TryGetBackupBool(values, "AlignWithBody", out alignWithBody) ||
                    !TryGetBackupFloat(values, "HeadSway", out headSway) ||
                    !TryGetBackupFloat(values, "PitchLimit", out pitchLimit) ||
                    !TryGetBackupFloat(values, "PositionSmoothing", out positionSmoothing) ||
                    !TryGetBackupFloat(values, "ForwardOffset", out forwardOffset) ||
                    !TryGetBackupFloat(values, "NearClip", out nearClip) ||
                    !TryGetBackupFloat(values, "UpOffset", out upOffset)
                )
                {

                    return;
                }


                SetPerspectiveXSetting(plugin, pluginType, "DefaultFov", fov);
                SetPerspectiveXSetting(plugin, pluginType, "HideHead", hideHead);
                SetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", alignWithBody);
                SetPerspectiveXSetting(plugin, pluginType, "HeadSway", headSway);
                SetPerspectiveXSetting(plugin, pluginType, "PitchLimit", pitchLimit);
                SetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", positionSmoothing);
                SetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", forwardOffset);
                SetPerspectiveXSetting(plugin, pluginType, "NearClip", nearClip);
                SetPerspectiveXSetting(plugin, pluginType, "UpOffset", upOffset);


                // Deliberately DO NOT touch PerspectiveX's povEnabled state.


                string restoredText =
                    "[Recovery]\n" +
                    "Pending=false\n" +
                    "DefaultFov=" + FloatText(fov) + "\n" +
                    "HideHead=" + (hideHead ? "true" : "false") + "\n" +
                    "AlignWithBody=" + (alignWithBody ? "true" : "false") + "\n" +
                    "HeadSway=" + FloatText(headSway) + "\n" +
                    "PitchLimit=" + FloatText(pitchLimit) + "\n" +
                    "PositionSmoothing=" + FloatText(positionSmoothing) + "\n" +
                    "ForwardOffset=" + FloatText(forwardOffset) + "\n" +
                    "NearClip=" + FloatText(nearClip) + "\n" +
                    "UpOffset=" + FloatText(upOffset) + "\n";


                System.IO.File.WriteAllText(
                    perspectiveXBackupPath,
                    restoredText
                );
            }
            catch (System.Exception)
            {
            }
        }



        private static bool TryGetBackupFloat(
            Dictionary<string, string> values,
            string key,
            out float result
        )
        {
            result = 0f;

            string text;


            if (!values.TryGetValue(key, out text))
                return false;


            return float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result
            );
        }



        private static bool TryGetBackupBool(
            Dictionary<string, string> values,
            string key,
            out bool result
        )
        {
            result = false;

            string text;


            if (!values.TryGetValue(key, out text))
                return false;


            return bool.TryParse(
                text,
                out result
            );
        }



        private static bool ReadPerspectiveXSettings(
            out float fov,
            out bool hideHead,
            out bool alignWithBody,
            out float headSway,
            out float pitchLimit,
            out float positionSmoothing,
            out float forwardOffset,
            out float nearClip,
            out float upOffset
        )
        {
            fov = 60f;
            hideHead = false;
            alignWithBody = true;
            headSway = 1f;
            pitchLimit = 89f;
            positionSmoothing = 0f;
            forwardOffset = 0f;
            nearClip = 0.01f;
            upOffset = 0f;


            BepInEx.PluginInfo pluginInfo;


            if (
                !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                    "bucky.kk.perspectivex",
                    out pluginInfo
                )
            )
            {
                return false;
            }


            object plugin =
                pluginInfo.Instance;


            System.Type pluginType =
                plugin.GetType();


            object value;


            if (!GetPerspectiveXSetting(plugin, pluginType, "DefaultFov", out value))
                return false;
            fov = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "HideHead", out value))
                return false;
            hideHead = (bool)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", out value))
                return false;
            alignWithBody = (bool)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "HeadSway", out value))
                return false;
            headSway = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "PitchLimit", out value))
                return false;
            pitchLimit = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", out value))
                return false;
            positionSmoothing = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", out value))
                return false;
            forwardOffset = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "NearClip", out value))
                return false;
            nearClip = (float)value;

            if (!GetPerspectiveXSetting(plugin, pluginType, "UpOffset", out value))
                return false;
            upOffset = (float)value;


            return true;
        }



        private static bool GetPerspectiveXSetting(
            object plugin,
            System.Type pluginType,
            string settingName,
            out object value
        )
        {
            value = null;


            System.Reflection.PropertyInfo property =
                pluginType.GetProperty(
                    settingName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                );


            if (property == null)
                return false;


            object configEntry =
                property.GetValue(
                    plugin,
                    null
                );


            if (configEntry == null)
                return false;


            System.Reflection.PropertyInfo valueProperty =
                configEntry.GetType().GetProperty(
                    "Value"
                );


            if (valueProperty == null)
                return false;


            value =
                valueProperty.GetValue(
                    configEntry,
                    null
                );


            return value != null;
        }



        private static void SetPerspectiveXSetting(
            object plugin,
            System.Type pluginType,
            string settingName,
            object value
        )
        {
            System.Reflection.PropertyInfo property =
                pluginType.GetProperty(
                    settingName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                );


            if (property == null)
                return;


            object configEntry =
                property.GetValue(
                    plugin,
                    null
                );


            if (configEntry == null)
                return;


            System.Reflection.PropertyInfo valueProperty =
                configEntry.GetType().GetProperty(
                    "Value"
                );


            if (valueProperty == null)
                return;


            valueProperty.SetValue(
                configEntry,
                value,
                null
            );
        }



        private static bool TryReadCustomParts(
            string[] parts,
            out float fov,
            out bool hideHead,
            out bool alignWithBody,
            out float headSway,
            out float pitchLimit,
            out float positionSmoothing,
            out float forwardOffset,
            out float nearClip,
            out float upOffset
        )
        {
            fov = 0f;
            hideHead = false;
            alignWithBody = false;
            headSway = 0f;
            pitchLimit = 30f;
            positionSmoothing = 0f;
            forwardOffset = 0f;
            nearClip = 0.01f;
            upOffset = 0f;


            if (parts.Length < 11)
                return false;


            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out fov))
                return false;

            hideHead =
                parts[3] == "1";

            alignWithBody =
                parts[4] == "1";

            if (!float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out headSway))
                return false;

            if (!float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out pitchLimit))
                return false;

            if (!float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out positionSmoothing))
                return false;

            if (!float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out forwardOffset))
                return false;

            if (!float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out nearClip))
                return false;

            if (!float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out upOffset))
                return false;


            return true;
        }



        private static bool TryReadViewDirection(
            string[] parts,
            out float yaw,
            out float pitch,
            out float roll
        )
        {
            Quaternion ignoredRotation;
            Vector3 ignoredPosition;
            bool ignoredHasRotation;
            bool ignoredHasPosition;
            bool ignoredLockDirection;
            bool ignoredLockPosition;

            return TryReadViewDirection(
                parts,
                out yaw,
                out pitch,
                out roll,
                out ignoredRotation,
                out ignoredPosition,
                out ignoredHasRotation,
                out ignoredHasPosition,
                out ignoredLockDirection,
                out ignoredLockPosition
            );
        }



        private static bool TryReadViewDirection(
            string[] parts,
            out float yaw,
            out float pitch,
            out float roll,
            out Quaternion finalRotation,
            out Vector3 finalPosition,
            out bool hasFinalRotation,
            out bool hasFinalPosition,
            out bool lockDirection,
            out bool lockPosition
        )
        {
            yaw = 0f;
            pitch = 0f;
            roll = 0f;
            finalRotation = Quaternion.identity;
            finalPosition = Vector3.zero;
            hasFinalRotation = false;
            hasFinalPosition = false;
            lockDirection = false;
            lockPosition = false;

            if (parts == null)
                return false;

            foreach (string part in parts)
            {
                if (
                    !part.StartsWith(
                        "viewdir:",
                        System.StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }

                string[] values =
                    part.Split(':');

                // Legacy one-shot capture.
                if (values.Length != 4 &&
                    values.Length != 8 &&
                    values.Length < 13)
                {
                    return false;
                }

                if (
                    !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out yaw) ||
                    !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out pitch) ||
                    !float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out roll)
                )
                {
                    return false;
                }

                if (values.Length >= 8)
                {
                    float qx;
                    float qy;
                    float qz;
                    float qw;

                    if (
                        float.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out qx) &&
                        float.TryParse(values[5], NumberStyles.Float, CultureInfo.InvariantCulture, out qy) &&
                        float.TryParse(values[6], NumberStyles.Float, CultureInfo.InvariantCulture, out qz) &&
                        float.TryParse(values[7], NumberStyles.Float, CultureInfo.InvariantCulture, out qw)
                    )
                    {
                        finalRotation =
                            new Quaternion(qx, qy, qz, qw);

                        hasFinalRotation = true;

                        // Previous forced-direction format had no explicit
                        // lock flag, so preserve its old behavior.
                        if (values.Length == 8)
                            lockDirection = true;
                    }
                }

                if (values.Length >= 13)
                {
                    float px;
                    float py;
                    float pz;

                    if (
                        float.TryParse(values[8], NumberStyles.Float, CultureInfo.InvariantCulture, out px) &&
                        float.TryParse(values[9], NumberStyles.Float, CultureInfo.InvariantCulture, out py) &&
                        float.TryParse(values[10], NumberStyles.Float, CultureInfo.InvariantCulture, out pz)
                    )
                    {
                        finalPosition =
                            new Vector3(px, py, pz);

                        hasFinalPosition = true;
                    }

                    lockDirection =
                        values[11] == "1";

                    lockPosition =
                        values[12] == "1";
                }

                return true;
            }

            return false;
        }



        private static bool CapturePerspectiveXViewDirection(
            out float yaw,
            out float pitch,
            out float roll
        )
        {
            yaw = 0f;
            pitch = 0f;
            roll = 0f;

            try
            {
                BepInEx.PluginInfo pluginInfo;

                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return false;
                }

                object plugin = pluginInfo.Instance;

                if (plugin == null)
                    return false;

                System.Type pluginType = plugin.GetType();

                System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;

                System.Reflection.FieldInfo yawField =
                    pluginType.GetField("yaw", flags);

                System.Reflection.FieldInfo pitchField =
                    pluginType.GetField("pitch", flags);

                System.Reflection.FieldInfo rollField =
                    pluginType.GetField("manualRoll", flags);

                if (
                    yawField == null ||
                    pitchField == null ||
                    rollField == null
                )
                {
                    return false;
                }

                yaw = (float)yawField.GetValue(plugin);
                pitch = (float)pitchField.GetValue(plugin);
                roll = (float)rollField.GetValue(plugin);

                return true;
            }
            catch
            {
                return false;
            }
        }



        private static void ApplyPerspectiveXViewDirection(
            object plugin,
            System.Type pluginType,
            string[] parts
        )
        {
            // Every POV Switch event clears the previous camera locks first.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;

            float yaw;
            float pitch;
            float roll;
            Quaternion finalRotation;
            Vector3 finalPosition;
            bool hasFinalRotation;
            bool hasFinalPosition;
            bool lockDirection;
            bool lockPosition;

            if (
                !TryReadViewDirection(
                    parts,
                    out yaw,
                    out pitch,
                    out roll,
                    out finalRotation,
                    out finalPosition,
                    out hasFinalRotation,
                    out hasFinalPosition,
                    out lockDirection,
                    out lockPosition
                )
            )
            {
                return;
            }

            // Restore PerspectiveX's own look values once when the keyframe fires.
            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            System.Reflection.FieldInfo yawField =
                pluginType.GetField("yaw", flags);

            System.Reflection.FieldInfo pitchField =
                pluginType.GetField("pitch", flags);

            System.Reflection.FieldInfo rollField =
                pluginType.GetField("manualRoll", flags);

            if (
                yawField != null &&
                pitchField != null &&
                rollField != null
            )
            {
                yawField.SetValue(plugin, yaw);
                pitchField.SetValue(plugin, Mathf.Clamp(pitch, -89f, 89f));
                rollField.SetValue(plugin, roll);
            }

            if (lockDirection && hasFinalRotation)
            {
                forcedCameraRotation =
                    finalRotation;

                forceCapturedCameraDirection =
                    true;
            }

            if (lockPosition && hasFinalPosition)
            {
                forcedCameraPosition =
                    finalPosition;

                forceCapturedCameraPosition =
                    true;
            }
        }



        private static void ApplyForcedCameraLocks(
            Camera renderingCamera
        )
        {
            if (
                !forceCapturedCameraDirection &&
                !forceCapturedCameraPosition
            )
            {
                return;
            }

            if (renderingCamera == null)
                return;

            // PerspectiveX has already calculated and written this frame's
            // final POV transform. Override only the properties selected by
            // the Timeline keyframe immediately before the camera renders.
            if (forceCapturedCameraPosition)
            {
                renderingCamera.transform.position =
                    forcedCameraPosition;
            }

            if (forceCapturedCameraDirection)
            {
                renderingCamera.transform.rotation =
                    forcedCameraRotation;
            }
        }



        private static bool GetHeadFollowCamera(
            string[] parts
        )
        {
            if (parts == null)
                return false;

            foreach (string part in parts)
            {
                if (part == "headfollow1")
                    return true;

                if (part == "headfollow0")
                    return false;
            }

            return false;
        }


        private static float GetHeadFollowYawLimit(string[] parts)
        {
            if (parts != null) foreach (string part in parts)
            {
                if (part != null && part.StartsWith("hfyaw", System.StringComparison.OrdinalIgnoreCase))
                {
                    float v;
                    if (float.TryParse(part.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        return Mathf.Clamp(v, 5f, 89f);
                }
            }
            return 50f;
        }

        private static float GetHeadFollowPitchLimit(string[] parts)
        {
            if (parts != null) foreach (string part in parts)
            {
                if (part != null && part.StartsWith("hfpitch", System.StringComparison.OrdinalIgnoreCase))
                {
                    float v;
                    if (float.TryParse(part.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        return Mathf.Clamp(v, 5f, 80f);
                }
            }
            return 35f;
        }

        private static void RestoreHeadFollowRotation()
        {
            // No original animation bone is modified in pivot mode.
        }


        private static bool TryGetHeadFollowEyeMidpoint(ChaControl chara, out Vector3 midpoint)
        {
            midpoint = Vector3.zero;
            if (chara == null || chara.objHeadBone == null)
                return false;

            try
            {
                var eyeLookCtrl = chara.eyeLookCtrl;
                if (eyeLookCtrl != null && eyeLookCtrl.eyeLookScript != null)
                {
                    var eyeObjs = eyeLookCtrl.eyeLookScript.eyeObjs;
                    if (eyeObjs != null && eyeObjs.Length >= 2 &&
                        eyeObjs[0] != null && eyeObjs[1] != null &&
                        eyeObjs[0].eyeTransform && eyeObjs[1].eyeTransform)
                    {
                        midpoint = Vector3.Lerp(
                            eyeObjs[0].eyeTransform.position,
                            eyeObjs[1].eyeTransform.position,
                            0.5f
                        );
                        return true;
                    }
                }
            }
            catch
            {
            }

            Transform headT = chara.objHeadBone.transform;
            midpoint = headT.position +
                headT.rotation * new Vector3(0f, 0.06f, 0.08f);
            return true;
        }


        private static bool CreateHeadFollowPivot(ChaControl chara, Transform sourceBone)
        {
            if (chara == null || sourceBone == null)
                return false;

            GameObject pivotObject = new GameObject(
                "__TimelinePOVSwitchX_HeadFollowPivot"
            );
            pivotObject.hideFlags = HideFlags.HideAndDontSave;

            Transform pivot = pivotObject.transform;
            pivot.SetParent(sourceBone, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            // Move the existing visual/facial hierarchy under OUR transform while
            // preserving every child's world transform. p_cf_head_bone itself keeps
            // its original hierarchy/path and is never written by Head Follow.
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < sourceBone.childCount; i++)
            {
                Transform child = sourceBone.GetChild(i);
                if (child != null && child != pivot)
                    children.Add(child);
            }

            headFollowPivotChildren.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                if (child == null)
                    continue;

                child.SetParent(pivot, true);
                headFollowPivotChildren.Add(child);
            }

            headFollowPivot = pivot;
            return true;
        }


        private static void ClearHeadFollowCamera()
        {
            // Restore the original hierarchy exactly.  The source animation bone was
            // never changed, so disabling Head Follow does not need to restore a pose.
            if (headFollowPivot != null)
            {
                headFollowPivot.localPosition = Vector3.zero;
                headFollowPivot.localRotation = Quaternion.identity;
                headFollowPivot.localScale = Vector3.one;

                if (headFollowBone != null)
                {
                    for (int i = 0; i < headFollowPivotChildren.Count; i++)
                    {
                        Transform child = headFollowPivotChildren[i];
                        if (child != null && child.parent == headFollowPivot)
                            child.SetParent(headFollowBone, true);
                    }
                }

                if (headFollowPivot.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(headFollowPivot.gameObject);
            }

            headFollowPivotChildren.Clear();
            headFollowCameraActive = false;
            headFollowCharacter = null;
            headFollowBone = null;
            headFollowPivot = null;
            headFollowLastAppliedFrame = -1;
        }


        private static Transform FindHeadFollowBone(ChaControl chara)
        {
            if (chara == null) return null;
            Transform[] all = chara.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == "p_cf_head_bone")
                    return all[i];
            return null;
        }


        private static void SetHeadFollowCamera(OCIChar target, bool enabled, float yawLimit, float pitchLimit)
        {
            if (!enabled || target == null || target.charInfo == null)
            {
                if (headFollowCameraActive || headFollowPivot != null)
                    ClearHeadFollowCamera();
                return;
            }

            Transform bone = FindHeadFollowBone(target.charInfo);
            if (bone == null)
            {
                if (headFollowCameraActive || headFollowPivot != null)
                    ClearHeadFollowCamera();
                return;
            }

            headFollowYawLimit = Mathf.Clamp(yawLimit, 5f, 89f);
            headFollowPitchLimit = Mathf.Clamp(pitchLimit, 5f, 80f);

            // Re-evaluating the same Timeline POV keyframe changes no hierarchy and
            // performs no restore/reapply cycle.
            if (headFollowCameraActive &&
                headFollowCharacter == target.charInfo &&
                headFollowBone == bone &&
                headFollowPivot != null)
                return;

            if (headFollowCameraActive || headFollowPivot != null)
                ClearHeadFollowCamera();

            headFollowCharacter = target.charInfo;
            headFollowBone = bone;

            if (!CreateHeadFollowPivot(target.charInfo, bone))
            {
                headFollowCharacter = null;
                headFollowBone = null;
                return;
            }

            headFollowLastAppliedFrame = -1;
            headFollowCameraActive = true;
        }


        private static void ApplyHeadFollowCameraKKPEStyle()
        {
            if (!headFollowCameraActive || headFollowCharacter == null ||
                headFollowBone == null || headFollowPivot == null) return;

            if (headFollowLastAppliedFrame == Time.frameCount) return;
            headFollowLastAppliedFrame = Time.frameCount;

            Camera cam = Camera.main;
            if (cam == null || headFollowCharacter.objHeadBone == null) return;

            // Always begin from the neutral pivot.  This reveals the ONE pose that
            // Timeline/Animator/facial systems produced this frame.  We never restore
            // or rewrite any of their original bones.
            headFollowPivot.localPosition = Vector3.zero;
            headFollowPivot.localRotation = Quaternion.identity;
            headFollowPivot.localScale = Vector3.one;

            // Use the animated head/neck attachment joint as the rotation center.
            // This keeps the visual head physically attached to the neck instead of
            // orbiting around the eyes.  The animation bones remain read-only.
            Transform attachmentAnchor = headFollowBone;
            Transform scan = headFollowBone;
            while (scan != null)
            {
                if (scan.name == "cf_j_head")
                {
                    attachmentAnchor = scan;
                    break;
                }
                scan = scan.parent;
            }

            Transform headReference = headFollowCharacter.objHeadBone.transform;
            Vector3 headForward = headReference.forward;

            Quaternion observedRotation;
            float liveYaw, livePitch, liveRoll;
            if (CapturePerspectiveXViewDirection(out liveYaw, out livePitch, out liveRoll))
                observedRotation = Quaternion.Euler(livePitch, liveYaw, liveRoll);
            else
                observedRotation = cam.transform.rotation;

            Vector3 cameraForward = observedRotation * Vector3.forward;
            if (headForward.sqrMagnitude < 0.000001f ||
                cameraForward.sqrMagnitude < 0.000001f) return;

            Vector3 localLook =
                headReference.InverseTransformDirection(cameraForward.normalized);
            float yaw = Mathf.Atan2(localLook.x, localLook.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(
                Mathf.Clamp(localLook.y, -1f, 1f)
            ) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -headFollowYawLimit, headFollowYawLimit);
            pitch = Mathf.Clamp(pitch, -headFollowPitchLimit, headFollowPitchLimit);

            float yr = yaw * Mathf.Deg2Rad;
            float pr = pitch * Mathf.Deg2Rad;
            Vector3 limitedLocalLook = new Vector3(
                Mathf.Sin(yr) * Mathf.Cos(pr),
                Mathf.Sin(pr),
                Mathf.Cos(yr) * Mathf.Cos(pr)
            );
            Vector3 limitedWorldLook =
                headReference.TransformDirection(limitedLocalLook).normalized;

            Quaternion correction =
                Quaternion.FromToRotation(headForward, limitedWorldLook);

            // Apply the final visual offset to OUR transform only.  Rotate the
            // visual head hierarchy around cf_j_head (the animated head/neck
            // attachment joint), so the base of the head stays connected to the
            // neck.  We still never write cf_j_head, cf_j_neck, p_cf_head_bone,
            // or any other Timeline/Animator-controlled transform.
            Vector3 sourcePosition = headFollowBone.position;
            Quaternion sourceRotation = headFollowBone.rotation;
            Vector3 attachmentPoint = attachmentAnchor.position;
            headFollowPivot.position =
                attachmentPoint + correction * (sourcePosition - attachmentPoint);
            headFollowPivot.rotation = correction * sourceRotation;
        }


        private static void ApplyHeadFollowCameraForRender(Camera renderingCamera)
        {
            // Head Follow owns only its private pivot in LateUpdate.
            // No render callback writes a character pose.
        }


        private static void PerspectiveXDisablePovPrefix()
        {
            if (timelineCallingPerspectiveXLifecycle)
                return;

            // Manual PerspectiveX exit: release Timeline's transform ownership
            // BEFORE PerspectiveX restores the normal Studio camera.
            forceCapturedCameraDirection = false;
            forceCapturedCameraPosition = false;
            ClearHeadFollowCamera();
        }



        private static void PerspectiveXEnablePovPostfix(
            object __instance
        )
        {
            if (timelineCallingPerspectiveXLifecycle)
                return;

            try
            {
                if (__instance == null)
                    return;

                // PerspectiveX's actual EnablePov() stores the character it
                // successfully entered as its private `chara` ChaControl field.
                // Read that exact field AFTER EnablePov has completed instead
                // of trying to infer the target from Workspace selection.
                System.Reflection.FieldInfo charaField =
                    __instance.GetType().GetField(
                        "chara",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );

                if (charaField == null)
                    return;

                ChaControl enabledCharacter =
                    charaField.GetValue(__instance)
                    as ChaControl;

                if (enabledCharacter == null)
                    return;

                ReapplyPovStateAtTimeIfCharaMatches(
                    enabledCharacter
                );
            }
            catch
            {
            }
        }


        private static void PerspectiveXCameraPreCullPostfix(
            Camera renderingCam
        )
        {
            ApplyForcedCameraLocks(
                renderingCam
            );

            ApplyHeadFollowCameraForRender(
                renderingCam
            );
        }



        private static void TryPatchPerspectiveXLifecycle(
            Harmony harmony
        )
        {
            if (harmony == null)
                return;

            try
            {
                BepInEx.PluginInfo pluginInfo;

                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }

                object plugin =
                    pluginInfo.Instance;

                if (plugin == null)
                    return;

                System.Type pluginType =
                    plugin.GetType();

                System.Reflection.MethodInfo disablePov =
                    AccessTools.Method(
                        pluginType,
                        "DisablePov"
                    );

                System.Reflection.MethodInfo enablePov =
                    AccessTools.Method(
                        pluginType,
                        "EnablePov"
                    );

                System.Reflection.MethodInfo disablePrefix =
                    AccessTools.Method(
                        typeof(TimelinePOVSwitchX),
                        "PerspectiveXDisablePovPrefix"
                    );

                System.Reflection.MethodInfo enablePostfix =
                    AccessTools.Method(
                        typeof(TimelinePOVSwitchX),
                        "PerspectiveXEnablePovPostfix"
                    );

                if (
                    disablePov != null &&
                    disablePrefix != null
                )
                {
                    harmony.Patch(
                        disablePov,
                        new HarmonyMethod(disablePrefix),
                        null
                    );
                }

                if (
                    enablePov != null &&
                    enablePostfix != null
                )
                {
                    harmony.Patch(
                        enablePov,
                        null,
                        new HarmonyMethod(enablePostfix)
                    );
                }
            }
            catch
            {
            }
        }



        private static void TryPatchPerspectiveXCameraPreCull(
            Harmony harmony
        )
        {
            if (harmony == null)
                return;

            try
            {
                BepInEx.PluginInfo pluginInfo;

                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }

                object plugin =
                    pluginInfo.Instance;

                if (plugin == null)
                    return;

                System.Reflection.MethodInfo cameraPreCull =
                    AccessTools.Method(
                        plugin.GetType(),
                        "OnCameraPreCull",
                        new System.Type[]
                        {
                            typeof(Camera)
                        }
                    );

                if (cameraPreCull == null)
                    return;

                System.Reflection.MethodInfo postfixMethod =
                    AccessTools.Method(
                        typeof(TimelinePOVSwitchX),
                        "PerspectiveXCameraPreCullPostfix"
                    );

                if (postfixMethod == null)
                    return;

                harmony.Patch(
                    cameraPreCull,
                    null,
                    new HarmonyMethod(postfixMethod)
                );
            }
            catch
            {
            }
        }



        private static void WriteCustomParts(
            Timeline.Keyframe keyframe,
            float fov,
            bool hideHead,
            bool alignWithBody,
            float headSway,
            float pitchLimit,
            float positionSmoothing,
            float forwardOffset,
            float nearClip,
            float upOffset
        )
        {
            string storedValue =
                keyframe.value as string;


            if (string.IsNullOrEmpty(storedValue))
                return;


            string[] parts =
                storedValue.Split('|');


            string sceneId =
                parts[0];


            string viewMode =
                parts.Length > 1
                    ? parts[1]
                    : "custom";


            bool forcePov =
                GetForcePov(parts);

            bool spawnMirror =
                GetSpawnMirror(parts);


            keyframe.value =
                sceneId + "|" +
                viewMode + "|" +
                FloatText(Mathf.Clamp(fov, 20f, 120f)) + "|" +
                (hideHead ? "1" : "0") + "|" +
                (alignWithBody ? "1" : "0") + "|" +
                FloatText(Mathf.Clamp01(headSway)) + "|" +
                FloatText(Mathf.Clamp(pitchLimit, 30f, 89f)) + "|" +
                FloatText(Mathf.Clamp01(positionSmoothing)) + "|" +
                FloatText(Mathf.Clamp(forwardOffset, 0f, 0.2f)) + "|" +
                FloatText(Mathf.Clamp(nearClip, 0.01f, 0.1f)) + "|" +
                FloatText(Mathf.Clamp(upOffset, -0.1f, 0.1f)) + "|" +
                (forcePov ? "force1" : "force0") + "|" +
                (spawnMirror ? "mirror1" : "mirror0");

            string mirrorModeTag =
                GetExistingMirrorModeTag(parts);

            string viewDirectionTag =
                GetViewDirectionTag(parts);

            string headFollowTag =
                GetHeadFollowCamera(parts)
                    ? "headfollow1"
                    : "headfollow0";

            string headFollowYawTag =
                "hfyaw" + FloatText(GetHeadFollowYawLimit(parts));
            string headFollowPitchTag =
                "hfpitch" + FloatText(GetHeadFollowPitchLimit(parts));

            if (!string.IsNullOrEmpty(mirrorModeTag))
                keyframe.value += "|" + mirrorModeTag;

            if (!string.IsNullOrEmpty(viewDirectionTag))
                keyframe.value += "|" + viewDirectionTag;

            keyframe.value += "|" + headFollowTag + "|" + headFollowYawTag + "|" + headFollowPitchTag;
        }



        private static string FloatText(
            float value
        )
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture
            );
        }



        // =========================================================
        // TIMELINE VALUE SAVE / LOAD
        //
        // Timeline calls these methods when it serializes keyframes into
        // the scene's Timeline data. The entire POV payload is one string:
        //
        // sceneId|viewMode|...custom settings...|force1
        //
        // Keeping it in one XML attribute means character selection,
        // View mode, Force POV and all Custom values survive scene saves.
        // Old values remain compatible because the payload format itself
        // has not changed.
        // =========================================================


        private static void SwitchPOV(
            int sceneId,
            string viewMode,
            string[] storedParts
        )
        {
            try
            {
                // The previous keyframe's mirror lives only until the next
                // POV keyframe is reached.
                DeleteActivePovMirror();
                ClearHeadFollowCamera();


                // -1 is the special "Disable POV" Timeline action.
                // It exits PerspectiveX POV immediately and intentionally
                // ignores Force POV, View Slot and Custom settings.
                if (sceneId == -1)
                {
                    forceCapturedCameraDirection = false;
                    forceCapturedCameraPosition = false;
                    BepInEx.PluginInfo disablePluginInfo;

                    if (
                        !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                            "bucky.kk.perspectivex",
                            out disablePluginInfo
                        )
                    )
                    {
                        return;
                    }


                    object disablePlugin =
                        disablePluginInfo.Instance;

                    System.Reflection.MethodInfo disablePov =
                        disablePlugin.GetType().GetMethod(
                            "DisablePov",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (disablePov != null)
                    {
                        timelineCallingPerspectiveXLifecycle = true;

                        try
                        {
                            disablePov.Invoke(
                                disablePlugin,
                                null
                            );
                        }
                        finally
                        {
                            timelineCallingPerspectiveXLifecycle = false;
                        }
                    }


                    return;
                }


                ObjectCtrlInfo obj;


                // Find character from stored scene ID.
                if (
                    !Studio.Studio.Instance.dicObjectCtrl.TryGetValue(
                        sceneId,
                        out obj
                    )
                )
                {
                    return;
                }


                OCIChar target =
                    obj as OCIChar;


                if (target == null)
                    return;



                // Find PerspectiveX.
                BepInEx.PluginInfo pluginInfo;


                if (
                    !BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
                        "bucky.kk.perspectivex",
                        out pluginInfo
                    )
                )
                {
                    return;
                }


                object plugin =
                    pluginInfo.Instance;

                System.Type pluginType =
                    plugin.GetType();


                // Find PerspectiveX's povEnabled field first.
                System.Reflection.FieldInfo povEnabledField =
                    pluginType.GetField(
                        "povEnabled",

                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );


                if (povEnabledField == null)
                    return;


                bool povEnabled =
                    (bool)povEnabledField.GetValue(
                        plugin
                    );


                bool forcePov =
                    GetForcePov(storedParts);


                // Force POV OFF means this event is allowed to control an
                // already-active POV, but it must not activate POV by itself.
                if (
                    !povEnabled &&
                    !forcePov
                )
                {
                    return;
                }


                // Apply this keyframe's Custom settings before enabling/switching.
                if (
                    viewMode == "custom" &&
                    storedParts != null &&
                    storedParts.Length >= 11
                )
                {
                    float customFov;
                    bool customHideHead;
                    bool customAlignWithBody;
                    float customHeadSway;
                    float customPitchLimit;
                    float customPositionSmoothing;
                    float customForwardOffset;
                    float customNearClip;
                    float customUpOffset;


                    if (TryReadCustomParts(
                        storedParts,
                        out customFov,
                        out customHideHead,
                        out customAlignWithBody,
                        out customHeadSway,
                        out customPitchLimit,
                        out customPositionSmoothing,
                        out customForwardOffset,
                        out customNearClip,
                        out customUpOffset
                    ))
                    {
                        SetPerspectiveXSetting(plugin, pluginType, "DefaultFov", Mathf.Clamp(customFov, 20f, 120f));
                        SetPerspectiveXSetting(plugin, pluginType, "HideHead", customHideHead);
                        SetPerspectiveXSetting(plugin, pluginType, "AlignWithBody", customAlignWithBody);
                        SetPerspectiveXSetting(plugin, pluginType, "HeadSway", customHeadSway);
                        SetPerspectiveXSetting(plugin, pluginType, "PitchLimit", customPitchLimit);
                        SetPerspectiveXSetting(plugin, pluginType, "PositionSmoothing", customPositionSmoothing);
                        SetPerspectiveXSetting(plugin, pluginType, "ForwardOffset", customForwardOffset);
                        SetPerspectiveXSetting(plugin, pluginType, "NearClip", customNearClip);
                        SetPerspectiveXSetting(plugin, pluginType, "UpOffset", customUpOffset);
                    }
                }


                // Only Force POV is allowed to activate PerspectiveX.
                if (
                    !povEnabled &&
                    forcePov
                )
                {
                    System.Reflection.MethodInfo enablePov =
                        pluginType.GetMethod(
                            "EnablePov",

                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (enablePov == null)
                        return;


                    timelineCallingPerspectiveXLifecycle = true;

                    try
                    {
                        enablePov.Invoke(
                            plugin,
                            null
                        );
                    }
                    finally
                    {
                        timelineCallingPerspectiveXLifecycle = false;
                    }
                }


                // Find PerspectiveX's private SwitchTo method.
                System.Reflection.MethodInfo switchTo =
                    plugin.GetType().GetMethod(
                        "SwitchTo",

                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );


                if (switchTo == null)
                    return;



                switchTo.Invoke(
                    plugin,
                    new object[]
                    {
                        target.charInfo
                    }
                );


                // Apply a PerspectiveX saved view only when
                // this keyframe explicitly asks for one.
                // None / Default and Custom do nothing here.
                int viewSlot =
                    0;


                if (viewMode == "slot1")
                    viewSlot = 1;
                else if (viewMode == "slot2")
                    viewSlot = 2;
                else if (viewMode == "slot3")
                    viewSlot = 3;


                if (viewSlot != 0)
                {
                    System.Reflection.MethodInfo loadViewSlot =
                        pluginType.GetMethod(
                            "LoadViewSlot",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance
                        );


                    if (loadViewSlot != null)
                    {
                        loadViewSlot.Invoke(
                            plugin,
                            new object[]
                            {
                                viewSlot
                            }
                        );
                    }
                }


                // Apply the keyframe's captured PerspectiveX look direction
                // after SwitchTo and after an optional View Slot load.
                // This changes PerspectiveX's own mouse-look state once;
                // it does not freeze Camera.main, so movement/sway continue.
                ApplyPerspectiveXViewDirection(
                    plugin,
                    pluginType,
                    storedParts
                );

                SetHeadFollowCamera(
                    target,
                    GetHeadFollowCamera(storedParts),
                    GetHeadFollowYawLimit(storedParts),
                    GetHeadFollowPitchLimit(storedParts)
                );


                if (
                    IsMirrorFunctionAvailable() &&
                    GetSpawnMirror(storedParts)
                )
                {
                    SpawnPovMirrorForCharacter(
                        target,
                        GetMirrorMode(storedParts) == "dynamic"
                    );
                }


            }
            catch
            {
            }
        }
    }
}
