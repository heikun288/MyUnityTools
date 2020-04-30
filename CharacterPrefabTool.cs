using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;

public class CharacterPrefabTool : Editor
{
    private readonly static string[] EXPORT_ROLE_BONES_NAME =
    {
        "B_Root", "Chest", "Cloak", "Hand", "LeftFoot", "LeftHand", "Mount", "Origin", "RightFoot", "RightHand", "Weapon", "Wing", "Head"
    };

    private readonly static string[] EXPORT_MONSTER_BONES_NAME =
    {
        "Chest", "Origin", "Weapon", "Head"
    };

    private readonly static string[] EXPORT_BOSS_BONES_NAME =
    {
        "Chest", "Origin", "Weapon", "Head", "Dummy002"
    };

    private readonly static string[] EXPORT_NPC_BONES_NAME =
    {
        "Chest", "Origin", "Head"
    };

    private readonly static string[] EXPORT_GOD_BONES_NAME =
    {
        "Chest", "Origin", "Weapon", "Head"
    };

    private readonly static string[] EXPORT_MOUNT_BONES_NAME =
    {
        "Origin"
    };

    private readonly static string[] ROLE_BASE_ANIM_STATES_NAME = new string[]
    {
        "idle",
        "guard",
        "run",
        "guard_run",
        "off_battle_run",
        "off_battle_idle",
        "hit",
        "die",
        "sit",
        "auto_jump_1",
        "auto_jump_2",
        "auto_jump_3",
        "ride_idle",
        "ride_idle_fly",
        "ride_idle_sit",
        "ride_run",
        "ride_run_fly",
        "ride_run_sit",
        "collect_pre",
        "collect_idle",
        "collect_cast",
        "chat"
    };

    private readonly static string[] MONSTER_BASE_ANIM_STATES_NAME = new string[]
    {
        "guard", "guard_run", "hit", "die", "diefly"
    };

    private readonly static string[] BOSS_BASE_ANIM_STATES_NAME = new string[]
    {
        "guard", "guard_run", "die"
    };

    public readonly static string PATH_ROLE = "Assets/RawResources/Character/Prefab/Role";
    public readonly static string PATH_MONSTER = "Assets/RawResources/Character/Prefab/Monster";
    public readonly static string PATH_BOSS = "Assets/RawResources/Character/Prefab/Boss";
    public readonly static string PATH_NPC = "Assets/RawResources/Character/Prefab/Npc";
    public readonly static string PATH_MOUNT = "Assets/RawResources/Character/Prefab/Mount";
    public readonly static string PATH_PET = "Assets/RawResources/Character/Prefab/Pet";
    public readonly static string PATH_LAW = "Assets/RawResources/Character/Prefab/Law";
    public readonly static string PATH_GUARDIAN = "Assets/RawResources/Character/Prefab/Guardian";
    public readonly static string PATH_WEAPON = "Assets/RawResources/Character/Prefab/Weapon";
    public readonly static string PATH_WING = "Assets/RawResources/Character/Prefab/Wing";
    public readonly static string PATH_GOD = "Assets/RawResources/Character/Prefab/God";
    public readonly static string PATH_SWORD = "Assets/RawResources/Character/Prefab/Sword";

    private enum EModelType
    {
        EModelTypeNone      =   0,
        EModelTypeRole      =   1,  //角色
        EModelTypeMonster   =   2,  //怪物
        EModelTypeBoss      =   3,  //Boss
        EModelTypeNPC       =   4,  //Npc
        EModelTypeGod       =   5,  //天神
        EModelTypePet       =   6,  //宠物
        EModelTypeMount       =   7,  //坐骑
        EModelTypeSword       =   8,  //剑
    }
    
    public static void GenerateJsonInfo()
    {
        GenerateRoleAnimationJsonInfo();
        GenerateRoleRadiusJsonInfo(); 
    }

    private static string GetGeneratePrefabNameFromAssetPath(string assetPath)
    {
        string[] splitNames = assetPath.Split('/');
        string generateName = splitNames[splitNames.Length - 1];
        generateName = generateName.Replace("_skin", "");
        splitNames = generateName.Split('.');
        return splitNames[0];
    }

    private static string GetGenerateAnimatorControllerNameFromAssetPath(string assetPath)
    {
        string[] splitNames = assetPath.Split('/');
        return splitNames[splitNames.Length - 2];
    }

    private static void FixExposeRoleTransforms(string assetPath)
    {
        if(assetPath.Contains("ThirdParty"))
        {
            return;
        }

        // export transforms
        ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        modelImporter.optimizeGameObjects = true;
        modelImporter.extraExposedTransformPaths = EXPORT_ROLE_BONES_NAME;
        modelImporter.SaveAndReimport();
    }

    private static void FixExposeTransforms(string assetPath, string[] exportBones)
    {
        // export transforms
        ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        modelImporter.optimizeGameObjects = true;
        modelImporter.extraExposedTransformPaths = exportBones;
        modelImporter.SaveAndReimport();
    }

