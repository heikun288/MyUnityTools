using Cinemachine.Timeline;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MissingDetectEditor : Editor 
{
    //跳过不检测的目录
    static List<string> ignorePath = new List<string>() {
            "Assets/RawResources/Audio",
            "Assets/RawResources/Emoji",
            "Assets/RawResources/Filter",
            "Assets/RawResources/Font",
            "Assets/RawResources/Shader",
            //"Assets/RawResources/T4MOBJ",
            "Assets/RawResources/UI/Prefab/Unuse",
            "Assets/RawResources/UI/Texture",
            //"Assets/RawResources/UI",
        };
    private static bool CheckIsToIgnore(string path)
    {
        for (int i = 0; i < ignorePath.Count; i++)
        {
            if (path.Contains(ignorePath[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string rawResourcesPath = "Assets/RawResources";


    [MenuItem("Tools/AssetsCheckTools/0.一键检查丢失，无用资源(全部)(1,3,7)", priority = 2100)]
    static void CheckAll()
    {
        string missingMsg = CheckMissingAll(false);
        string unuseMsg = CheckUnuseResource(false, false);
        //string artLableMsg = CheckAndDeleteUnuseAssetInArtLable(false, false);
        string unusePrefabInScene = CheckUnusePrefabInSceneFolder(false);
        

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("资源丢失检测结果：\n");
        sb.AppendLine(missingMsg);
        sb.AppendLine("\n\n\n无用资源检测结果：");
        sb.AppendLine(unuseMsg);
        //sb.AppendLine("\n\n\n无用艺术字资源：");
        //sb.AppendLine(artLableMsg);
        sb.AppendLine("\n\n\n场景目录下无用预设检测结果：");
        sb.AppendLine(unusePrefabInScene);

        string msg = sb.ToString();

        Debug.LogError(msg);
        ShowDisplayDialo("检查结果", msg);
        SaveStringToFile(msg); 
    }



    [MenuItem("Tools/AssetsCheckTools/1.检查丢失(全部)", priority = 2110)]
    static void CheckMissing()
    {
        CheckMissingAll(true);
    }

    static string CheckMissingAll(bool isToShowResult)
    {
        //string uiPrefabDirectoryPath = "Assets/Test";
        string[] allPath = AssetDatabase.GetAllAssetPaths();

        StringBuilder allMsg = new StringBuilder();
        StringBuilder scriptMissingMsg = new StringBuilder();
        StringBuilder meshMissing = new StringBuilder();
        StringBuilder materialMissing = new StringBuilder();
        StringBuilder shaderlMissing = new StringBuilder();
        StringBuilder shaderError = new StringBuilder(); 
        int checkedCount = CheckMissingInPaths(allPath, ref scriptMissingMsg, ref meshMissing, ref materialMissing, ref shaderlMissing, ref shaderError);

        return ShowResult(allMsg, scriptMissingMsg, meshMissing, materialMissing, shaderlMissing, checkedCount, isToShowResult);
    }

    private static string ShowResult(StringBuilder allMsg, StringBuilder scriptMissingMsg, StringBuilder meshMissing, StringBuilder materialMissing, StringBuilder shaderlMissing, int checkedCount, bool isToShowResult)
    {
        if (scriptMissingMsg != null && scriptMissingMsg.Length > 0)
        {
            allMsg.AppendLine("以下物体有脚本丢失：\n" + scriptMissingMsg);
        }
        if (meshMissing != null && meshMissing.Length > 0)
        {
            allMsg.AppendLine("以下物体有Mesh丢失：\n" + meshMissing);
        }
        if (materialMissing != null && materialMissing.Length > 0)
        {
            allMsg.AppendLine("以下物体有材质球丢失：\n" + materialMissing);
        }
        if (shaderlMissing != null && shaderlMissing.Length > 0)
        {
            allMsg.AppendLine("以下材质球有Shader丢失：\n" + shaderlMissing);
        }


        string msg = null;
        string title = "检测完成";
        if (allMsg.Length > 0)
        {
            msg = allMsg.ToString();           
                    
        }
        else
        {
            msg = string.Empty;           
            if (checkedCount > 0)
            {
                msg = "检测完成，没检测到有丢失";
            }
            else
            {
                title = "检测失败";
                StringBuilder ignorePathSB = new StringBuilder();
                for (int i = 0; i < ignorePath.Count;i++ )
                {
                    ignorePathSB.AppendLine(ignorePath[i]);
                }
                msg = string.Format("检测了0个，请确认所选择需要检测的资源为{0}下的目录或资源\n以下目录默认跳过，不检查：\n{1}", rawResourcesPath, ignorePathSB.ToString());
            }         
        }

        if (isToShowResult)
        {
            Debug.LogError(msg);
            ShowDisplayDialo(title, msg);
            SaveStringToFile(msg);  
        }

        return msg;
    }

    static void ShowDisplayDialo(string title, string msg)
    {
        string temStr = msg;
        int maxLenght = 50;
        int line = Regex.Matches(msg, @"\n").Count;
        if (line > maxLenght)
        {
            temStr = "太多了显示不下，请到Console窗口查看或者到输出的result.txt文件里查看……";
        }
        EditorUtility.DisplayDialog(title, temStr, "OK");  
    }



    static int CheckMissingInPaths(string[] allPath, ref StringBuilder scriptMissingMsg, ref StringBuilder meshMissing, ref StringBuilder materialMissing, ref StringBuilder shaderlMissing, ref StringBuilder shaerError)
    {
        int checkedItemCount = 0;

        for (int i = 0; i < allPath.Length; i++)
        {
            EditorUtility.DisplayProgressBar("正在检查资源丢失", allPath[i], i * 1f / allPath.Length);
            if (allPath[i].Contains(rawResourcesPath) && allPath[i] != rawResourcesPath && !CheckIsToIgnore(allPath[i]))
            {
                GameObject go = AssetDatabase.LoadAssetAtPath(allPath[i], typeof(GameObject)) as GameObject;
                List<GameObject> missCom = null;

                checkedItemCount++;

                if (CheckScriptMissing(go, ref missCom))
                {
                    scriptMissingMsg.AppendLine(allPath[i] + string.Format("({0})", GetGameNameList(missCom)));
                }

                missCom = null;
                if (CheckMeshMissing(go, ref  missCom))
                {
                    meshMissing.AppendLine(allPath[i] + string.Format("({0})", GetGameNameList(missCom)));                    
                }



                if (!allPath[i].EndsWith(".FBX") && !allPath[i].EndsWith(".fbx"))
                {
                    missCom = null;
                    if (CheckMaterialMissing(go, ref missCom))
                    {
                        materialMissing.AppendLine(allPath[i] + string.Format("({0})", GetGameNameList(missCom)));
                    }
                }


                if (allPath[i].EndsWith(".mat"))
                {
                    Material mat = AssetDatabase.LoadAssetAtPath(allPath[i], typeof(Material)) as Material;
                    if (mat != null)
                    {
                        if (CheckShaderMissing(mat) == 1)
                        {
                            shaerError.AppendLine(allPath[i]);
                        }
                        else if(CheckShaderMissing(mat) == 2)
                        {
                            shaderlMissing.AppendLine(allPath[i]);
                        }
                    }
                }
            }
        }

        EditorUtility.ClearProgressBar();
        return checkedItemCount;
    }

    private static string GetGameNameList(List<GameObject> gameObjectList)
    {
        string gameObjectNames = string.Empty;

        if(gameObjectList != null)
        {
            for (int i = 0; i < gameObjectList.Count; i++)
            {
                if (string.IsNullOrEmpty(gameObjectNames))
                {
                    gameObjectNames += gameObjectList[i].name;
                }
                else
                {
                    gameObjectNames += ", " + gameObjectList[i].name;
                }
            }
        }

        return gameObjectNames;
    }


    //[MenuItem("Tools/AssetsCheckTools/CheckSelection", priority = 2101)]
    static void CheckMissingWithSelection()
    {
        Object[] objs = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

        StringBuilder allMsg = new StringBuilder();
        StringBuilder scriptMissingMsg = new StringBuilder();
        StringBuilder meshMissing = new StringBuilder();
        StringBuilder materialMissing = new StringBuilder();
        StringBuilder shaderlMissing = new StringBuilder();
        StringBuilder shaderError = new StringBuilder();

        string[] allPath = null;

        int checkedCount = 0;

        for (int i = 0; i < objs.Length;i++ )
        {
            string path = AssetDatabase.GetAssetPath(objs[i]);

            if(objs[i] is DefaultAsset)
            {               
                List<string> checkPaths = new List<string>();

                if(allPath == null)
                {
                    allPath = AssetDatabase.GetAllAssetPaths();        
                }
                for(int j = 0;j<allPath.Length;j++)
                {
                    if(allPath[j].Contains(rawResourcesPath) && allPath[j].Contains(path))
                    {
                        checkPaths.Add(allPath[j]);
                    }
                }

                if (checkPaths.Count > 0)
                {
                    checkedCount += CheckMissingInPaths(checkPaths.ToArray(), ref scriptMissingMsg, ref meshMissing, ref materialMissing, ref shaderlMissing, ref shaderError);
                }
            }
            else
            {
                string[] dependencies = AssetDatabase.GetDependencies(path, true);//这里已经包含了自身GO的路径了

                checkedCount += CheckMissingInPaths(dependencies, ref scriptMissingMsg, ref meshMissing, ref materialMissing, ref shaderlMissing, ref shaderError);
            }
        }

        ShowResult(allMsg, scriptMissingMsg, meshMissing, materialMissing, shaderlMissing, checkedCount, true);
    }

    [MenuItem("Tools/AssetsCheckTools/2.检查丢失(选中预设或场景及其依赖资源)", priority = 2120)]
    static void CheckMissingWithSelectionInDeep()
    {
        Object[] objs = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

        StringBuilder allMsg = new StringBuilder();
        
       
        List<string> checkPaths = new List<string>();

        //先找到选中的资源或目录下的所有预设或场景文件
        string[] allPath = AssetDatabase.GetAllAssetPaths();
        for (int i = 0; i < objs.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(objs[i]);

            if (objs[i] is DefaultAsset)
            {
                for (int j = 0; j < allPath.Length; j++)
                {
                    if (allPath[j].Contains(rawResourcesPath) && allPath[j].Contains(path) && CheckIsPrefabOrScenePath(allPath[j]))
                    {
                        checkPaths.Add(allPath[j]);
                    }
                }                        
            }
            else
            {
                if (path.Contains(rawResourcesPath) && CheckIsPrefabOrScenePath(path))
                {
                    checkPaths.Add(path);
                }
            }
        }

        int checkedCount = 0;
        int missingCount = 0;
        for (int i = 0; i < checkPaths.Count;i++ )
        {
            StringBuilder scriptMissingMsg = new StringBuilder();
            StringBuilder meshMissing = new StringBuilder();
            StringBuilder materialMissing = new StringBuilder();
            StringBuilder shaderlMissing = new StringBuilder();
            StringBuilder shaderError = new StringBuilder(); 
            string[] dependencies = AssetDatabase.GetDependencies(checkPaths[i], true);//这里已经包含了自身GO的路径了
            checkedCount += CheckMissingInPaths(dependencies, ref scriptMissingMsg, ref meshMissing, ref materialMissing, ref shaderlMissing ,ref shaderError);

            if (scriptMissingMsg.Length > 0 || meshMissing.Length > 0 || materialMissing.Length > 0 || shaderlMissing.Length > 0)
            {
                missingCount++;
                allMsg.AppendLine(string.Format("{0} 相关的以下资源有丢失：", checkPaths[i]));

                if(scriptMissingMsg.Length > 0)
                {
                    allMsg.AppendLine("脚本丢失：");
                    allMsg.AppendLine(scriptMissingMsg.ToString());
                }
                if (meshMissing.Length > 0)
                {
                    allMsg.AppendLine("Mesh丢失：");
                    allMsg.AppendLine(meshMissing.ToString());
                }
                if (materialMissing.Length > 0)
                {
                    allMsg.AppendLine("材质球丢失：");
                    allMsg.AppendLine(materialMissing.ToString());
                }
                if (shaderlMissing.Length > 0)
                {
                    allMsg.AppendLine("Shader丢失：");
                    allMsg.AppendLine(shaderlMissing.ToString());
                }
                if(shaderError.Length > 0)
                {
                    allMsg.AppendLine("材质球使用了错误Shader 默认shader 需要删除：");
                    allMsg.AppendLine(shaderError.ToString());
                }

                allMsg.AppendLine("\n");
            }
        }
        
        ShowResult(allMsg, null, null, null, null, checkedCount, true);
    }

    static bool CheckIsPrefabOrScenePath(string path)
    {
        return (path.EndsWith(".prefab") || path.EndsWith(".unity"));
    }

    [MenuItem("Tools/AssetsCheckTools/3.检查无用资源(全部)", priority = 2130)]
    static void CheckUnuseResourceOnly()
    {
        CheckUnuseResource(false, true);
    }

    [MenuItem("Tools/AssetsCheckTools/4.删除无用资源(全部)", priority = 2140)]
    static void CheckAndDeleteUnuseResource()
    {
        CheckUnuseResource(true, true);
    }


    static System.DateTime lastCheckUnuseResourceTime;
    static List<string> unusePathList = null;

    ///// <summary>
    ///// 删除无用资源忽略列表
    ///// </summary>
    //static List<string> deleteUnuseAssetIgnorList = new List<string>()
    //{
    //    "Assets/RawResources/Effect/Common/Materials/UI_image_gray",
    //    "Assets/RawResources/UI/Material",
    //};

    static string CheckUnuseResource(bool isToRemove, bool isToShowResult)
    {        
        StringBuilder sb = new StringBuilder();

        //如果不需要删除，说明是只检查的，必须检查， 如果需要删除，并且距离上次检查已经超过了X秒，也强制检查
        bool isToCheck = !isToRemove || unusePathList == null || unusePathList.Count <= 0 || (System.DateTime.Now - lastCheckUnuseResourceTime).TotalSeconds >= 60 * 5;
        if(isToCheck)
        {
            unusePathList = new List<string>();

            string[] allPath = AssetDatabase.GetAllAssetPaths();

            List<string> allToCheckResources = new List<string>();//需要检测的所有资源路径，RawResources下除了忽略的目录，其他目录下所有的资源路径

            List<string> usingPaths = new List<string>();//预设及其依赖的资源的目录
            for (int i = 0; i < allPath.Length; i++)
            {
                EditorUtility.DisplayProgressBar("正在检查无用资源", allPath[i], i * 1f / allPath.Length);
                if (allPath[i].Contains(rawResourcesPath) && !CheckIsToIgnore(allPath[i]))
                {
                    if (allPath[i].Contains("."))
                    {
                        allToCheckResources.Add(allPath[i]);
                    }

                    if (CheckIsPrefabOrScenePath(allPath[i]))
                    {
                        string[] dependencies = AssetDatabase.GetDependencies(allPath[i]);
                        for (int j = 0; j < dependencies.Length; j++)
                        {
                            if (!usingPaths.Contains(dependencies[j]))
                            {
                                usingPaths.Add(dependencies[j]);
                            }
                        }
                    }
                }
            }
           
            for (int i = 0; i < allToCheckResources.Count; i++)
            {
                if (!usingPaths.Contains(allToCheckResources[i]) && !allToCheckResources[i].EndsWith(".unity") /*&& !allToCheckResources[i].EndsWith(".prefab")*/ && !allToCheckResources[i].EndsWith(".xlsx")
                    && !allToCheckResources[i].EndsWith(".txt") /*&& !allToCheckResources[i].Contains("Assets/RawResources/Effect/Texture")*/
                    && !allToCheckResources[i].Contains("Assets/RawResources/UI/Material")
                    )
                {
                    unusePathList.Add(allToCheckResources[i]);
                }
            }

            lastCheckUnuseResourceTime = System.DateTime.Now;
        }

        
        unusePathList.Sort();
        for (int i = 0; i < unusePathList.Count;i++ )
        {
            sb.AppendLine(unusePathList[i]);
            if (isToRemove)
            {
                EditorUtility.DisplayProgressBar("正在删除无用资源", unusePathList[i], i * 1f / unusePathList.Count);
                AssetDatabase.DeleteAsset(unusePathList[i]);
            }
        }

        string msg = string.Empty;
        if (sb.Length > 0)
        {
            if (isToRemove)
            {
                msg = string.Format("删除了以下{0}个资源:\n", unusePathList.Count) + sb.ToString();
                unusePathList.Clear();
                unusePathList = null;
                AssetDatabase.Refresh();               
            }
            else
            {
                msg = string.Format("以下{0}个资源可能是没用的，请检查确认没用后删除:\n", unusePathList.Count) + sb.ToString();
            }
        }
        else
        {
            msg = "检测完成，未发现无用的资源";
        }

        EditorUtility.ClearProgressBar();

        if (isToShowResult)
        {
            Debug.Log(msg);
            ShowDisplayDialo("处理结果", msg);
            SaveStringToFile(msg);
        }

        return msg;
    }

    /// <summary>
    /// 查找使用到当前所选择的资源的全部资源
    /// </summary>
    [MenuItem("Tools/AssetsCheckTools/5.查找谁引用了选中的资源", priority = 2150)]
    static void FindUsingAsset()
    {
        Object[] objs = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

        string msg = string.Empty;

        if (objs.Length == 1)
        {
            if (objs[0] is DefaultAsset)
            {
                msg = "选择的是目录，请选中具体的某个资源";
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(objs[0]);
                string[] allPath = AssetDatabase.GetAllAssetPaths();
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < allPath.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("正在检查资源", allPath[i], i * 1f / allPath.Length);
                    if (allPath[i] != path && allPath[i].Contains(rawResourcesPath))
                    {
                        string[] dependencies = AssetDatabase.GetDependencies(allPath[i], false);
                        for (int j = 0; j < dependencies.Length; j++)
                        {
                            if (dependencies[j] == path)
                            {
                                sb.AppendLine(allPath[i]);
                                break;
                            }
                        }
                    }
                }

                if (sb.Length > 0)
                {
                    msg = string.Format("{0}被以下资源引用到：\n", path) + sb.ToString();
                }
                else
                {
                    msg = string.Format("{0}没有被任何资源引用", path);
                }
            }
        }
        else
        {

            if (objs.Length <= 0)
            {
                msg = "选择的资源为空，请选中某个资源后再执行该命令";
            }
            else
            {
                msg = "选择的资源大于一个，每次只能检查一个资源";
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log(msg);
        ShowDisplayDialo("检测结果", msg);        
    }
      



    /// <summary>
    /// 检查ArtLable下没用的资源
    /// </summary>
    [MenuItem("Tools/AssetsCheckTools/6.检查无用的艺术字资源", priority = 2160)]
    static void CheckUnuseAssetInArtLable()
    {
        CheckAndDeleteUnuseAssetInArtLable(false, true);
    }

    /// <summary>
    /// 检查并删除ArtLable下没用的资源
    /// </summary>
    //[MenuItem("Tools/AssetsCheckTools/DeleteUnuseAssetInArtLable", priority = 2113)]
    //static void DeleteUnuseAssetInArtLable2()
    //{
    //    CheckAndDeleteUnuseAssetInArtLable(true);
    //}

    static string CheckAndDeleteUnuseAssetInArtLable(bool isToDelete, bool isToShowResult)
    {
        string uiPath = "Assets/RawResources/UI";
        string artLablePath = "Assets/RawResources/UI/Texture/Component/ArtLabel";

        string[] allPath = AssetDatabase.GetAllAssetPaths();
        List<string> prefabsPathList = new List<string>();
        List<string> artLableAssetPathList = new List<string>();

        for (int i = 0; i < allPath.Length; i++)
        {
            EditorUtility.DisplayProgressBar("正在检查无用艺术字资源", allPath[i], i * 1f / allPath.Length);

            if (allPath[i].Contains(uiPath) && allPath[i].EndsWith(".prefab"))
            {
                prefabsPathList.Add(allPath[i]);
            }

            if (allPath[i].Contains(artLablePath) && allPath[i] != artLablePath && allPath[i].Contains("."))
            {
                artLableAssetPathList.Add(allPath[i]);
            }
        }

        List<string> dependencies = new List<string>(AssetDatabase.GetDependencies(prefabsPathList.ToArray()));

        List<string> unusePathList = new List<string>();
        for (int i = 0; i < artLableAssetPathList.Count; i++)
        {
            if (!dependencies.Contains(artLableAssetPathList[i]))
            {
                unusePathList.Add(artLableAssetPathList[i]);
            }
        }
        unusePathList.Sort();

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < unusePathList.Count; i++)
        {
            if (unusePathList[i].EndsWith(".fnt") && !unusePathList.Contains(unusePathList[i].Replace(".fnt", ".fontsettings")))
            {
                string temStr = unusePathList[i].Replace(".fnt", ".fontsettings");
                if (!unusePathList.Contains(temStr) && AssetDatabase.LoadAssetAtPath<Object>(temStr) != null)
                {
                    unusePathList.RemoveAt(i);

                    i--;
                    continue;
                }               
            }

            sb.AppendLine(unusePathList[i]);
        }

        if(isToDelete)
        {
             for (int i = 0; i < unusePathList.Count; i++)
             {
                 EditorUtility.DisplayProgressBar("正在删除资源", unusePathList[i], i * 1f / unusePathList.Count);

                 AssetDatabase.DeleteAsset(unusePathList[i]);
             }

             AssetDatabase.Refresh();
        }

        string msg = string.Empty;
        if (sb.Length > 0)
        {
            if (isToDelete)
            {
                sb.Insert(0, string.Format("在ArtLabel目录下，删除了以下{0}个资源：\n", unusePathList.Count));
            }
            else sb.Insert(0, string.Format("在ArtLabel目录下，以下{0}个资源可能是无用的，请确认后删除：\n", unusePathList.Count));
            msg = sb.ToString();
        }
        else
        {
            msg = "在ArtLabel目录下未发现无用资源";
        }

        EditorUtility.ClearProgressBar();
        if (isToShowResult)
        {
            Debug.Log(msg);
            ShowDisplayDialo("处理结果", msg);
            SaveStringToFile(msg);
        }

        return msg;
    }


    /// <summary>
    /// 查找场景目录下无用的预设
    /// </summary>
    [MenuItem("Tools/AssetsCheckTools/7.检查场景目录下无用预设", priority = 2170)]
    static void CheckUnusePrefabInSceneFolder()
    {
        CheckUnusePrefabInSceneFolder(true);
    }

     [MenuItem("Tools/AssetsCheckTools/8.检查UI目录下无用图片", priority = 2180)]
    static void CheckUnsuseUiAssets()
    {
        DoCheckUnsuseUiAssets(false, true);
    }

    [MenuItem("Tools/AssetsCheckTools/9.分镜资源处理", priority = 2190)]
    static string CheckCutsceneAssets()
    {
        string msg = string.Empty;

        string[] allPath = AssetDatabase.GetAllAssetPaths();
        string storyPath = "Assets/RawResources";

        List<string> prefabList = new List<string>();

        for (int i = 0; i < allPath.Length; i++)
        {
            EditorUtility.DisplayProgressBar("正在分析分镜资源", allPath[i], i * 1f / allPath.Length);
            if (allPath[i].Contains(storyPath) && allPath[i] != storyPath && allPath[i].EndsWith(".prefab") && allPath[i].Contains("sto_"))
            {
                prefabList.Add(allPath[i]);
            }            
        }

        StringBuilder sb = new StringBuilder();
        bool isToSave = false;
        for (int i = 0; i < prefabList.Count;i++ )
        {
            GameObject go = AssetDatabase.LoadAssetAtPath(prefabList[i], typeof(GameObject)) as GameObject;            
          
            bool isChange = false;
            bool isControlParticleChange = false;
            PlayableDirector director = go.GetComponent<PlayableDirector>();
            if(director != null)
            {
                Camera[] cameras = go.GetComponentsInChildren<Camera>();
                for(int j = 0;j<cameras.Length;j++)
                {                    
                    AudioListener audioListener = cameras[j].GetComponent<AudioListener>();
                    
                    if (audioListener)
                    {
                        if (audioListener.enabled)
                        {
                            isChange = true;
                        }
                        audioListener.enabled = false;
                    }

                    if (cameras[j].useOcclusionCulling || cameras[j].allowHDR || cameras[j].allowMSAA)
                    {
                        isChange = true;
                    }

                    cameras[j].useOcclusionCulling = false;
                    cameras[j].allowHDR = false;
                    cameras[j].allowMSAA = false;
                }

                if (director.playableAsset == null)
                {
                    sb.AppendLine(string.Format("{0}：丢失了TimeLine资源", prefabList[i]));
                }
                else
                {
                    PlayableAsset timeLine = director.playableAsset;
                    foreach (var tem in timeLine.outputs)
                    {
                        if (tem.sourceObject.GetType().Equals(typeof(AnimationTrack)))
                        {
                            AnimationTrack track = tem.sourceObject as AnimationTrack;                           
                            IEnumerable<TimelineClip> trackClips = track.GetClips();
                            IEnumerator<TimelineClip> temTrackAsset = trackClips.GetEnumerator();
                            while (temTrackAsset.MoveNext())
                            {
                                AnimationPlayableAsset temAsset = temTrackAsset.Current.asset as AnimationPlayableAsset;
                                if (temAsset != null && temAsset.clip == null)
                                {
                                    sb.AppendLine(string.Format("{0} 下的{1} 丢失了动作资源", prefabList[i], tem.sourceObject.name));
                                }                                                               
                            }
                        }
                        else if (tem.sourceObject.GetType().Equals(typeof(ControlTrack)))
                        {
                            ControlTrack track = tem.sourceObject as ControlTrack;
                            IEnumerable<TimelineClip> trackClips = track.GetClips();
                            IEnumerator<TimelineClip> temTrackAsset = trackClips.GetEnumerator();
                            while (temTrackAsset.MoveNext())
                            {
                                ControlPlayableAsset temAsset = temTrackAsset.Current.asset as ControlPlayableAsset;
                                if (temAsset != null && temAsset.updateParticle)
                                {
                                    temAsset.updateParticle = false;
                                    isChange = true;
                                    isControlParticleChange = true;
                                    //sb.AppendLine(string.Format("{0}：下{1} 的Control Particle 没有取消勾选", prefabList[i], tem.sourceObject.name));
                                }
                            }
                        }
                        //else if (tem.sourceObject.GetType().Equals(typeof(ActivationTrack)))
                        //{

                        //}
                        //else if (tem.sourceObject.GetType().Equals(typeof(CinemachineTrack)))
                        //{

                        //}
                    }
                }
            }

            if (isControlParticleChange)
            {
                EditorUtility.SetDirty(director.playableAsset);
            }
            if(isChange)
            {
                EditorUtility.SetDirty(go);
                isToSave = true;
            }
        }

        if(isToSave)
        {
            AssetDatabase.SaveAssets();
        }

        EditorUtility.ClearProgressBar();

        if(sb.Length > 0)
        {
            msg = sb.ToString();
        }
        else
        {
            msg = "分镜资源检查完成，没发现问题";
        }

        ShowDisplayDialo("处理结果", msg);
        Debug.Log(msg);

        return msg;
    }

    [MenuItem("Tools/AssetsCheckTools/10.输出RawResources目录下所有prefab预挂载脚本信息", priority = 2191)]
    public static void DetectPrefabScript()
    {
        FileStream mFile;
        BinaryWriter m_kFileWriter;

        string path = Application.dataPath + "/../prefabScripInfo.txt";
        File.Delete(path);

        mFile = new FileStream(path, FileMode.Create);
        m_kFileWriter = new BinaryWriter(mFile);

        string strResPath = "/RawResources";

        int i;
        int j;
        UnityEngine.Object[] arrDeps;
        UnityEngine.Object kSrcObj;
        string strAssetPath;
        int iIndex;
        string strDepPath;

        Dictionary<string, bool> scriptDic = new Dictionary<string, bool>();

        UnityEngine.Object[] arrSrc = new UnityEngine.Object[1];
        Dictionary<string, HashSet<string>> mapRefRes = new Dictionary<string, HashSet<string>>();
        string strPath = Application.dataPath + strResPath;
        FileInfo[] arrFI = IxFileFunc.FindAllFiles(strPath, "*.prefab", false);
        for (i = 0; i < arrFI.Length; i++)
        {
            strAssetPath = arrFI[i].FullName.ToLower();
            strAssetPath = strAssetPath.Replace('\\', '/');
            iIndex = strAssetPath.IndexOf("/assets/");
            strAssetPath = strAssetPath.Substring(iIndex + 1);
            kSrcObj = AssetDatabase.LoadAssetAtPath(strAssetPath, typeof(UnityEngine.Object));
            if (kSrcObj == null)
            {
                Logger.LogWarn("Load asset failed: " + kSrcObj);
                continue;
            }
            arrSrc[0] = kSrcObj;
            arrDeps = EditorUtility.CollectDependencies(arrSrc);
            for (j = 0; j < arrDeps.Length; j++)
            {
                strDepPath = AssetDatabase.GetAssetPath(arrDeps[j]);
                if (strDepPath.LastIndexOf(".cs") == -1 || scriptDic.ContainsKey(strDepPath))// || strDepPath.Contains("Plugins"))
                {
                    continue;
                }

                if (strDepPath.Contains("Plugins"))
                {
                    scriptDic[strDepPath] = true;
                }
                else
                {
                    strDepPath += ("  错误+++ Script 脚本  " + kSrcObj);
                }

                //strDepPath += (" | " + kSrcObj);

                Debug.LogError(strDepPath);

                string[] formatStrs = new string[] { strDepPath, "\r\n" };
                m_kFileWriter.Write(string.Concat(formatStrs).ToCharArray());
                m_kFileWriter.Flush();
            }
        }
        m_kFileWriter.Flush();
        m_kFileWriter.Close();
    }

    static void GetIgnorPathInResourceManagerLua(string path)
    {
        string newPath = Application.dataPath.Replace("Assets","") + path;
        string msg = File.ReadAllText(newPath);
        if (msg.Contains("effectPathConfig"))
        {
            string markStr = "effectPathConfig";
            int index = msg.IndexOf(markStr) + markStr.Length;
            msg = msg.Substring(index, msg.Length - index);

            markStr = "{";
            index = msg.IndexOf(markStr) + markStr.Length;
            msg = msg.Substring(index, msg.Length - index);

            markStr = "}";
            index = msg.IndexOf(markStr);
            msg = msg.Substring(0, index);
           
            string[] lines = msg.Split(new string[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length;i++ )
            {
                string temStr = lines[i].Trim();
                if(!temStr.StartsWith("--") && !string.IsNullOrEmpty(temStr))
                {
                    markStr = "=";
                    index = temStr.IndexOf(markStr);
                    if (temStr.Contains(markStr))
                    {
                        index = temStr.IndexOf(markStr) + markStr.Length;
                        temStr = temStr.Substring(index, temStr.Length - index);
                    }


                    markStr = ",";
                    index = temStr.IndexOf(markStr);
                    if(temStr.Contains(markStr))
                    {
                        index = temStr.IndexOf(markStr);
                        temStr = temStr.Substring(0, index);
                    }
                    temStr = temStr.Trim();
                    temStr = temStr.Replace("\"", "");
                    temStr = temStr.Trim();
                    if(!unuseUiTextureIgnorPathList.Contains(temStr))
                    {
                        unuseUiTextureIgnorPathList.Add(temStr);
                    }
                }
            }
        }
    }

    static string DoCheckUnsuseUiAssets(bool isToDelete, bool isToShowResult)
    {
        string msg = string.Empty;
       
        string uiTexturePath = "Assets/RawResources/UI/Texture";
        string uiPrefabPath = "Assets/RawResources/UI/Prefab";
        string[] allPath = AssetDatabase.GetAllAssetPaths();

       

        List<string> prefabList = new List<string>();

        List<string> textureList = new List<string>();

        string resourceManagerLuaFileName = "ResManager.lua";
        string resourceManagerLuaPath = string.Empty;

        for (int i = 0; i < allPath.Length; i++)
        {
            EditorUtility.DisplayProgressBar("正在分析UI资源", allPath[i], i * 1f / allPath.Length);
            if (allPath[i].Contains(uiPrefabPath) && allPath[i] != uiPrefabPath && allPath[i].EndsWith(".prefab"))
            {
                prefabList.Add(allPath[i]);
            }

            if (allPath[i].Contains(uiTexturePath) && allPath[i] != uiTexturePath && allPath[i].Contains("."))
            {
                textureList.Add(allPath[i]);
            }

            if (allPath[i].EndsWith(resourceManagerLuaFileName) && !allPath[i].Contains("LuaJIT"))
            {
                resourceManagerLuaPath = allPath[i];
            }
        }

        if(!string.IsNullOrEmpty(resourceManagerLuaPath))
        {
            //增加ResManager.lua文件里的effectPathConfig字段下配置的资源列表
            //GetIgnorPathInResourceManagerLua(resourceManagerLuaPath);
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabList.ToArray());
        List<string> dependenciesList = new List<string>(dependencies);

        List<string> unsuseList = new List<string>();
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < textureList.Count;i++ )
        {
            EditorUtility.DisplayProgressBar("正在检查无用UI资源", textureList[i], i * 1f / textureList.Count);           

            if (!dependenciesList.Contains(textureList[i]) && !IsToIgnorUiUnusePath(textureList[i]))
            {
                unsuseList.Add(textureList[i]);               
            }
        }

        unsuseList.Sort();
        for (int i = 0; i < unsuseList.Count; i++)
        {
            sb.AppendLine(unsuseList[i]);
        }

        string temMsg = string.Empty;
        if(isToDelete)
        {
            for (int i = 0; i < unsuseList.Count; i++)
            {
                EditorUtility.DisplayProgressBar("正在删除资源", unsuseList[i], i * 1f / unsuseList.Count);

                AssetDatabase.DeleteAsset(unsuseList[i]);
            }

            AssetDatabase.Refresh();

            temMsg = string.Format("以下{0}个资源已经被删除：\n", unsuseList.Count);
        }
        else
        {
            temMsg = string.Format("以下{0}个资源可能是无用资源，请确认后删除：\n", unsuseList.Count);
        }

        sb.Insert(0, temMsg);
        msg = sb.ToString();

        if(isToShowResult)
        {
            Debug.Log(msg);
            ShowDisplayDialo("处理结果", msg);
            SaveStringToFile(msg);
        }

        EditorUtility.ClearProgressBar();

        return msg;
    }

    /// <summary>
    /// UI无用资源检测是忽略的目录
    /// </summary>
    static List<string> unuseUiTextureIgnorPathList = new List<string>()
    {
        "UI/Texture/ItemIcon",
        "UI/Texture/Public/ItemCell",
        "UI/Texture/ToolTip",
        "Effect/UI/Prefab/Soul",
        "UI/Texture/BoundaryLevel/level",
        "UI/Texture/BoundaryLevel/LevelTextIcon",
        "UI/Texture/Skill",
        "UI/Texture/Shop/MainPanel",
        "UI/Texture/Task/TaskEscort",
        "UI/Texture/Shape/stageName",
        "UI/Texture/Shape/petName",
        "UI/Texture/Shape/mountName",
        "UI/Texture/Shape/shapeName2",
        "UI/Texture/MainUI/RoleAvatar/StateIcon",
        "UI/Texture/Public",
        "UI/Texture/OpenServer/SevenDay",
        "UI/Texture/MainUI/RoleAvatar/Avatar",
        "UI/Texture/MainUI/RoleAvatar/CareerName",
        "UI/Texture/Avatar/Boss",
        "Effect/Title",
        "UI/Texture/PlayerTitle",
        "UI/Texture/Public/ModuleTitle",
        "UI/Texture/SceneMap",
        "UI/Texture/SmallMap/Point",
        "UI/Texture/CopyBoss/BossAvatar",
        "UI/Texture/MainUI/Task",
        "UI/Texture/Guide/NewFunc",
        "UI/Texture/Guide/NewFunc/SkillName",
        "UI/Texture/Vip",
        "UI/Texture/Component/Number/Vip",
        "UI/Texture/Component/Number",
        "UI/Texture/Login/Career",
        "UI/Texture/Login",
        "UI/Texture/Rank",
        "UI/Texture/Chat/Face",
        "UI/Texture/Guild/GuildSkill/Icon",
        "UI/Texture/Daily/ActivityIcon",
        "UI/Texture/OpenServer/DeityBook/Book",
        "UI/Texture/ChangeCareer",
        "UI/Texture/Public/Common",
        "UI/Texture/OpenServer/RankRush",
        "UI/Texture/MainUI/Chat",
        "UI/Texture/CopyHall/PersonalPK/StageIcon",
        "UI/Texture/CopyHall/PersonalPK/StageName",

        "UI/Texture/CopyHall/CopyBloodMatrix/BossAvatar",
        "UI/Texture/CopyHall/CopyBloodMatrix/BossBg",
        "UI/Texture/CopyHall/CopyBloodMatrix/BossName",
        "Assets/RawResources/UI/Texture/Component/Hp",
        "Assets/RawResources/UI/Texture/Resurgence",
        "UI/Texture/Shop/PickAssistant",

        "UI/Texture/Story",


        "UI/Texture/Shape/star",
        "UI/Texture/Shape/starBack",
        "UI/Texture/Public/imgBigLock",
        "UI/Texture/Team/teamMain3",
        "UI/Texture/Team/teamMain2",
        "UI/Texture/Team/team6",
        "UI/Texture/Team/team7",
        "UI/Texture/CopyHall/BossMode/9001",
        "UI/Texture/CopyHall/BossMode/19001",
        "UI/Texture/CopyHall/BossMode/29001",
        "UI/Texture/CopyHall/BossMode/39001",
        "UI/Texture/CopyHall/BossMode/49001",
        "UI/Texture/CopyHall/BossMode/9001",
        "UI/Texture/CopyHall/BossMode/19001",
        "UI/Texture/CopyHall/BossMode/29001",
        "UI/Texture/CopyHall/BossMode/39001",
        "UI/Texture/CopyHall/BossMode/49001",
        "UI/Texture/CopyHall/BossMode/59001",
        "UI/Texture/CopyHall/BossMode/69001",
        "UI/Texture/Rank/rank3",
        "UI/Texture/Rank/rank4",
        "UI/Texture/Rank/rank6",
        "UI/Texture/Rank/rank8",
        "UI/Texture/Rank/rank10",
        "UI/Texture/Public/Common/commonbg10",
        "UI/Texture/ToolTip/tipsStoneRed",
        "UI/Texture/ToolTip/tipsStoneBlue",

        "UI/Texture/Component/ArtLabel",

        "Assets/RawResources/UI/Texture/Friend/mailRead.png",
        "Assets/RawResources/UI/Texture/Team/teamMain13.png",
        "Assets/RawResources/UI/Texture/Team/teamMain11.png",
        "Assets/RawResources/UI/Texture/CopyHall/PersonalPK/ChestOpened.png",
        "Assets/RawResources/UI/Texture/Loading/ExpGroupCopy_LoadingBg0001.jpg",
        "Assets/RawResources/UI/Texture/Team/teamMain4.png",
        "Assets/RawResources/UI/Texture/Loading/Normal_LoadingBg0001.jpg",
        "Assets/RawResources/UI/Texture/Loading/Normal_LoadingBg0002.jpg",
        "Assets/RawResources/UI/Texture/Team/teamMain10.png",
        "Assets/RawResources/UI/Texture/Component/ProgressBar/bossHpGreen.png",
        "Assets/RawResources/UI/Texture/Guild/GuildBeastGod/GuildBeastGodProgressArrow2.png",
        "Assets/RawResources/UI/Texture/MainUI/TopMenuBtn/btnAutoFight.png",
        "Assets/RawResources/UI/Texture/Loading/Normal_LoadingBg0003.jpg",
        "Assets/RawResources/UI/Texture/Team/teamMain9.png",
        "Assets/RawResources/UI/Texture/Team/teamMain7.png",
        "Assets/RawResources/UI/Texture/CopyHall/PersonalPK/ChestAvailable.png",
        "Assets/RawResources/UI/Texture/Team/teamMain12.png",
        "Assets/RawResources/UI/Texture/Rune/runeAdd.png",
        "Assets/RawResources/UI/Texture/CopyBoss/BossWorld/bossAvatar3.png",
        "Assets/RawResources/UI/Texture/CopyBoss/BossWorld/bossAvatar4.png",
        "Assets/RawResources/UI/Texture/Team/team3w16",

    };

    static bool IsToIgnorUiUnusePath(string path)
    {
        for (int i = 0; i < unuseUiTextureIgnorPathList.Count;i++ )
        {
            if (path.Contains(unuseUiTextureIgnorPathList[i]))
            {
                return true;
            }
        }

        return false;
    }

    static string CheckUnusePrefabInSceneFolder(bool isToShowResult)
    {
        string sceneFolder = "Assets/RawResources/Scene";
        List<string> scenePathList = new List<string>();
        List<string> prefabsPathList = new List<string>();       

        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        for (int i = 0; i < allAssets.Length;i++ )
        {
            EditorUtility.DisplayProgressBar("正在分析场景引用的资源", allAssets[i], i * 1f / allAssets.Length);
            if (allAssets[i].Contains(sceneFolder) && allAssets[i] != sceneFolder)
            {
                if (allAssets[i].EndsWith(".unity")) scenePathList.Add(allAssets[i]);

                if (allAssets[i].EndsWith(".prefab")) prefabsPathList.Add(allAssets[i]);
            }
        }

        List<string> dependencies = new List<string>(AssetDatabase.GetDependencies(scenePathList.ToArray()));

        int count = 0;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < prefabsPathList.Count;i++ )
        {
            EditorUtility.DisplayProgressBar("正在检查场景没用的预设", prefabsPathList[i], i *1f / prefabsPathList.Count);
            if (!dependencies.Contains(prefabsPathList[i]))
            {
                sb.AppendLine(prefabsPathList[i]);
                count++;
            }
        }

        string msg = string.Empty;
        if (sb.Length > 0)
        {
            sb.Insert(0, string.Format("在{0}目录下，以下{1}个预设(Prefab)可能是无用的，请确认后删除：\n", sceneFolder, count));
            msg = sb.ToString();
        }
        else
        {
            msg = string.Format("在{0}目录下未发现无用的预设", sceneFolder);
        }

        EditorUtility.ClearProgressBar();

        if (isToShowResult)
        {
            Debug.Log(msg);
            ShowDisplayDialo("处理结果", msg);
            SaveStringToFile(msg);
        }

        return msg;
    }

    private static void SaveStringToFile(string msg)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        string bossRefreshPointJsonFileName = Application.dataPath.Replace("Assets","") + "result.txt";
        if (File.Exists(bossRefreshPointJsonFileName))
        {
            File.Delete(bossRefreshPointJsonFileName);
        }
        File.WriteAllText(bossRefreshPointJsonFileName, msg);
        ShowDisplayDialo("处理结果保存在这里", bossRefreshPointJsonFileName);  
#endif
    }
    

    private static bool CheckScriptMissing(GameObject go, ref List<GameObject> missingGoList)
    {
        bool missing = false;
        if(go != null)
        {
            Dictionary<GameObject, List<Component>> components = new Dictionary<GameObject, List<Component>>();
            GetComponentsInGo(go, ref components);

            foreach(KeyValuePair<GameObject,List<Component>> keyValue in components)
            {
                for(int i = 0;i<keyValue.Value.Count;i++)
                {
                    if(keyValue.Value[i] == null)
                    {
                        if (missingGoList == null)
                        {
                            missingGoList = new List<GameObject>();
                        }
                        missingGoList.Add(keyValue.Key);
                        missing = true;
                    }
                }
            }
        }

        return missing;
    }

    private static bool CheckMaterialMissing(GameObject go, ref List<GameObject> missingGoList)
    {
        bool missing = false;
        if (go != null)
        {
            Renderer[] components = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    if(components[i].enabled && components[i].sharedMaterial == null)
                    {
                        if (missingGoList == null)
                        {
                            missingGoList = new List<GameObject>();
                        }
                        missingGoList.Add(components[i].gameObject);
                        missing = true;
                    }
                }
            }
        }

        return missing;
    }

    private static bool CheckMeshMissing(GameObject go, ref List<GameObject> missingGoList)
    {
        bool missing = false;
        if (go != null)
        {
            MeshFilter[] components = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].sharedMesh == null)
                {
                    if (missingGoList == null)
                    {
                        missingGoList = new List<GameObject>();
                    }
                    missingGoList.Add(components[i].gameObject);
                    missing = true;
                }
            }

            if(!missing)
            {
                SkinnedMeshRenderer[] renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < renderers.Length;i++ )
                {
                    if(renderers[i].sharedMesh == null)
                    {
                        if (missingGoList == null)
                        {
                            missingGoList = new List<GameObject>();
                        }
                        missingGoList.Add(renderers[i].gameObject);
                        missing = true;
                    }
                }
            }
        }

        return missing;
    }

    private static int  CheckShaderMissing(Material mat)
    {
       
        if (mat != null)
        {
            if (mat.shader.name.ToLower().Contains("standard") && !mat.shader.name.ToLower().Contains("sgame") || mat.shader.name.ToLower().Contains("legacy Shaders"))
            {
                return 1;//有错误的shader
            }
            if (mat.shader.name.Contains("InternalErrorShader"))
            {
                return 2;//shader丢失
            }
        }

        return 0;
    }


    private static void GetComponentsInGo(GameObject go, ref Dictionary<GameObject,List<Component>> output)
    {
        if(go != null)
        {            
            List<Component> outputComponentList = null;
            if (output == null)
            {
                output = new Dictionary<GameObject, List<Component>>();
            }
            if(!output.ContainsKey(go))
            {
                output.Add(go, new List<Component>());
            }
            outputComponentList = output[go];

            Component[] components = go.GetComponents<Component>();
            outputComponentList.AddRange(components);

            Transform temTf = go.transform;
            for (int i = 0; i < temTf.childCount;i++ )
            {
                GetComponentsInGo(temTf.GetChild(i).gameObject, ref output);
            }
        }
    }

    [MenuItem("Tools/AssetsCheckTools/11.检查特效层级问题", priority = 2192)]
    static void CheckPsrAndRawImageOrder()
    {
       	string[] allPath = AssetDatabase.GetAllAssetPaths();

		string result = CheckPsrAndRawImageOrderInPaths (allPath);

		string bossRefreshPointJsonFileName = Application.dataPath.Replace("Assets","") + "LayerErrorResult.txt";
		if (File.Exists(bossRefreshPointJsonFileName))
		{
			File.Delete(bossRefreshPointJsonFileName);
		}
		File.WriteAllText(bossRefreshPointJsonFileName, result);
		ShowDisplayDialo("处理结果保存在这里", bossRefreshPointJsonFileName);  
    }

	static string CheckPsrAndRawImageOrderInPaths (string[] allPath)
    {
		string result = "";
        int checkedItemCount = 0;

        for (int i = 0; i < allPath.Length; i++)
        {
            EditorUtility.DisplayProgressBar("正在检查资源丢失", allPath[i], i * 1f / allPath.Length);
            if (allPath[i].Contains(rawResourcesPath) && allPath[i] != rawResourcesPath && !CheckIsToIgnore(allPath[i]))
            {
                GameObject go = AssetDatabase.LoadAssetAtPath(allPath[i], typeof(GameObject)) as GameObject;
				if(go == null)
				{
					continue;
				}
				ParticleSystemRenderer[] psrAry = go.transform.GetComponentsInChildren<ParticleSystemRenderer> ();
				int minOrder = -1;
				int maxOrder = -1;
				foreach(ParticleSystemRenderer p in psrAry)
				{
					int order = p.sortingOrder;
					if(minOrder<0)
					{
						minOrder = order;
					}

					if(maxOrder<0)
					{
						maxOrder = order;
					}

					if(order<minOrder)
					{
						minOrder = order;
					}

					if(order>maxOrder)
					{
						maxOrder = order;
					}
				}

				IxSortLayer[] slAry = go.transform.GetComponentsInChildren<IxSortLayer> ();
				foreach(IxSortLayer p in slAry)
				{
					int order = p._Order;
					if(minOrder<0)
					{
						minOrder = order;
					}

					if(maxOrder<0)
					{
						maxOrder = order;
					}

					if(order<minOrder)
					{
						minOrder = order;
					}

					if(order>maxOrder)
					{
						maxOrder = order;
					}
				}
				int diff = maxOrder - minOrder;
				if(diff>10)
				{
					result += allPath[i] + " \n";
				}
				checkedItemCount++;
            }
        }

        EditorUtility.ClearProgressBar();
		return result;
    }


    /// <summary>
    /// 查找使用到当前所选择的资源的全部资源
    /// </summary>
    [MenuItem("Tools/AssetsCheckTools/12.查找选中资源的依赖资源", priority = 2200)]
    static void FindAssetDependency()
    {
        Object[] objs = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

        string msg = string.Empty;

        if (objs.Length == 1)
        {
            if (objs[0] is DefaultAsset)
            {
                msg = "选择的是目录，请选中具体的某个资源";
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(objs[0]);

                StringBuilder sb = new StringBuilder();

                string[] dependencies = AssetDatabase.GetDependencies(path, false);

                List<string> depenList = new List<string>(dependencies);
                depenList.Sort();

                for (int i = 0; i < depenList.Count; i++)
                {
                    sb.AppendLine(depenList[i]);
                }

                if (sb.Length > 0)
                {
                    msg = string.Format("{0}的依赖资源如下：\n", path) + sb.ToString();
                }
                else
                {
                    msg = string.Format("{0}没有依赖其他资源", path);
                }
            }
        }
        else
        {

            if (objs.Length <= 0)
            {
                msg = "选择的资源为空，请选中某个资源后再执行该命令";
            }
            else
            {
                msg = "选择的资源大于一个，每次只能检查一个资源";
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log(msg);
        ShowDisplayDialo("检测结果", msg);
    }

    /// <summary>
    /// 检查RawImage
    /// </summary>
    [MenuItem("Tools/AssetsCheckTools/13.查找RawImage", priority = 2201)]
    static void FindAllRawImage()
    {
        GameObject[] selectedObjs = Selection.GetFiltered<GameObject>(SelectionMode.DeepAssets);

        string msg = string.Empty;

        if(selectedObjs != null && selectedObjs.Length > 0)
        {
            foreach(GameObject go in selectedObjs)
            {
                msg += CheckRawImage(go, AssetDatabase.GetAssetPath(go));
            }
        }
        else
        {
            //没有选中就用asset里面的
            DirectoryInfo dir = new DirectoryInfo(rawResourcesPath);
            FileInfo[] allPath = dir.GetFiles("*.prefab", SearchOption.AllDirectories);

            foreach (FileInfo info in allPath)
            {
                string f = info.FullName;
                f = f.Replace(@"\", @"/");
                int index = f.LastIndexOf(rawResourcesPath);
                f = f.Substring(index);

                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(f);

                msg += CheckRawImage(go, f);
            }
        }

        EditorUtility.ClearProgressBar();
        Debug.Log(msg);
        ShowDisplayDialo("检测结果", msg);
        SaveStringToFile(msg);
    }

    [MenuItem("Tools/AssetsCheckTools/14.检查场景命名和预设", priority = 2201)]
    private static void CheckSceneImportGameObjects()
    {
        Scene scene = SceneManager.GetActiveScene();

        if(!scene.path.Contains("RawResources/Scene"))
        {
            return;
        }

        GameObject sceneRoot = null;

        GameObject[] roots = scene.GetRootGameObjects();
        for(int i = 0; i < roots.Length; i++)
        {
            if(roots[i].name == "SceneRoot")
            {
                sceneRoot = roots[i];
            }
        }

        if(sceneRoot == null)
        {
            string errorMsg = "没有找到SceneRoot节点，请检查命名，注意：不允许有空格";
            Debug.LogError(errorMsg);
            ShowDisplayDialo("检查结果", errorMsg);
            return;
        }

        Sunshine sunshine = sceneRoot.GetComponentInChildren<Sunshine>();

        if(sunshine != null)
        {
            string errorMsg = "没有找到SceneRoot下不允许挂Sunshine节点";
            Debug.LogError(errorMsg);
            ShowDisplayDialo("检查结果", errorMsg);
            return;
        }

        RenderSetting[] renderSettings = sceneRoot.GetComponentsInChildren<RenderSetting>();
        if(renderSettings.Length != 1)
        {
            string errorMsg = "SceneRoot下必须有且只有一个激活的RenderSetting节点";
            Debug.LogError(errorMsg);
            ShowDisplayDialo("检查结果", errorMsg);
            return;
        }

        Terrain terrain = Transform.FindObjectOfType<Terrain>(); 
        if(terrain != null)
        {
            string errorMsg = "检测到地形，项目不允许使用地形";
            Debug.LogError(errorMsg);
            ShowDisplayDialo("检查结果", errorMsg);
            return;
        }

        SceneEffectCulling sceneEffectCulling = FindObjectOfType<SceneEffectCulling>();
        if(sceneEffectCulling == null)
        {
            string errorMsg = "场景特效没用挂载SceneEffectCulling脚本";
            Debug.LogError(errorMsg);
            ShowDisplayDialo("检查结果", errorMsg);
            return;
        }

        ShowDisplayDialo("检查结果", "检测通过");
    }

    [MenuItem("Tools/AssetsCheckTools/15.检查冗余UV", priority = 2202)]
    private static void CheckFBXUV()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { "Assets/RawResources" });

        foreach(var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshFilter meshFilter = fbx.GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                if (path.Contains("Scene"))
                {
                    if(meshFilter.sharedMesh.uv3 != null && meshFilter.sharedMesh.uv3.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 2 uv", path));
                    }
                    else if(meshFilter.sharedMesh.uv4 != null && meshFilter.sharedMesh.uv4.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 2 uv", path));
                    }
                }
                else
                {
                    if(meshFilter.sharedMesh.uv2 != null && meshFilter.sharedMesh.uv2.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 1 uv", path));
                    }
                    else if(meshFilter.sharedMesh.uv3 != null && meshFilter.sharedMesh.uv3.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 1 uv", path));
                    }
                }
            }

            SkinnedMeshRenderer[] skinnedMesheRenderers = fbx.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach(SkinnedMeshRenderer renderer in skinnedMesheRenderers)
            {
                if (path.Contains("Scene"))
                {
                    if(renderer.sharedMesh.uv3 != null && renderer.sharedMesh.uv3.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 2 uv", path));
                    }
                    else if(renderer.sharedMesh.uv4 != null && renderer.sharedMesh.uv4.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 2 uv", path));
                    }
              
                }
                else
                {
                    if(renderer.sharedMesh.uv2 != null && renderer.sharedMesh.uv2.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 1 uv", path));
                    }
                    else if(renderer.sharedMesh.uv3 != null && renderer.sharedMesh.uv3.Length > 0)
                    {
                        Debug.LogError(string.Format("FBX: {0}, mesh has more than 1 uv", path));
                    }
                }
            }
        }

        Debug.Log("扫描完成");
    }

    [MenuItem("Tools/AssetsCheckTools/16.检查非法shader", priority = 2202)]
    private static void CheckInvalidShader()
    {
        Scene scene = SceneManager.GetActiveScene();

        GameObject[] roots = scene.GetRootGameObjects();

        List<GameObject> allGoes = new List<GameObject>();

        foreach (var go in roots)
        {
            Transform[] childs = go.transform.GetComponentsInChildren<Transform>(true);
            foreach (var child in childs)
            {
                allGoes.Add(child.gameObject);
            }
        }

        foreach (var go in allGoes)
        {
            Renderer render = go.GetComponent<Renderer>();
            if (render != null)
            {
                Material[] mats = render.sharedMaterials;

                foreach (Material mat in mats)
                {
                    if (mat != null && mat.shader != null && mat.shader.name.Contains("Legacy Shaders"))
                    {
                        GameObject parent = go.transform.parent.gameObject;
                        Debug.LogError(string.Format("GameObject: {0}-{1}的shader: {2}非法", parent.name, go.name, mat.shader.name));
                    }
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { "Assets/RawResources" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = mat.shader;

            if (shader != null && (shader.name.StartsWith("Standard") || shader.name.Contains("Legacy Shaders") || shader.name.Contains("Unlit") || shader.name.Contains("Error")))
            {
                Debug.LogError(string.Format("材质: {0}, shader: {1}, 非法", path + "/" + mat.name, mat.shader.name));
            }
        }
      
        Debug.Log("扫描完成");
    }

    [MenuItem("Tools/AssetsCheckTools/17.检查非法MeshCollider", priority = 2202)]
    private static void CheckInvalidMeshCollider()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { "Assets/RawResources" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if(go.GetComponent<MeshCollider>() != null)
            {
                Debug.LogError(string.Format("模型{0}有MeshCollider，非法", go.name));
            }
        }

        guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/RawResources" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshCollider[] colliders = go.GetComponents<MeshCollider>();

            if(colliders.Length > 0)
            {
                Debug.LogError(string.Format("预设{0}有MeshCollider，非法", path + "/" + go.name));
            }

            colliders = go.GetComponentsInChildren<MeshCollider>();

            if(colliders.Length > 0)
            {
                Debug.LogError(string.Format("预设{0}有MeshCollider，非法", path + "/" + go.name + "-" + colliders[0].name));
            }
        }
      
        Debug.Log("扫描完成");
    }

    [MenuItem("Tools/AssetsCheckTools/18.检查含有SkinnedMeshRenderer的粒子", priority = 2202)]
    private static void CheckParticlesWithSkinnedMeshRenderer()
    {
        Debug.Log("开始扫描");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/RawResources" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            ParticleSystem[] pses = go.GetComponentsInChildren<ParticleSystem>();

            for (int i = 0; i < pses.Length; i++)
            {
                ParticleSystem ps = pses[i];

                if (ps != null && ps.shape.shapeType == ParticleSystemShapeType.SkinnedMeshRenderer)
                {
                    Debug.LogError(string.Format("{0}的{1}使用了SkinnedMeshRenderer做为Shape类型", path, ps.name));
                }
            }
        }

        Debug.Log("扫描完成");
    }


    [MenuItem("Tools/AssetsCheckTools/19.检查使用法帖材质的模型切线", priority = 2202)]
    private static void CheckNormalMapTangent()
    {
        Debug.Log("开始扫描");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/RawResources/Scene" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();

            if (meshFilter != null && meshRenderer != null)
            {
                Material material = meshRenderer.sharedMaterial;

                if (material != null && material.HasProperty("_UseNormalMap"))
                {
                    if (material.GetFloat("_UseNormalMap") == 1)
                    {
                        Mesh mesh = meshFilter.sharedMesh;
                        if (mesh != null)
                        {
                            if (mesh.tangents.Length <= 0)
                            {
                                Debug.LogError(string.Format("预设:{0}, 模型:{1} 需要切线，因为材质开了法线贴图", path, AssetDatabase.GetAssetPath(mesh)));
                            }
                        }
                    }
                }
            }
        }

        Debug.Log("扫描完成");
    }

    static private string CheckRawImage(GameObject go, string path)
    {
        string msg = string.Empty;

        if (go)
        {
            RawImage[] raws = go.transform.GetComponentsInChildren<RawImage>(true);

            if (raws != null && raws.Length > 0)
            {
                string str = string.Empty;
                foreach (RawImage raw in raws)
                {
                    str += string.Format("name={0} texture={1}, ", raw.gameObject.name, raw.texture);
                }
                msg = string.Format("{0} = [{1}]\n", path, str);
            }
        }

        return msg;
    }

    ///// <summary>
    ///// 检查RawImage
    ///// </summary>
    //[MenuItem("Tools/AssetsCheckTools/14.提取unity_builtin_extra包", priority = 2202)]
    static void Pack_unity_builtin_extra()
    {
        string packPath = "Resources/unity_builtin_extra";

        string msg = string.Empty;

        Object[] UnityAssets = AssetDatabase.LoadAllAssetsAtPath(packPath);

        msg += string.Format("pack={0}, GUID={1}", packPath, AssetDatabase.AssetPathToGUID(packPath));

        string path = rawResourcesPath + "/unity_builtin_extra";

        //create then folder
        if (AssetDatabase.IsValidFolder(path) == false)
        {
            AssetDatabase.CreateFolder(rawResourcesPath, "unity_builtin_extra");
        }

        foreach (var asset in UnityAssets)
        {
            AssetDatabase.CreateAsset(asset, string.Format("D:/unity_builtin_extra/", path));
            string assetPath = AssetDatabase.GetAssetPath(asset);

            msg += string.Format("name={0}, path={1}, GUID={2}", asset.name, assetPath, AssetDatabase.AssetPathToGUID(assetPath));
        }

        EditorUtility.ClearProgressBar();
        Debug.Log(msg);
        ShowDisplayDialo("提取完成", "");
        SaveStringToFile(msg);
    }

    //[MenuItem("Tools/AssetsCheckTools/15.检查资源名称命名是否相同", priority = 2202)] 
    public static void CheckAssetNameEqual()
    {
        string name = "";
        Dictionary<string, List<string>> paths = new Dictionary<string, List<string>>();
        List<string> list = new List<string>();
        string[] names = AssetDatabase.GetAllAssetPaths();
        for (int i = 0; i < names.Length; i++)
        {
            if (!names[i].Contains("RawResources"))
            {
                continue;
            }
            if (names[i].LastIndexOf(".png") < 0)
            {
                continue;
            }
            int index = names[i].LastIndexOf("/");
            name = names[i].Substring(index + 1);
            if (paths.ContainsKey(name))
            {
                paths[name].Add(names[i]);
                if (!list.Contains(name))
                {
                    list.Add(name);
                }
            }
            else
            {
                paths.Add(name, new List<string>() { names[i] }); 
            }
        }
        if (list.Count > 0)
        {
            string msg = "相同文件名存在多个情况>";
            for (int i = 0; i < list.Count; i++)
            {
                msg += list[i];
                msg += "\n[\n";
                foreach (var item in paths[list[i]])
                {
                    msg += item;
                    msg += " , \n";
                }
            }
            Debug.Log(msg); 
            SaveStringToFile(msg);
            return;
        }
        Debug.Log("未发现异常命名资源文件.");
    }

}
