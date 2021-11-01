using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

/// <summary>
/// 同步.lua的修改及删除到工程的打包目录中的.txt文件
/// </summary>
public class CopyLua : AssetPostprocessor
{
    public static void OnPostprocessAllAssets(string[] importedAsset, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string str in importedAsset)
        {
            if (!Directory.Exists(str) && str.Contains("Assets/Lua/"))
            {
                string src = Application.dataPath + "/" + str.Remove(0, 7);
                string dst = Application.dataPath + "/Resources/" + str.Remove(0, 7) + ".txt";
                string dstDir = Application.dataPath + "/Resources/" + str.Remove(0, 7);
                dstDir = dstDir.Remove(dstDir.LastIndexOf('/'));

                Debug.Log("copy lua " + dst);
                try
                {
                    if (!Directory.Exists(dstDir))
                    {
                        Directory.CreateDirectory(dstDir);
                    }

                    File.Copy(src, dst, true);
                }
                catch (Exception ex)
                {
                    Debug.LogError("copy lua error " + ex.ToString());
                }
            }
        }

        foreach (string str in deletedAssets)
        {
            if (str.Contains("Assets/Lua/"))
            {
                string dst = Application.dataPath + "/Resources/" + str.Remove(0, 7) + ".txt";
                Debug.Log("delete lua " + dst);
                try
                {
                    File.Delete(dst);
                }
                catch (Exception ex)
                {
                    Debug.LogError("delete lua error " + ex.ToString());
                }
            }
        }

        AssetDatabase.Refresh();
    }
}

