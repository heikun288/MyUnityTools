//****************************************************************************
//
//  File:      OptimizeAnimationClipTool.cs
//
//  Copyright (c) SuiJiaBin
//
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
//****************************************************************************

//#define CREATE_NEW_ANIM

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;

namespace EditorTool
{


    public class OptimizeAnimationClipTool
    {
        static MethodInfo getAnimationClipStats;
        static FieldInfo sizeInfo;
        static object[] methodParam = new object[1];
        static int m_Index = 0;
        static List<string> m_FBXPaths = new List<string>();
        static Dictionary<string, AnimatorController> m_AnimatorControllerDict = new Dictionary<string, AnimatorController>();

        [MenuItem("Assets/Animation/裁剪浮点数and去除Scale")]
        public static void Optimize()
        {
            GetAnimationClipsFromFBX();
#if (CREATE_NEW_ANIM)
            GetAnimatorController();
#endif
            InitMethodInfo();
            int index = 0;
            int count = m_FBXPaths.Count;
            
            foreach (string path in m_FBXPaths)
            {
                string info = string.Format("{0}/{1}  {2}", ++index, count, path);
                
                bool isCancel = EditorUtility.DisplayCancelableProgressBar("优化AnimationClip", info, (float)index / (float)count);
                if(isCancel)
                {
                    break;
                }
                Optimize(path);
            }

            Finished();
        }

        private static void InitMethodInfo()
        {
            Assembly asm = Assembly.GetAssembly(typeof(Editor));
            getAnimationClipStats = typeof(AnimationUtility).GetMethod("GetAnimationClipStats", BindingFlags.Static | BindingFlags.NonPublic);
            Type aniclipstats = asm.GetType("UnityEditor.AnimationClipStats");
            sizeInfo = aniclipstats.GetField("size", BindingFlags.Public | BindingFlags.Instance);
        }

        private static void Finished()
        {
            EditorUtility.ClearProgressBar();
            Debug.LogFormat("--优化完成--    总数量: {0}/{1}",  m_Index, m_FBXPaths.Count);
//            Resources.UnloadUnusedAssets();
//            GC.Collect();
//            AssetDatabase.SaveAssets();
            m_Index = 0;
        }

        private static void Optimize(string path)
        {
            string dirPath = Path.GetDirectoryName(path);
            AnimatorController controller = null;
            string clipDirPath = dirPath + "/Clip";
#if (CREATE_NEW_ANIM)
            if(!m_AnimatorControllerDict.TryGetValue(dirPath, out controller))
            {
                Debug.LogErrorFormat("找不到Animator Controller:[{0}]", dirPath);
                return;
            }

            if (!Directory.Exists(clipDirPath))
            {
                Directory.CreateDirectory(clipDirPath);
            }
#endif

            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in objs)
            {
                if (obj is AnimationClip)
                {
                    if (obj.name != "__preview__Take 001")
                    {
                        OptimizeClip(obj as AnimationClip, controller, clipDirPath);
                    }
                }
            }
        }

        private static void OptimizeClip(AnimationClip clip, AnimatorController controller, string clipDirPath)
        {
            if(clip == null)
            {
                return;
            }

#if (CREATE_NEW_ANIM)
            string clipPath = string.Format("{0}/{1}.anim", clipDirPath, clip.name);
            AnimationClip newClip;

            if(File.Exists(clipPath))
            {
                newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            }
            else
            {
                newClip = new AnimationClip();
                AssetDatabase.CreateAsset(newClip, clipPath);
            }

            EditorUtility.CopySerialized(clip, newClip);
            OptimizeClip(newClip);
#else
            OptimizeClip(clip);
#endif

            

#if (CREATE_NEW_ANIM)
            if(controller.layers.Length > 0)
            {
                ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
                for (int i = 0; i < states.Length; ++i)
                {
                    if (states[i].state.name == newClip.name)
                    {
                        states[i].state.motion = newClip;
                        break;
                    }
                }
            }            
#endif
        }

        public static void OptimizeClip(AnimationClip clip)
        {
//            LogInspectorSize(clip);

            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);

            foreach (EditorCurveBinding theCurveBinding in curveBindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, theCurveBinding);
                Keyframe[] keys = curve.keys;
                bool isKeyChanged = IsKeyFrameChanged(keys);
                if(!isKeyChanged)
                {
                    //移除没有变化的Scale曲线
                    if(RemoveUnChangedScaleCurve(clip, theCurveBinding, curve))
                    {
                        continue;
                    }

                    //过滤位移值没有变化的帧动画
                    OptimizeUnChangedKeyFrams(clip, theCurveBinding, curve);
                }

