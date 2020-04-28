using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;
using System.Collections;

public class ResourceNotBeRefrenceWindow : EditorWindow
{
    private class ResourceDependencyInfo
    {
        public string path;
        public string[] dependencyPaths;        
    }
    private class SearchInfo
    {
        public SearchInfo(string guid, string path)
        {
            this.guid = guid;
            this.path = path;
            this.searched = false;
        }
        public string path;
        public string guid;
        public bool searched;
    }
    private class FindNoRefrenceSearchJob
    {
        private ResourceDependencyInfo[] m_AllResInfos;
        private List<SearchInfo> m_SearchInfos;
        private int m_StartIndex;
        private int m_EndIndex;
        public ManualResetEvent doneEvent;

        public System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();

        public FindNoRefrenceSearchJob()
        {
            doneEvent = new ManualResetEvent(false);
        }

        public void SetData(int startIndex, int length, ResourceDependencyInfo[] allResInfos, List<SearchInfo> searchInfos)
        {
            m_StartIndex = startIndex;
            if(m_StartIndex + length >= allResInfos.Length)
            {
                m_EndIndex = allResInfos.Length;
            }
            else
            {
                m_EndIndex = m_StartIndex + length;
            }
            m_AllResInfos = allResInfos;
            m_SearchInfos = searchInfos;
            doneEvent.Reset();
        }


        public void ThreadPoolCallback(System.Object threadContext)
        {
            try
            {
                stopWatch.Start();
                for (int i = m_StartIndex; i < m_EndIndex; ++i)
                {
                    ResourceDependencyInfo info = m_AllResInfos[i];
                    for (int j = 0; j < info.dependencyPaths.Length ; ++j)
                    {
                        string dependencyPath = info.dependencyPaths[j];
                        if (dependencyPath == info.path)
                        {
                            continue;
                        }
                        foreach (var it in m_SearchInfos)
                        {
                            if (dependencyPath.Equals(it.path))
                            {
                                it.searched = true;
                            }
                        }
                    }                   
                }
                stopWatch.Stop();            
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat(ex.Message);
            }

            doneEvent.Set();
        }
    }

    static public ResourceNotBeRefrenceWindow instance;
    //public static string NOT_BE_REFRENCE_SOURCE_TYPE = "t:Material t:Texture t:Prefab t:Model t:Script";
    public static string NOT_BE_REFRENCE_SOURCE_TYPE = "t:Material t:Texture t:Script";
    private Dictionary<string, List<string>> m_SearchResultDict;
    Vector2 mScroll = Vector2.zero;
    private string m_Src;
    private string m_SearchDir = ResourceRefrenceWindow.DEFAULT_SEARCH_DIR;

    void OnEnable() { instance = this; }
    void OnDisable() { instance = null; }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("资源目录:", GUILayout.Width(100));
        Rect srcTextFieldRect = EditorGUILayout.GetControlRect(GUILayout.Width(700));
        m_Src = EditorGUI.TextField(srcTextFieldRect, m_Src);
        m_Src = OnDrawElementAcceptDrop(srcTextFieldRect, m_Src);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("查找目录:", GUILayout.Width(100));
        Rect searchTextFieldRect = EditorGUILayout.GetControlRect(GUILayout.Width(700));
        m_SearchDir = EditorGUI.TextField(searchTextFieldRect, m_SearchDir);
        m_SearchDir = OnDrawElementAcceptDrop(searchTextFieldRect, m_SearchDir);
        if (GUILayout.Button("默认查找目录", GUILayout.Width(150)))
        {
            m_SearchDir = ResourceRefrenceWindow.DEFAULT_SEARCH_DIR;
        }
        if (GUILayout.Button("删除全部查找结果", GUILayout.Width(150)))
        {
            DeleteAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("查找未被引用的资源"))
        {            
            if (!Directory.Exists(m_Src))
            {
                EditorUtility.DisplayDialog("输入错误", string.Format("请输入目录!!"), "确定");
                return;
            }

            m_SearchResultDict = FindResourceNotBeRefrences();
        }
        EditorGUILayout.EndHorizontal();
        if (m_SearchResultDict == null || m_Src == "" || !Directory.Exists(m_SearchDir))
        {
            return;
        }

        
        mScroll = GUILayout.BeginScrollView(mScroll);




        DrawResultFields("Scene", typeof(SceneAsset));
        DrawResultFields("Prefab", typeof(GameObject));
        DrawResultFields("FBX", typeof(GameObject));
        DrawResultFields("Texture", typeof(UnityEngine.Object));
        DrawResultFields("Material", typeof(Material));
        DrawResultFields("Other", typeof(UnityEngine.Object));