    private static void FixAnimationDefaultLoop(string clipName, string assetPath)
    {
        if(clipName == "idle" || clipName == "guard" || clipName == "run" || clipName == "guard_run")
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;

            if(!clipAnimations[0].loopTime)
            {
                clipAnimations[0].loopTime = true;
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
            }
        }
    }

    private static void FixRigAvatar(string fbxPath, Animator animator)
    {
        if (!fbxPath.Contains("_skin"))
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if(importer.sourceAvatar == null)
            {
                importer.sourceAvatar = animator.avatar;
                importer.SaveAndReimport();
            }
        }
    }

    private static AnimatorController FixBaseAnimatorClip(string assetDirPath, string generateName, Dictionary<string,AnimationClip> animClipDic)
    {
        // animator override controller
        string animatorAssetPath = assetDirPath + "/" + generateName + ".controller";

        if(File.Exists(animatorAssetPath))
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorAssetPath);
        }

        AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(animatorAssetPath);
        var stateMachine = animatorController.layers[0].stateMachine;

        float base_x = 250;
        float base_y = 0;
        float skill_x = 500;
        float skill_y = 0;

        foreach(KeyValuePair<string, AnimationClip> kvp in animClipDic)
        {
            if(kvp.Key.StartsWith("skill_"))
            {
                AnimatorState state = stateMachine.AddState(kvp.Key, new Vector3(skill_x, skill_y, 0));
                state.motion = kvp.Value;
                skill_y += 50;
            }
            else
            {
                AnimatorState state = stateMachine.AddState(kvp.Key, new Vector3(base_x, base_y, 0));
                state.motion = kvp.Value;
                base_y += 50;
            }
        }

        return animatorController;
    }
    
    private static GameObject CreateDefault(GameObject fbx, AnimatorController animatorController, string generateName, string materialDirPath)
    {
        Animator animator = fbx.GetComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;
        SkinnedMeshRenderer[] renderers = fbx.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.ToLower().Contains(generateName))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialDirPath + "/" + generateName + ".mat");
                if (mat != null)
                {
                    renderers[i].sharedMaterial = mat;
                }
                break;
            }
        }

        GameObject newGo = Instantiate(fbx);
        CapsuleCollider collider = newGo.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 1, 0);
        collider.radius = 0.5f;
        collider.height = 2;

        return newGo;
    }

    private static GameObject CreateFromOldMaterialAndCollider(GameObject fbx, AnimatorController animatorController, string generateName, string materialDirPath, string finalPrefabPath)
    {
        GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(finalPrefabPath);

        GameObject newGo = Instantiate(fbx);
        CapsuleCollider controller = newGo.AddComponent<CapsuleCollider>();
        controller.center = new Vector3(0, 1, 0);
        controller.radius = 0.5f;
        controller.height = 2;
        CapsuleCollider oldController = oldPrefab.GetComponent<CapsuleCollider>();
        if(oldController)
        {
            controller.center = oldController.center;
            controller.radius = oldController.radius;
            controller.height = oldController.height;
        }

        Animator animator = newGo.GetComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;
        SkinnedMeshRenderer[] renderers = newGo.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer[] oldRenderers = oldPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            for(int j = 0; j < oldRenderers.Length; ++j)
            {
                if(renderers[i].name.Equals(oldRenderers[j].name))
                {
                    renderers[i].sharedMaterial = oldRenderers[j].sharedMaterial;
                    break;
                }
            }
        }
        
        return newGo;
    }

    private static void GenerateFinalPrefab(string generateName, string finalPrefabPath, string assetPath, string prefabDirPath, string materialDirPath, AnimatorController animatorController)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        GameObject newGO = null;
        if (File.Exists(finalPrefabPath))
        {
            int option = EditorUtility.DisplayDialogComplex("预设已存在",
            "已经存在同名的预设，选择接下来的操作?",
            "复制原参数(包围盒/材质)",            
            "取消",
            "删除覆盖");

            switch (option)
            {
                // 复制原参数(包围盒/材质) 
                case 0:
                    newGO = CreateFromOldMaterialAndCollider(fbx, animatorController, generateName, materialDirPath, finalPrefabPath);
                    File.Delete(finalPrefabPath);
                    break;
                // 取消
                case 1:
                    return;
                // 删除覆盖
                case 2:
                    newGO = CreateDefault(fbx, animatorController, generateName, materialDirPath);
                    File.Delete(finalPrefabPath);
                    break;
                default:
                    Debug.LogError("Unrecognized option.");
                    return;
            }
        }
        else
        {
            newGO = CreateDefault(fbx, animatorController, generateName, materialDirPath);
        }

        bool success = false;
        PrefabUtility.SaveAsPrefabAsset(newGO, finalPrefabPath, out success);
        if(!success)
        {
            Debug.LogErrorFormat("保存预设失败:[{0}]", finalPrefabPath);
        }

        DestroyImmediate(newGO);
    }

    [MenuItem("Assets/生成模型动画预设(角色,怪物,Boss,Npc,天神,宠物)")]
    public static void GenerateCharacterPrefab()
    {
        Object activeObject = Selection.activeObject;

        if (activeObject == null)
        {
            Debug.LogError("请选择要生成预置的skin对象");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(activeObject);
        if (!assetPath.Contains("Character") || !assetPath.EndsWith(".FBX", System.StringComparison.CurrentCultureIgnoreCase))
        {
            Debug.LogError("选中对象必须是Character目录下的FBX文件");
            return;
        }

        if (!assetPath.Contains("_skin"))
        {
            Debug.LogError("选中对象必须skin文件");
            return;
        }

        string assetDirPath = Path.GetDirectoryName(assetPath);

        string materialDirPath = assetDirPath + "/Materials";
        if (!Directory.Exists(materialDirPath))
        {
            Debug.LogError("没有Materials目录，缺少材质信息");
            return;
        }

        string prefabDirPath = assetDirPath + "/Prefab";
        if (!Directory.Exists(assetDirPath + "/Prefab"))
        {
            Directory.CreateDirectory(prefabDirPath);
        }

        EModelType modelType = EModelType.EModelTypeNone;
        string[] baseAnimStateName = null;
        string[] exportBones = null;
        if (assetPath.Contains("Role") || assetPath.Contains("ThirdParty"))
        {
            modelType = EModelType.EModelTypeRole;
            baseAnimStateName = ROLE_BASE_ANIM_STATES_NAME;
        }
        else if (assetPath.Contains("Monster"))
        {
            modelType = EModelType.EModelTypeMonster;
            baseAnimStateName = MONSTER_BASE_ANIM_STATES_NAME;
            exportBones = EXPORT_MONSTER_BONES_NAME;
        }
        else if (assetPath.Contains("Boss"))
        {
            modelType = EModelType.EModelTypeBoss;
            baseAnimStateName = BOSS_BASE_ANIM_STATES_NAME;
            exportBones = EXPORT_BOSS_BONES_NAME;
        }
        else if (assetPath.Contains("Npc"))
        {
            modelType = EModelType.EModelTypeNPC;
            exportBones = EXPORT_NPC_BONES_NAME;
        }
        else if (assetPath.Contains("God"))
        {
            modelType = EModelType.EModelTypeGod;
            exportBones = EXPORT_GOD_BONES_NAME;
        }
        else if (assetPath.Contains("Pet"))
        {
            modelType = EModelType.EModelTypePet;            
        }
        else if (assetPath.Contains("Mount"))
        {
            modelType = EModelType.EModelTypeMount;            
        }
        else if (assetPath.Contains("Sword"))
        {
            modelType = EModelType.EModelTypeSword;            
        }
        else
        {
            Debug.LogErrorFormat("选中对象必须是Role或者ThirdParty或者Monster或者Boss或者Npc目录下文件, 当前路径:[{0}]", assetPath);
            return;
        }

        Dictionary<string, AnimationClip> animClipDic = new Dictionary<string, AnimationClip>();
        if (baseAnimStateName != null)
        {
            foreach (string stateName in baseAnimStateName)
            {
                string clipAssetPath = assetDirPath + "/" + stateName + ".FBX";

                if (!File.Exists(clipAssetPath))
                {
                    Debug.LogError(string.Format("基础动作：{0}缺失", clipAssetPath));
                    return;
                }

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAssetPath);

                if (stateName != clip.name)
                {
                    Debug.LogError(string.Format("FBX name: {0} 必须和 clip name: {1}相同", stateName, clip.name));
                    return;
                }

                animClipDic.Add(clip.name, clip);
            }
        }

        var fbxGuids = AssetDatabase.FindAssets("t:Model", new string[] { assetDirPath });

        Animator animator = AssetDatabase.LoadAssetAtPath<Animator>(assetPath);

        foreach (var guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);

            string[] splitPaths = fbxPath.Split('/');
            string clipName = splitPaths[splitPaths.Length - 1];
            splitPaths = clipName.Split('.');
            clipName = splitPaths[0];

            if (clipName.StartsWith("skill_"))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);

                if (clipName != clip.name)
                {
                    Debug.LogError(string.Format("FBX name: {0} 必须和 clip name: {1}相同", clipName, clip.name));
                    return;
                }

                animClipDic.Add(clip.name, clip);
            }

            if(fbxPath.ToLower().Contains("login") || fbxPath.ToLower().Contains("shouchong"))
            {
                continue;
            }
            FixAnimationDefaultLoop(clipName, fbxPath);

            FixRigAvatar(fbxPath, animator);
        }

        string generateAnimatorControllerName = GetGenerateAnimatorControllerNameFromAssetPath(assetPath);

        if (exportBones != null)
        {
            FixExposeTransforms(assetPath, exportBones);
        }

        AnimatorController animatorController = FixBaseAnimatorClip(assetDirPath, generateAnimatorControllerName, animClipDic);

        string generatePrefabName = GetGeneratePrefabNameFromAssetPath(assetPath);
        string finalPrefabPath = prefabDirPath + "/" + generatePrefabName + ".prefab";

        GenerateFinalPrefab(generatePrefabName, finalPrefabPath, assetPath, prefabDirPath, materialDirPath, animatorController);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    private static string GetModelIDFromPrefabPath(string prefabPath)
    {
        string[] splitNames = prefabPath.Split('/');
        string prefabName = splitNames[splitNames.Length - 1];

        splitNames = prefabName.Split('.');
        prefabName = splitNames[0];

        splitNames = prefabName.Split('_');
        return splitNames[1];
    }

    private static string GetMonsterCommonAttackAnimationName(string fbxPath, AnimationClip clip)
    {
        string modelName = GetMonsterModelName(fbxPath);
        return modelName + "." + clip.name;
    }

    private static string GetMonsterModelName(string fbxPath)
    {
        string[] splitNames = fbxPath.Split('/');
        string dirName = splitNames[splitNames.Length - 2];

        splitNames = dirName.Split('_');
        string modelName = splitNames[1];

        return modelName;
    }

    [MenuItem("Tools/生成角色动作信息json文件")]
    public static void GenerateRoleAnimationJsonInfo()
    {
        string[] searchDirs = new string[] { PATH_ROLE, PATH_MONSTER, PATH_BOSS, PATH_PET, PATH_GOD };
        var fbxGuids = AssetDatabase.FindAssets("t:Model", searchDirs);
        Dictionary<string, List<AnimationEvent>> clipInfoDic = new Dictionary<string, List<AnimationEvent>>();

        FileInfo jsonFile = new FileInfo("../../Public/runnable/config/data/t_animation_info.json");
        StreamWriter sw = jsonFile.CreateText();

        sw.Write("{\n");

        string lastAnimatorControllerDir = "";
        AnimatorController animatorController = null;
        bool isFirstLine = true;
        foreach (var guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            
            if (clip != null && (clip.name.StartsWith("skill_") || clip.name == "god_come" || clip.name.StartsWith("attack_god_") || (clip.name == "hit" && fbxPath.Contains("Monster"))))
            {
                string fbxDir = fbxPath.Substring(0, fbxPath.LastIndexOf('/'));
                if(fbxDir != lastAnimatorControllerDir)
                {
                    string filter = string.Format("t:{0}", typeof(AnimatorController).Name);
                    var animatorControllerGUIDs = AssetDatabase.FindAssets(filter, new string[] { fbxDir });
                    if (animatorControllerGUIDs.Length <= 0)
                    {
                        Debug.LogErrorFormat("AnimatorController not found:[{0}]", fbxDir);
                        continue;
                    }
                    string animatorControllerPath = AssetDatabase.GUIDToAssetPath(animatorControllerGUIDs[0]);
                    animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorControllerPath);
                }
                lastAnimatorControllerDir = fbxDir;
                ChildAnimatorState[] states = animatorController.layers[0].stateMachine.states;
                float speed = 1f;
                for (int i = 0; i < states.Length; ++i)
                {
                    if (states[i].state.name == clip.name)
                    {
                        speed = states[i].state.speed;
                    }
                }
                if (!isFirstLine)
                {
                    sw.Write("\t},");
                    sw.Write("\n");
                }

                isFirstLine = false;

                if (clip.name == "skill_common_attack" || clip.name == "hit" || clip.name == "god_come" || fbxPath.Contains("Monster") || fbxPath.Contains("Boss"))
                {
                    sw.Write(string.Format("\t\"{0}\":\n", GetMonsterCommonAttackAnimationName(fbxPath, clip)));
                }
                else
                {
                    sw.Write(string.Format("\t\"{0}\":\n", clip.name));
                }
                sw.Write("\t{\n");
                AnimationEvent[] events = clip.events;
                List<AnimationEvent> sortedEvents = new List<AnimationEvent>(events);
                for(int i = 0; i < sortedEvents.Count; i++)
                {
                    AnimationEvent e = sortedEvents[i];
                    if (e.functionName == "key")
                    {
                        sw.Write("\t\t\"{0}\":{1}", e.stringParameter, Mathf.FloorToInt(e.time * 1000 / speed));
                        if(i != (sortedEvents.Count-1))
                        {
                            sw.Write(",\n");
                        }
                        else
                        {
                            sw.Write("\n");
                        }
                    }
                }
            }
        }

        sw.Write("\t}");
        sw.Write("\n");

        sw.Write("}");

        sw.Close();
        sw.Dispose();

        Debug.Log("json文件生成成功");
    }

    [MenuItem("Tools/生成怪物出生动作信息配置")]
    public static void GenerateMonsterAppearAnimationConfig()
    {
        var fbxGuids = AssetDatabase.FindAssets("t:Model", new string[] { PATH_BOSS, PATH_MONSTER });

        Dictionary<string, List<AnimationEvent>> clipInfoDic = new Dictionary<string, List<AnimationEvent>>();

        FileInfo jsonFile = new FileInfo("../../Public/runnable/config/data/t_monster_appear_info.json");
        FileInfo luaFile = new FileInfo("Assets/TextAssets/LuaScript/MainGame/Resource/CN/MonsterAppearTime.lua");
        StreamWriter swJson = jsonFile.CreateText();
        StreamWriter swLua = luaFile.CreateText();

        swJson.Write("{\n");
        swLua.Write("module( \"MainGame.Resource.CN.Config\", package.seeall );\n\nlocal t = {\n");

        bool isFirstLine = true;

        foreach (var guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);

            if (clip != null && (clip.name.Equals("appear")))
            {
                if (!isFirstLine)
                {
                    swJson.Write(",\n");
                    swLua.Write(",\n");
                }

                isFirstLine = false;

                int clipLength = Mathf.FloorToInt(clip.length * 1000);
                swJson.Write(string.Format("\t\"{0}\": {1}", GetMonsterModelName(fbxPath), clipLength));
                swLua.Write(string.Format("\t[{0}] = {1}", GetMonsterModelName(fbxPath), clipLength));
            }
        }

        swJson.Write("\n}");
        swJson.Close();
        swJson.Dispose();

        swLua.Write("\n}\n\nreturn t");

        swLua.Close();
        swLua.Dispose();

        Debug.Log("配置文件生成成功");
    }

    [MenuItem("Tools/生成角色半径信息json文件")]
    public static void GenerateRoleRadiusJsonInfo()
    {
        string pathRole = "Assets/RawResources/Character/Prefab/Role";
        string pathMonster = "Assets/RawResources/Character/Prefab/Monster";
        string pathBoss = "Assets/RawResources/Character/Prefab/Boss";
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { pathRole, pathMonster, pathBoss });

        Dictionary<string, List<AnimationEvent>> clipInfoDic = new Dictionary<string, List<AnimationEvent>>();

        FileInfo jsonFile = new FileInfo("../../Public/runnable/config/data/t_radius_info.json");
        StreamWriter sw = jsonFile.CreateText();

        sw.Write("{\n");

        bool isFirstLine = true;

        foreach (var guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);

            if (prefabPath.Contains("Prefab/") && !prefabPath.Contains("login"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                CapsuleCollider capsuleCollider = prefab.GetComponent<CapsuleCollider>();

                if (capsuleCollider == null)
                {
                    Debug.LogErrorFormat("{0}: CapsuleCollider缺失", prefabPath);
                    continue;
                }

                if (!isFirstLine)
                {
                    sw.Write(",\n");
                }

                isFirstLine = false;

                sw.Write(string.Format("\t\"{0}\":{1}", GetModelIDFromPrefabPath(prefabPath), capsuleCollider.radius));
            }
        }

        sw.Write("\n}");

        sw.Close();
        sw.Dispose();

        Debug.Log("json文件生成成功");
    }

    private static AnimationEvent[] GetSortedEvents(List<AnimationEvent> eventList)
    {
        eventList.Sort(delegate (AnimationEvent e1, AnimationEvent e2)
        {
            int ret = e1.time.CompareTo(e2.time);
            if (ret == 0)
            {
                ret = e1.functionName.CompareTo(e2.functionName);
            }
            return ret;
        });
        return eventList.ToArray();
    }

    private static void CheckIsFBXInRuntimeAnimatorController(AnimationClip[] rtClips, AnimationClip clip, string fbxPath)
    {
        for (int i = 0; i < rtClips.Length; ++i)
        {
            AnimationClip rtClip = rtClips[i];        
            if(rtClip.name == clip.name && rtClip == clip)
            {
                return;
            }
        }

        if(clip.name == "zhuozi")
        {
            return;
        }
        Debug.LogErrorFormat("{0}:动作 [{1}] 未被引用 ", fbxPath, clip.name);
    }

    private static void CheckClipEvent(AnimationClip clip, string strEvent, string path)
    {
        AnimationEvent[] events = clip.events;
        for (int i = 0; i < events.Length; i++)
        {
            AnimationEvent e = events[i];
            if (e.functionName == "key" && e.stringParameter == strEvent)
            {
                return;
            }
        }

        Debug.LogErrorFormat("{0} [{1}]:缺少打点:[{2}] ", path, clip.name, strEvent);        
    }

    private static void CheckClip(AnimationClip clip, string prefabPath)
    {
        string clipName = clip.name;
        if(clipName == "die" || clipName == "diefly" || clipName == "hit")
        {
            CheckClipEvent(clip, "start", prefabPath);
            CheckClipEvent(clip, "hit", prefabPath);
            CheckClipEvent(clip, "end", prefabPath);
        }
        else if(clip.name.Contains("skill") && !clip.name.Contains("pre") && !clip.name.Contains("idle") && !clip.name.Contains("cast"))
        {
            CheckClipEvent(clip, "start", prefabPath);
            CheckClipEvent(clip, "hj", prefabPath);
            CheckClipEvent(clip, "hit", prefabPath);
            CheckClipEvent(clip, "bs", prefabPath);
            CheckClipEvent(clip, "end", prefabPath);
        }
        else if(clip.name.Equals("collect_pre") || clip.name.Equals("collect_cast"))
        {
            CheckClipEvent(clip, "start", prefabPath);
            CheckClipEvent(clip, "end", prefabPath);
        }

        if (clipName == "idle" || clipName == "idle_ui" || clipName == "guard" || clipName == "run" || clipName == "guard_run")
        {
            if (!clip.isLooping)
            {
                Debug.LogErrorFormat("{0} [{1}]:没有循环！！", prefabPath, clipName);
            }
        }
    }


    private static void CheckPetStateMachineTransition(AnimatorStateMachine stateMachine, string prefabPath)
    {
        if(stateMachine.defaultState.name != "idle")
        {
            Debug.LogErrorFormat("{0}: 动画状态机默认状态错误:{1}", prefabPath, stateMachine.defaultState.name);
        }

        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; ++i)
        {
            AnimatorState state = states[i].state;
            if(state.name == "idle" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "fallow"))
            {
                // idle -> fallow
                Debug.LogErrorFormat("{0}: 动画状态机缺少 idle -> fallow", prefabPath);
                continue;
            }

            if(state.name == "fallow" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "idle"))
            {
                // fallow -> idle
                Debug.LogErrorFormat("{0}: 动画状态机缺少 fallow -> idle", prefabPath);
                continue;
            }

            if(state.name == "idle_ui")
            {
                for(int j = 0; j < states.Length; ++j)
                {
                    if(states[j].state.name == "fallow_ui" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "fallow_ui"))
                    {
                        // idel_ui -> fallow_ui
                        Debug.LogErrorFormat("{0}: 动画状态机缺少 idel_ui -> fallow_ui", prefabPath);
                        continue;
                    }
                }
            }

            if(state.name == "fallow_ui" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "idle_ui"))
            {
                // fallow_ui -> idle_ui
                Debug.LogErrorFormat("{0}: 动画状态机缺少 fallow_ui -> idle_ui", prefabPath);
                continue;
            }

            if(state.name == "spawn" && (state.transitions.Length <= 0 || (state.transitions[0].destinationState.name != "idle" && state.transitions[0].destinationState.name != "idle_ui")))
            {
                // spwan -> idle or idle_ui
                Debug.LogErrorFormat("{0}: 动画状态机缺少 spawn -> idle or idle_ui", prefabPath);
                continue;
            }
        }
    }

    private static void CheckLawStateMachineTransition(AnimatorStateMachine stateMachine, string prefabPath)
    {
        if(stateMachine.defaultState.name != "guard")
        {
            Debug.LogErrorFormat("{0}: 动画状态机默认状态错误:{1}", prefabPath, stateMachine.defaultState.name);
        }
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; ++i)
        {
            AnimatorState state = states[i].state;
            if (state.name != "spawn")
            {
                continue;
            }
            if (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "guard")
            {
                Debug.LogErrorFormat("{0}: 动画状态机缺少 spawn -> guard", prefabPath);
            }
        }
    }

    private static void CheckNpcStateMachineTransition(AnimatorStateMachine stateMachine, string prefabPath)
    {
        if (stateMachine.defaultState.name != "guard")
        {
            Debug.LogErrorFormat("{0}: 动画状态机默认状态错误:{1}", prefabPath, stateMachine.defaultState.name);
        }
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; ++i)
        {
            AnimatorState state = states[i].state;
            if (state.name == "guard" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "fallow"))
            {
                // idle -> fallow
                Debug.LogErrorFormat("{0}: 动画状态机缺少 guard -> fallow", prefabPath);
                continue;
            }
            if (state.name == "fallow" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "guard"))
            {
                // idle -> fallow
                Debug.LogErrorFormat("{0}: 动画状态机缺少 fallow -> guard", prefabPath);
                continue;
            }
            if (state.name == "chat" && (state.transitions.Length <= 0 || state.transitions[0].destinationState.name != "guard"))
            {
                // idle -> fallow
                Debug.LogErrorFormat("{0}: 动画状态机缺少 chat -> guard", prefabPath);
                continue;
            }
        }
    }
    private static void CheckStateMachineTransition(RuntimeAnimatorController animator, string prefabPath)
    {
        AnimatorController controller = animator as AnimatorController;
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        if (stateMachine.states.Length > 2 && stateMachine.defaultState.name != "guard" && stateMachine.defaultState.name != "idle")
        {
            Debug.LogErrorFormat("{0}: 动画状态机默认状态错误:{1}", prefabPath, stateMachine.defaultState.name);
        }

        if (prefabPath.Contains("/Pet/") || prefabPath.Contains("/Mount/") || prefabPath.Contains("/Sword/"))
        {
            CheckPetStateMachineTransition(stateMachine, prefabPath);
        }
        else if(prefabPath.Contains("/Law/") || prefabPath.Contains("/Weapon/") || prefabPath.Contains("/Wing/"))
        {
            CheckLawStateMachineTransition(stateMachine, prefabPath);
        }
        else if(prefabPath.Contains("/Npc/"))
        {
            CheckNpcStateMachineTransition(stateMachine, prefabPath);
        }
    }

    private static void CheckCharacterAnimatorController(RuntimeAnimatorController rtCtrl, string prefabPath)
    {
        string[] baseStateName = null;
        if(prefabPath.Contains("Role"))
        {
            baseStateName = ROLE_BASE_ANIM_STATES_NAME;
        }
        if(baseStateName == null)
        {
            return;
        }
        AnimationClip[] rtClips = rtCtrl.animationClips;        
        int clipLength = rtClips.Length;
        int stateLength = baseStateName.Length;
        bool[] bExist = new bool[stateLength];
        for (int i = 0; i < stateLength; ++i)
        {
            string stateName = baseStateName[i];
            for(int j = 0; j < clipLength; ++j)
            {
                AnimationClip clip = rtClips[j];
                if(clip.name == stateName)
                {
                    bExist[i] = true;
                    break;
                }
            }
        }

        for(int i = 0; i < stateLength; ++i)
        {
            if(!bExist[i])
            {
                Debug.LogErrorFormat("{0}: 缺少基础动作[{1}]", prefabPath, baseStateName[i]);
            }
        }
    }
    
    private static void CheckRuntimeAnimatorController(RuntimeAnimatorController rtCtrl, string prefabPath)
    {
        AnimationClip[] rtClips = rtCtrl.animationClips;
        if (rtClips.Length <= 0 )
        {
            Debug.LogErrorFormat("{0}: 没有指向动作文件", prefabPath);
            return;
        }

        CheckCharacterAnimatorController(rtCtrl, prefabPath);

        string modelPath = prefabPath.Remove(prefabPath.LastIndexOf("/Prefab"));
        var fbxGuids = AssetDatabase.FindAssets("t:Model", new string[] { modelPath });
        foreach (var guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            if(clip == null)
            {
                //skin
                continue;
            }

            if(fbxPath.Contains("story") || fbxPath.Contains("login") || fbxPath.Contains("shouchong"))
            {
                continue;
            }

            CheckIsFBXInRuntimeAnimatorController(rtClips, clip, fbxPath);

            CheckClip(clip, modelPath);
        }

        CheckStateMachineTransition(rtCtrl, prefabPath);
    }

    private static bool CheckPrefabPathName(string prefabPath)
    {
        int count = 0;
        int index = -1;
        while((index = prefabPath.LastIndexOf("Prefab")) >= 0)
        {
            prefabPath = prefabPath.Remove(index);
            count++;
        }

        return count == 2;
    }

    private static void CheckModelPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { PATH_ROLE, PATH_MONSTER, PATH_BOSS, PATH_NPC, PATH_MOUNT, PATH_PET, PATH_LAW, PATH_GUARDIAN, PATH_GOD });
        int length = prefabGuids.Length;

        for(int i = 0; i < length; ++i)
        {
            string guid = prefabGuids[i];
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            //采集物以monster_403打头，墓碑monster_402077，boss_401106，过滤            
            if (prefabPath.Contains("Prefab/") 
                && !prefabPath.Contains("login") 
                && !prefabPath.Contains("monster_403") 
                && !prefabPath.Contains("monster_402077") 
                && !prefabPath.Contains("monster_402101") 
                && !prefabPath.Contains("boss_401106") 
                && !prefabPath.Contains("boss_401094") 
                && !prefabPath.Contains("Shouchong"))
            {
                CharacterController characterController = prefab.GetComponent<CharacterController>();
                if(characterController != null)
                {
                    Debug.LogErrorFormat("{0}: 挂了CharacterController", prefabPath);
                }
                CapsuleCollider capsuleCollider = prefab.GetComponent<CapsuleCollider>();
                if (!prefabPath.Contains("/Law/") && !prefabPath.Contains("/Guardian/") && capsuleCollider == null)
                {
                    Debug.LogErrorFormat("{0}: CapsuleCollider缺失", prefabPath);
                }

                if(capsuleCollider != null && capsuleCollider.isTrigger)
                {
                    Debug.LogErrorFormat("{0}: CapsuleCollider IsTrigger 开启了！", prefabPath);
                }

                Animator animator = prefab.GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogErrorFormat("{0}: Animator缺失", prefabPath);
                    continue;
                }

                if (animator.runtimeAnimatorController == null)
                {
                    Debug.LogErrorFormat("{0}: Animator Controller缺失", prefabPath);
                    continue;
                }

                if(!CheckPrefabPathName(prefabPath))
                {
                    Debug.LogErrorFormat("{0}: 路径名不对", prefabPath);
                    continue;
                }
                CheckRuntimeAnimatorController(animator.runtimeAnimatorController, prefabPath);

                if (animator.avatar == null)
                {
                    Debug.LogErrorFormat("{0}: Animator Avatar缺失", prefabPath);
                }

                Transform originTrans = prefab.transform.Find("Origin");
                if (originTrans == null)
                {
                    Debug.LogErrorFormat("{0}: Origin缺失", prefabPath);
                    continue;
                }

                float absScale = 0.1f;
                if (Mathf.Abs(Mathf.Abs(originTrans.localScale.x) - 1) > absScale
                    || Mathf.Abs(Mathf.Abs(originTrans.localScale.y) - 1) > absScale
                    || Mathf.Abs(Mathf.Abs(originTrans.localScale.z) - 1) > absScale)
                {
                    Debug.LogErrorFormat("{0}: Origin Scale 错误", prefabPath);
                }

                float absAngle = 1f;
                Vector3 eulerAngles = originTrans.eulerAngles;
                if (Mathf.Abs(eulerAngles.x) > absAngle
                    || Mathf.Abs(eulerAngles.y) > absAngle
                    || Mathf.Abs(eulerAngles.z) > absAngle)
                {
                    Debug.LogErrorFormat("{0}: Origin Rotation 错误", prefabPath);
                }
            }
            EditorUtility.DisplayProgressBar("检查预设", string.Format("正在检查预设 ({0}/{1}), please wait...", i, length), (float)i / (float)length);
        }
    }

    private static void CheckOptimizeGameObject()
    {
        string[] fbxGUIDs = AssetDatabase.FindAssets("_skin t:Model", new string[] { PATH_MONSTER, PATH_BOSS, PATH_NPC, PATH_MOUNT, PATH_PET, PATH_LAW, PATH_GUARDIAN });
        int length = fbxGUIDs.Length;
        for(int i = 0; i < length; ++i)
        {
            string guid = fbxGUIDs[i];
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if(modelImporter.animationType == ModelImporterAnimationType.Legacy)
            {
                Debug.LogErrorFormat("Animation Type是Legacy:[{0}]", fbxPath);
                continue;
            }

            if(!fbxPath.Contains("_xfree") && modelImporter.animationType == ModelImporterAnimationType.Generic && !modelImporter.optimizeGameObjects)
            {
                Debug.LogErrorFormat("Optimize GameObject 选项未开启:[{0}]", fbxPath);
            }
            EditorUtility.DisplayProgressBar("检查模型", string.Format("正在检查模型 ({0}/{1}), please wait...", i, length), (float)i / (float)length);
        }
    }

    public static void CheckFileNameWithSpace()
    {
        string path = "Assets/RawResources";
        string filter = "";
        string[] prefabGuids = AssetDatabase.FindAssets(filter, new string[] { path });
        EditorUtility.DisplayProgressBar("检查文件名", string.Format("正在检查文件名 ({0}/{1}), please wait...", 0, prefabGuids.Length), 0);

        foreach (var guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath.Contains(" ") || assetPath.Contains(".dds"))                       //判断异常文件名
            {
                Debug.LogErrorFormat("文件名含有空格或者dds格式:   [{0}]", assetPath);
            }
        }

        EditorUtility.DisplayProgressBar("检查文件名", string.Format("正在检查文件名 ({0}/{1}), please wait...", prefabGuids.Length, prefabGuids.Length), 1);
    }

    [MenuItem("Tools/模型动作信息常规检测")]
    [MenuItem("Assets/模型动作信息常规检测")]
    public static void ModelConventionCheck()
    {
        CheckModelPrefab();
        CheckOptimizeGameObject();
        CheckFileNameWithSpace();
        CheckMissingScripts();
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("Tools/查找AnimationType [Legacy]")]
    [MenuItem("Assets/查找AnimationType [Legacy]")]
    public static void FindAnimationTypeLegacy()
    {
        string path = "Assets/RawResources";
        string filter = string.Format("t:{0}", typeof(AnimationClip).ToString().Replace("UnityEngine.", ""));
        var prefabGuids = AssetDatabase.FindAssets(filter, new string[] { path });

        foreach (var guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if(modelImporter != null && modelImporter.animationType == ModelImporterAnimationType.Legacy)
            {
                Debug.LogErrorFormat("AnimationType为Legacy:{0}: {1}", assetPath, modelImporter.name);
            }
        }
        Debug.LogErrorFormat("查找完成!");
    }

    [MenuItem("Tools/查找空脚本")]
    public static void CheckMissingScripts()
    {
        string path = "Assets/RawResources";
        string filter = string.Format("t:{0}", typeof(GameObject).ToString().Replace("UnityEngine.", ""));
        string[] prefabGuids = AssetDatabase.FindAssets(filter, new string[] { path });

        EditorUtility.DisplayProgressBar("检查文件名", string.Format("正在检查文件名 ({0}/{1}), please wait...", 0, prefabGuids.Length), 0);

        foreach (var guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            Transform[] trans = go.GetComponentsInChildren<Transform>(true);
            for(int i = 0; i < trans.Length; ++i)
            {
                Component[] comps = trans[i].GetComponents<Component>();
                for(int j = 0; j < comps.Length; ++j)
                {
                    if (comps[j] == null)
                    {
                        Debug.LogErrorFormat("有空脚本!!  路径:[{0}] 节点:[{1}]", assetPath, trans[i].name);
                    }
                }

            }
        }

        EditorUtility.DisplayProgressBar("检查文件名", string.Format("正在检查文件名 ({0}/{1}), please wait...", prefabGuids.Length, prefabGuids.Length), 1);
    }

    [MenuItem("Tools/清理材质残留属性")]
    public static void ClearMaterialsNoUsedProperties()
    {
        string path = "Assets/RawResources";

        var matGuids = AssetDatabase.FindAssets("t:Material", new string[] { path });

        int count = 0;
        EditorUtility.DisplayProgressBar("清理材质残留属性", string.Format("正在检查文件名 ({0}/{1}), please wait...", count, matGuids.Length), 0);

        foreach (var guid in matGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            count++;
            EditorUtility.DisplayProgressBar("清理材质残留属性", string.Format("正在检查文件名 ({0}/{1}), please wait...", count, matGuids.Length), count * 1.0f/matGuids.Length);

            CleanMatUnusedProperties(mat);
        }

        AssetDatabase.SaveAssets();

        EditorUtility.ClearProgressBar();
    }

    public static void CleanMatUnusedProperties(Material mat)
    {
        if (null == mat) return;
        SerializedObject matInfo = new SerializedObject(mat);
        SerializedProperty propArr = matInfo.FindProperty("m_SavedProperties");
        SerializedProperty prop = null;
        propArr.Next(true);
        do
        {
            if (!propArr.isArray) continue;

            for (int i = propArr.arraySize - 1; i >= 0; --i)
            {
                prop = propArr.GetArrayElementAtIndex(i);
                if (!mat.HasProperty(prop.displayName))
                {
                    propArr.DeleteArrayElementAtIndex(i);
                }
            }
        } while (propArr.Next(false));
        matInfo.ApplyModifiedProperties();
    }


    public static void CheckLayer(string path)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { path });
        int invalidLayers = LayerMask.GetMask(new string[] { "King", "UI3D", });
        int length = prefabGuids.Length;
        int counter = 0;
        for (int i = 0; i < length; ++i)
        {
            string guid = prefabGuids[i];
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            string nodes = "";
            if (prefabPath.Contains("login") || prefabPath.Contains("_show") || prefabPath.Contains("_shouchong"))
            {
                continue;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Transform[] childs = prefab.GetComponentsInChildren<Transform>();
            for (int j = 0; j < childs.Length; ++j)
            {
                GameObject child = childs[j].gameObject;
                int layerMask = 1 << child.layer;
                if ((layerMask & invalidLayers) > 0)
                {
                    nodes += child.name + ", ";
                    //Debug.LogErrorFormat("非法的Layer:[{0}] 预设:[{1}] 节点:[{2}]", LayerMask.LayerToName(child.layer), prefabPath, child.name);
                }
            }
            if (nodes != "")
            {
                Debug.LogErrorFormat("非法的Layer!! 预设:[{0}] 节点:[{1}]", prefabPath, nodes);
            }

            counter++;
            EditorUtility.DisplayProgressBar("检查Layer", string.Format("检查Layer[{0}] ({1}/{2}), please wait...", path, counter, prefabGuids.Length), (float)counter / (float)prefabGuids.Length);
        }

        EditorUtility.ClearProgressBar();
    }

    [MenuItem("Tools/检查非法Layer")]
    public static void CheckInvalidLayers()
    {
        CheckLayer("Assets/RawResources/Character");
        CheckLayer("Assets/RawResources/Effect");
    }
        
    public static void CheckShader(string path)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { path });
        int invalidLayers = LayerMask.GetMask(new string[] { "King", "UI3D", });
        int length = prefabGuids.Length;
        int counter = 0;
        for (int i = 0; i < length; ++i)
        {
            string guid = prefabGuids[i];
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            for (int j = 0; j < renderers.Length; ++j)
            {
                MeshRenderer renderer = renderers[j];
                Material[] materials = renderer.sharedMaterials;
                for(int k = 0; k < materials.Length; ++k)
                {
                    Material material = materials[k];
                    if(material == null)
                    {
                        Debug.LogErrorFormat("material null:{0}  {1}", prefab, renderer.gameObject.name);
                        continue;
                    }
                    if(material.shader.name.Contains("Fixed"))
                    {
                        Debug.LogErrorFormat("非法的Shader:[{0}] 预设:[{1}] 节点:[{2}]", "FixedLighting", prefabPath, renderer.gameObject.name);
                    }
                }
            }

            SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int j = 0; j < skinnedRenderers.Length; ++j)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[j];
                Material[] materials = renderer.sharedMaterials;
                for (int k = 0; k < materials.Length; ++k)
                {
                    Material material = materials[k];
                    if (material == null)
                    {
                        Debug.LogErrorFormat("material null:{0}  {1}", prefab, renderer.gameObject.name);
                        continue;
                    }
                    if (material.shader.name.Contains("Fixed"))
                    {
                        Debug.LogErrorFormat("非法的Shader:[{0}] 预设:[{1}] 节点:[{2}]", "FixedLighting", prefabPath, renderer.gameObject.name);
                    }
                }
            }
            counter++;
            EditorUtility.DisplayProgressBar("检查Layer", string.Format("检查Layer[{0}] ({1}/{2}), please wait...", path, counter, prefabGuids.Length), (float)counter / (float)prefabGuids.Length);
        }

        EditorUtility.ClearProgressBar();
    }

    [MenuItem("Tools/检查非法Shader")]
    public static void CheckInvalidShader()
    {
        CheckShader("Assets/RawResources/Character");
        CheckShader("Assets/RawResources/Effect");
    }

    [MenuItem("Assets/清除动画事件")]
    public static void CleanClipAnimationKeyEvent()
    {
        List<string> paths = new List<string>();
        List<string> animPathList = new List<string>();
        string[] guids = null;

        UnityEngine.Object[] objs = Selection.GetFiltered(typeof(object), SelectionMode.Assets);
        if (objs.Length > 0)
        {
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i].GetType() == typeof(AnimationClip))
                {
                    string p = AssetDatabase.GetAssetPath(objs[i]);
                    animPathList.Add(p);
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
            if (!assetPath.EndsWith(".anim", System.StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            animPathList.Add(assetPath);
        }

        for(int i = 0; i < animPathList.Count; ++i)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPathList[i]);
            if(clip.events.Length > 0)
            {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                Debug.LogFormat("清除了[{0}]的动画事件", animPathList[i]);
            }

            EditorUtility.DisplayProgressBar("清除动画事件", string.Format("清除动画事件 ({0}/{1})", i, animPathList.Count), (float)i / (float)animPathList.Count);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        Debug.Log("清除完成");
    }

    [MenuItem("Assets/TTTTTTTTTTTT")]
    private static void FindNormal()
    {
        string path = "Assets/RawResources";

        var matGuids = AssetDatabase.FindAssets("t:Material", new string[] { path });

        int count = 0;
        EditorUtility.DisplayProgressBar("清理材质残留属性", string.Format("正在检查文件名 ({0}/{1}), please wait...", count, matGuids.Length), 0);

        foreach (var guid in matGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            count++;
            EditorUtility.DisplayProgressBar("清理材质残留属性", string.Format("正在检查文件名 ({0}/{1}), please wait...", count, matGuids.Length), count * 1.0f / matGuids.Length);

            //CleanMatUnusedProperties(mat);
            if(mat.HasProperty("_NormalMap"))
            {
                Texture normalMap = mat.GetTexture("_NormalMap");
                if(normalMap != null && normalMap.name.Contains("A2"))
                {
                    Debug.LogErrorFormat("{0}", assetPath);
                }
            }
        }

        EditorUtility.ClearProgressBar();
    }
}
