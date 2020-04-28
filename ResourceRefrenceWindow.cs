using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading;
using System;
using System.IO;
using System.Collections;

public class ResourceRefrenceWindow : EditorWindow
{
    private class ResourceDependencyInfo
    {
        public string path;
        public string[] dependencyPaths;
        public bool beDependend;
    }

    private class FindRefrenceSearchJob
    {
        private ResourceDependencyInfo[] m_AllResInfos;
        private string m_SearchPath;
        private int m_StartIndex;
        private int m_EndIndex;
        public ManualResetEvent doneEvent;

        public FindRefrenceSearchJob()
        {
            doneEvent = new ManualResetEvent(false);
        }

        public void SetData(int startIndex, int length, ResourceDependencyInfo[] allResInfos, string searchPath)
        {
            m_StartIndex = startIndex;
            if (m_StartIndex + length >= allResInfos.Length)
            {
                m_EndIndex = allResInfos.Length;
            }
            else
            {
                m_EndIndex = m_StartIndex + length;
            }
            m_AllResInfos = allResInfos;
            m_SearchPath = searchPath;
            doneEvent.Reset();
        }


        public void ThreadPoolCallback(System.Object threadContext)
        {
            try
            {
                for (int i = m_StartIndex; i < m_EndIndex; ++i)
                {
                    ResourceDependencyInfo info = m_AllResInfos[i];
                    for (int j = 0; j < info.dependencyPaths.Length; ++j)
                    {
                        if (info.dependencyPaths[j].Equals(m_SearchPath))
                        {
                            info.beDependend = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat(ex.Message);
            }

            doneEvent.Set();
        }
    }

    static public ResourceRefrenceWindow instance;
    public static string DEFAULT_SEARCH_DIR = "Assets/RawResources";
    public static string SEARCH_TYPE = "t:Prefab t:Scene t:Material t:AnimatorController";
    private Dictionary<string, List<string>> m_SearchResultDict;
    Vector2 mScroll = Vector2.zero;
    private string m_Src;
    private string m_SearchDir = DEFAULT_SEARCH_DIR;

    void OnEnable() { instance = this; }
    void OnDisable() { instance = null; }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("资源文件:", GUILayout.Width(100));
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
            m_SearchDir = DEFAULT_SEARCH_DIR;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("查找引用"))
        {
            if (!File.Exists(m_Src))
            {
                EditorUtility.DisplayDialog("输入错误", string.Format("请输入文件!!"), "确定");
                return;
            }

            m_SearchResultDict = FindResourceRefrences();
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
        List<string> list;
        if(m_SearchResultDict.TryGetValue(srcType, out list))
        {
            if (list.Count > 0 && DrawHeader(srcType))
            {
                foreach (string item in list)
                {
                    UnityEngine.Object go = AssetDatabase.LoadAssetAtPath(item, type);
                    EditorGUILayout.ObjectField(srcType, go, type, true);
                }
            }
        }
    }

    /// <summary>
    /// 查找对某个对象的引用
    /// </summary>
    [MenuItem("Assets/资源引用查找工具", false)]
    [MenuItem("Tools/资源引用查找工具/查找引用", false)]
    public static void ResourceRefrenceSearch()
    {
        GetWindow<ResourceRefrenceWindow>(false, "查找引用", true).Show();
    }
    public Dictionary<string, List<string>> FindResourceRefrences()
    {
        Dictionary<string, List<string>> dic = new Dictionary<string, List<string>>();        
        string[] allGuids = AssetDatabase.FindAssets(ResourceRefrenceWindow.SEARCH_TYPE, new string[] { m_SearchDir });

        ShowProgress(0, allGuids.Length, 0);

        ResourceDependencyInfo[] allResourcesInfos = new ResourceDependencyInfo[allGuids.Length];
        UnityEngine.Object[] roots = new UnityEngine.Object[1];
        for (int i = 0; i < allGuids.Length; ++i)
        {
            ResourceDependencyInfo info = new ResourceDependencyInfo();
            string guid = allGuids[i];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            roots[0] = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
            UnityEngine.Object[] dependency = EditorUtility.CollectDependencies(roots);
            string[] dependencyPaths = new string[dependency.Length];
            for (int j = 0; j < dependency.Length; ++j)
            {
                dependencyPaths[j] = AssetDatabase.GetAssetPath(dependency[j]);
            }
            info.path = path;
            info.dependencyPaths = dependencyPaths;
            info.beDependend = false;
            allResourcesInfos[i] = info;
        }

        int threadCounts = Mathf.Min(Environment.ProcessorCount, allGuids.Length);
        FindRefrenceSearchJob[] searchJobsArray = new FindRefrenceSearchJob[threadCounts];
        ManualResetEvent[] events = new ManualResetEvent[threadCounts];
        for (int i = 0; i < threadCounts; ++i)
        {
            searchJobsArray[i] = new FindRefrenceSearchJob();
            events[i] = searchJobsArray[i].doneEvent;
        }
        int timeout = 600000;  // 10 分钟超时
        int index = 0;
        int step = 10;
        int startIndex = 0;
        //Less then step * threadCounts
        for (; index < threadCounts; index++)
        {
            if (index * step >= allGuids.Length)
            {
                break;
            }

            FindRefrenceSearchJob job = searchJobsArray[index];
            job.SetData(startIndex, step, allResourcesInfos, m_Src);
            ThreadPool.QueueUserWorkItem(job.ThreadPoolCallback);

            ShowProgress((float)index * step / (float)(allGuids.Length), allGuids.Length, index * step);

            startIndex += step;
        }

        for (; index < threadCounts; ++index)
        {
            searchJobsArray[index].doneEvent.Set();
        }

        for (int i = index * step; i < allGuids.Length; i += step)
        {
            index = WaitForDoFile(events, timeout);
            FindRefrenceSearchJob job = searchJobsArray[index];
            job.SetData(startIndex, step, allResourcesInfos, m_Src);
            ThreadPool.QueueUserWorkItem(job.ThreadPoolCallback);

            ShowProgress((float)i / (float)(allGuids.Length), allGuids.Length, i);

            startIndex += step;
        }

        WaitHandle.WaitAll(events, timeout);        

        List<string> prefabList = new List<string>();
        List<string> sceneList = new List<string>();
        List<string> matList = new List<string>();
        List<string> otherList = new List<string>();
        for (int i = 0; i < allResourcesInfos.Length; ++i)
        {
            ResourceDependencyInfo info = allResourcesInfos[i];
            if(!info.beDependend || info.path.Equals(m_Src))
            {
                continue;
            }
            string path = info.path.ToLower();
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
            else if(path.EndsWith(".mat"))
            {
                matList.Add(path);
                continue;
            }
            else
            {
                otherList.Add(path);
            }
        }

        dic.Add("Prefab", prefabList);
        dic.Add("Material", matList);
        dic.Add("Scene", sceneList);
        dic.Add("Other", otherList);
        
        EditorUtility.ClearProgressBar();

        return dic;
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