                //压缩浮点数精度
                CompressionFloat(clip, theCurveBinding, curve);
            }

            //            LogInspectorSize(clip); 
        }

        /// <summary>
        /// 过滤位移值没有变化的帧动画
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="theCurveBinding"></param>
        /// <param name="curve"></param>
        /// <returns></returns>
        private static void OptimizeUnChangedKeyFrams(AnimationClip clip, EditorCurveBinding theCurveBinding, AnimationCurve curve)
        {
            Keyframe[] keys = curve.keys;
            if (keys.Length > 2)
            {
                Keyframe[] newKeys = new Keyframe[2];
                newKeys[0] = keys[0];
                newKeys[1] = keys[keys.Length - 1];
                curve.keys = newKeys;
                AnimationUtility.SetEditorCurve(clip, theCurveBinding, curve);
            }
        }

        /// <summary>
        /// 压缩浮点数精度
        /// </summary>
        /// <param name="clip"></param>
        /// <returns></returns>
        private static void CompressionFloat(AnimationClip clip, EditorCurveBinding theCurveBinding, AnimationCurve curve)
        {
            //浮点数精度压缩到f4
            string floatFormat = "f4";
            Keyframe key;
            Keyframe[] keyFrames = curve.keys;
            for (int i = 0; i < keyFrames.Length; i++)
            {
                key = keyFrames[i];
                key.value = float.Parse(key.value.ToString(floatFormat));
                key.inTangent = float.Parse(key.inTangent.ToString(floatFormat));
                key.outTangent = float.Parse(key.outTangent.ToString(floatFormat));
                keyFrames[i] = key;
            }
            curve.keys = keyFrames;
            //clip.SetCurve(theCurveBinding.path, theCurveBinding.type, theCurveBinding.propertyName, curve);
            AnimationUtility.SetEditorCurve(clip, theCurveBinding, curve);
        }

        /// <summary>
        /// 移除没有变化的Scale曲线
        /// </summary>
        /// <param name="clip"></param>
        /// <returns></returns>
        private static bool RemoveUnChangedScaleCurve(AnimationClip clip, EditorCurveBinding theCurveBinding, AnimationCurve curve)
        {
            string name = theCurveBinding.propertyName.ToLower();
            if (name.Contains("scale") )
            {                
                AnimationUtility.SetEditorCurve(clip, theCurveBinding, null);
                return true;
                //clip.SetCurve(theCurveBinding.path, theCurveBinding.type, theCurveBinding.propertyName, null);
            }
            return false;
        }

        /// <summary>
        /// 判断序列帧是否有变化
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        private static bool IsKeyFrameChanged(Keyframe[] keys)
        {
            if(keys.Length <= 2)
            {//只有首尾两帧
                return false;
            }

            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (Mathf.Abs(keys[i].value - keys[i + 1].value) > 0
                    || Mathf.Abs(keys[i].outTangent - keys[i + 1].outTangent) > 0
                    || Mathf.Abs(keys[i].inTangent - keys[i + 1].inTangent) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        static private void GetAnimationClipsFromFBX()
        {
            m_FBXPaths.Clear();
            List<string> paths = new List<string>();
            HashSet<string> fbxPaths = new HashSet<string>();
            string[] guids = null;

            UnityEngine.Object[] objs = Selection.GetFiltered(typeof(object), SelectionMode.Assets);
            if (objs.Length > 0)
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    if (objs[i].GetType() == typeof(AnimationClip))
                    {
                        string p = AssetDatabase.GetAssetPath(objs[i]);
                        fbxPaths.Add(p);
                    }
                    else
                    {
                        paths.Add(AssetDatabase.GetAssetPath(objs[i]));
                    }
                }
                if (paths.Count > 0)
                {
                    string filter = string.Format("t:{0}", typeof(AnimationClip).ToString().Replace("UnityEngine.", ""));
                    guids = AssetDatabase.FindAssets(filter, paths.ToArray());
                }
                else
                {
                    guids = new string[] { };
                }
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if(!assetPath.EndsWith(".FBX", System.StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                fbxPaths.Add(assetPath);
            }

            foreach(string path in fbxPaths)
            {
                m_FBXPaths.Add(path);
            }
        }

        static private void GetAnimatorController()
        {
            m_AnimatorControllerDict.Clear();
            List<string> paths = new List<string>();
            string[] guids = null;

            UnityEngine.Object[] objs = Selection.GetFiltered(typeof(object), SelectionMode.Assets);
            if (objs.Length > 0)
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    string path = AssetDatabase.GetAssetPath(objs[i]);
                    paths.Add(Path.GetDirectoryName(path));
                }

                if (paths.Count > 0)
                {
                    guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(RuntimeAnimatorController).ToString().Replace("UnityEngine.", "")), paths.ToArray());
                }
                else
                {
                    guids = new string[] { };
                }
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                string dirName = Path.GetDirectoryName(assetPath);
                if(m_AnimatorControllerDict.ContainsKey(dirName))
                {
                    continue;
                }

                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
                m_AnimatorControllerDict[dirName] = controller;
            }
        }

        private static void LogInspectorSize(AnimationClip clip)
        {
            methodParam[0] = clip;
            var stats = getAnimationClipStats.Invoke(null, methodParam);
            int size = (int)sizeInfo.GetValue(stats);

            Debug.LogWarningFormat("{0}  {1}", clip.name, EditorUtility.FormatBytes(size));
        }
    }
}