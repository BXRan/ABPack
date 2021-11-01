using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

/// <summary>
/// 让编辑器支持从模板文件中创建.lua文件
/// </summary>
namespace U3DEditorUtility
{
    internal class CreateLua
    {
        [MenuItem("Assets/Create/Lua Script", false, 80)]
        public static void CreateNewLua()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0,
                ScriptableObject.CreateInstance<CreateScriptAssetAction>(),
                GetSelectedPathOrFallback() + "/NewLua.lua",
                null,
                "Assets/Lua/luaTemplate.lua");
        }

        public static string GetSelectedPathOrFallback()
        {
            string path = "Assets";
            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }
    }

    internal class CreateScriptAssetAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            //创建资源
            UnityEngine.Object obj = CreateAssetFromTemplate(pathName, resourceFile);
            //高亮显示该资源
            ProjectWindowUtil.ShowCreatedAsset(obj);
        }

        internal static UnityEngine.Object CreateAssetFromTemplate(string pathName, string resourceFile)
        {
            //获取要创建的资源的绝对路径
            string fullName = Path.GetFullPath(pathName);
            
            //读取本地模板文件
            StreamReader reader = new StreamReader(resourceFile);
            string content = reader.ReadToEnd();
            reader.Close();

            //替换默认的文件名
            content = content.Replace("#TIME", DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss dddd"));

            //写入新文件
            StreamWriter writer = new StreamWriter(fullName, false, System.Text.Encoding.UTF8);
            writer.Write(content);
            writer.Close();

            //刷新本地资源
            AssetDatabase.ImportAsset(pathName);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath(pathName, typeof(UnityEngine.Object));
        }
    }
}