        GUILayout.EndScrollView();
    }

    private string OnDrawElementAcceptDrop(Rect rect, string label)
    {
        if (rect.Contains(Event.current.mousePosition))
        {
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0 && !string.IsNullOrEmpty(DragAndDrop.paths[0]))
            {
                if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }

                if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    GUI.changed = true;

                    return string.Join("\" || \"", DragAndDrop.paths);
                }
            }
        }

        return label;
    }


    private void DrawResultFields(string srcType, Type type)
    {

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        List<string> list;
        if(m_SearchResultDict.TryGetValue(srcType, out list))
        {
            if (list.Count > 0 && DrawHeader(srcType))
            {
                foreach (string item in list)
                {
                    UnityEngine.Object go = AssetDatabase.LoadAssetAtPath(item, type);
                    EditorGUILayout.ObjectField(srcType, go, type, true);
                    sb.AppendLine(item);
                }
            }
        }

        string logpath = Application.dataPath + "/../Log/UnUse_" + srcType +".log";
        if (File.Exists(logpath))
            File.Delete(logpath);
        using (FileStream fs = File.Create(logpath))
        {
            byte[] data = System.Text.Encoding.Default.GetBytes(sb.ToString());
            fs.Write(data, 0, data.Length);
            fs.Flush();
        }
    }

    /// <summary>
    /// 查找对某个对象的引用
    /// </summary>
    [MenuItem("Tools/资源引用查找工具/查找未被引用的资源", false)]
    public static void ResourceRefrenceSearch()
    {
        GetWindow<ResourceNotBeRefrenceWindow>(false, "查找未被引用的资源", true).Show();
    }

    public Dictionary<string, List<string>> FindResourceNotBeRefrences()
    {
        Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();
        List<SearchInfo> searchInfos = new List<SearchInfo>();
        string[] searchGUIDArray = AssetDatabase.FindAssets(NOT_BE_REFRENCE_SOURCE_TYPE, new string[] { m_Src });
        //string[] srcPaths = new string[searchGUIDArray.Length];
        for(int i = 0; i < searchGUIDArray.Length; ++i)
        {
            searchInfos.Add(new SearchInfo(searchGUIDArray[i], AssetDatabase.GUIDToAssetPath(searchGUIDArray[i])));
        }
        string[] allGuids = AssetDatabase.FindAssets(ResourceRefrenceWindow.SEARCH_TYPE, new string[] { m_SearchDir });

        ShowProgress(0, allGuids.Length, 0);

        ResourceDependencyInfo[] allResources = new ResourceDependencyInfo[allGuids.Length];
        UnityEngine.Object[] roots = new UnityEngine.Object[1];
        for(int i = 0; i < allGuids.Length; ++i)
        {
            ResourceDependencyInfo info = new ResourceDependencyInfo();
            string guid = allGuids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            roots[0] = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
            UnityEngine.Object[] dependency = EditorUtility.CollectDependencies(roots);
            string[] dependencyPaths = new string[dependency.Length];
            for(int j = 0; j < dependency.Length; ++j)
            {
                dependencyPaths[j] = AssetDatabase.GetAssetPath(dependency[j]);
            }
            info.path = path;
            info.dependencyPaths = dependencyPaths;
            allResources[i] = info;
        }

        int threadCounts = Mathf.Min(Environment.ProcessorCount, allGuids.Length);
        FindNoRefrenceSearchJob[] searchJobsArray = new FindNoRefrenceSearchJob[threadCounts];
        ManualResetEvent[] events = new ManualResetEvent[threadCounts];
        for (int i = 0; i < threadCounts; ++i)
        {
            searchJobsArray[i] = new FindNoRefrenceSearchJob();
            events[i] = searchJobsArray[i].doneEvent;
        }
        int timeout = 600000;  // 10 分钟超时
        int index = 0;
        int step = 10;
        int startIndex = 0;
        //Less then step * threadCounts
        for(; index < threadCounts; index++)
        {
            if(index * step >= allGuids.Length)
            {
                break;
            }

            FindNoRefrenceSearchJob job = searchJobsArray[index];
            job.SetData(startIndex, step, allResources, searchInfos);
            ThreadPool.QueueUserWorkItem(job.ThreadPoolCallback);

            ShowProgress((float)index * step / (float)(allGuids.Length), allGuids.Length, index * step);

            startIndex += step;
        }

        for(; index < threadCounts; ++index)
        {
            searchJobsArray[index].doneEvent.Set();
        }
        
        for (int i = index * step; i < allGuids.Length; i += step)
        {
            index = WaitForDoFile(events, timeout);
            FindNoRefrenceSearchJob job = searchJobsArray[index];
            job.SetData(startIndex, step, allResources, searchInfos);
            ThreadPool.QueueUserWorkItem(job.ThreadPoolCallback);

            ShowProgress((float)i / (float)(allGuids.Length), allGuids.Length, i);

            startIndex += step;
        }

        WaitHandle.WaitAll(events, timeout);

        List<string> prefabList = new List<string>();
        List<string> sceneList = new List<string>();
        List<string> matList = new List<string>();
        List<string> fbxList = new List<string>();
        List<string> textureList = new List<string>();
        List<string> otherList = new List<string>();
        foreach (var iter in searchInfos)
        {
            if(!iter.searched)
            {
                string guid = iter.guid;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if(path.Contains("Lightmap") || path.Contains("ReflectionProbe"))
                {
                    continue;
                }
                if (path.EndsWith(".prefab"))
                {
                    prefabList.Add(path);
                    continue;
                }
                else if (path.EndsWith(".unity"))
                {
                    sceneList.Add(path);
                    continue;
                }
                else if (path.EndsWith(".mat"))
                {
                    matList.Add(path);
                    continue;
                }
                else if (path.EndsWith(".FBX"))
                {
                    fbxList.Add(path);
                    continue;
                }
                else if(path.EndsWith("png") || path.EndsWith("tga"))
                {
                    textureList.Add(path);
                    continue;
                }
                else
                {
                    otherList.Add(path);
                }
            }
        }

        dic.Add("Prefab", prefabList);
        dic.Add("Material", matList);
        dic.Add("Scene", sceneList);
        dic.Add("FBX", fbxList);
        dic.Add("Texture", textureList);
        dic.Add("Other", otherList);

        EditorUtility.ClearProgressBar();

        return dic;
    }

    private void DeleteAll()
    {
        int totalCount = 0;
        foreach (var pair in m_SearchResultDict)
        {
            for (int i = 0; i < pair.Value.Count; ++i)
            {
                ++totalCount;
            }
        }

        int count = 0;
        foreach (var pair in m_SearchResultDict)
        {
            for(int i = 0; i < pair.Value.Count; ++i)
            {
                AssetDatabase.DeleteAsset(pair.Value[i]);
                EditorUtility.DisplayProgressBar("正在删除", string.Format("删除 ({0}/{1}), please wait...", count, totalCount), (float)count / (float)totalCount);
                ++count;
            }
            pair.Value.Clear();
        }
        EditorUtility.ClearProgressBar();
    }

    private static int WaitForDoFile(ManualResetEvent[] events, int timeout)
    {
        int finished = WaitHandle.WaitAny(events, timeout);
        if (finished == WaitHandle.WaitTimeout)
        {
            // 超时
        }
        return finished;
    }

    private static int WaitForDoFile(List<ManualResetEvent> events, int timeout)
    {
        int finished = WaitHandle.WaitAny(events.ToArray(), timeout);
        if (finished == WaitHandle.WaitTimeout)
        {
            // 超时
        }
        events.RemoveAt(finished);
        return finished;
    }



    //集成NGUI方法，显示可折叠窗口
    public bool DrawHeader(string text) { return DrawHeader(text, text, false, false); }
    public bool DrawHeader(string text, string key, bool forceOn, bool minimalistic)
    {
        bool state = EditorPrefs.GetBool(key, true);

        if (!minimalistic) GUILayout.Space(3f);
        if (!forceOn && !state) GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        GUILayout.BeginHorizontal();
        GUI.changed = false;

        if (minimalistic)
        {
            if (state) text = "\u25BC" + (char)0x200a + text;
            else text = "\u25BA" + (char)0x200a + text;

            GUILayout.BeginHorizontal();
            GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.7f) : new Color(0f, 0f, 0f, 0.7f);
            if (!GUILayout.Toggle(true, text, "PreToolbar2", GUILayout.MinWidth(20f))) state = !state;
            GUI.contentColor = Color.white;
            GUILayout.EndHorizontal();
        }
        else
        {
            text = "<b><size=11>" + text + "</size></b>";
            if (state) text = "\u25BC " + text;
            else text = "\u25BA " + text;
            if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) state = !state;
        }

        if (GUI.changed) EditorPrefs.SetBool(key, state);

        if (!minimalistic) GUILayout.Space(2f);
        GUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
        if (!forceOn && !state) GUILayout.Space(3f);
        return state;
    }
    /// <summary>
    /// 显示进度条
    /// </summary>
    /// <param name="val"></param>
    /// <param name="total"></param>
    /// <param name="cur"></param>
    public static void ShowProgress(float val, int total, int cur)
    {
        EditorUtility.DisplayProgressBar("Searching", string.Format("Checking ({0}/{1}), please wait...", cur, total), val);
    }

}