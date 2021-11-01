using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using U3DUtility;

/// <summary>
/// 几个打包使用的菜单功能实现
/// </summary>
namespace U3DEditorUtility
{
    public class BundleBuilder
    {
        [MenuItem(itemName: "Tools/打包工具/清空所有打包名", isValidateFunction: false, priority: 20)]
        private static void CleanResourcesAssetBundleName()
        {
            string appPath = Application.dataPath + "/";
            string projPath = appPath.Substring(0, appPath.Length - 7);
            string fullPath = projPath + "/Assets/Resources";

            DirectoryInfo dir = new DirectoryInfo(fullPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; ++i)
            {
                var fileInfo = files[i];
                string path = fileInfo.FullName.Replace('\\', '/').Substring(projPath.Length);
                EditorUtility.DisplayProgressBar("清理打包资源名称", "正在处理" + fileInfo.Name, 1f * i / files.Length);
                var importer = AssetImporter.GetAtPath(path);
                if (importer)
                {
                    importer.assetBundleName = null;
                }
            }

            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            Debug.Log("=========clean Lua bundle name finished.." + files.Length + " processed");
        }

        [MenuItem(itemName: "Tools/打包工具/生成资源打包名", isValidateFunction: false, priority: 20)]
        private static void SetResourcesAssetBundleName()
        {
            string appPath = Application.dataPath + "/";
            string projPath = appPath.Substring(0, appPath.Length - 7);
            
            string[] searchExtensions = new[] {".prefab", ".mat", ".txt", ".png", ".jpg", ".shader", ".fbx", ".controller", ".anim", ".tga"};
            Regex[] excluseRules = new Regex[] 
            {
                //new Regex (@"^.*/Lua/.*$"), //忽略掉lua脚本，这些脚本会单独打包
            };

            string fullPath = projPath + "/Assets/Resources";

            SetDirAssetBundleName(fullPath, searchExtensions, excluseRules);

            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            Debug.Log("=========Set resource bundle name finished....");
        }

        [MenuItem(itemName: "Tools/打包工具/生成打包文件Android", isValidateFunction: false, priority: 20)]
        private static void BuildAllAssetBundlesAndroid()
        {
            UnityEngine.Debug.Log("=========Build AssetBundles Android start..");
            //用lz4格式压缩
            BuildAssetBundleOptions build_options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.IgnoreTypeTreeChanges | BuildAssetBundleOptions.DeterministicAssetBundle;
            string assetBundleOutputDir = Application.dataPath + "/../AssetBundles/Android/";
            if (!Directory.Exists(assetBundleOutputDir))
            {
                Directory.CreateDirectory(assetBundleOutputDir);
            }

            string projPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            BuildPipeline.BuildAssetBundles(assetBundleOutputDir.Substring(projPath.Length), build_options, BuildTarget.Android);

            Debug.Log("=========Build AssetBundles Android finished..");

            GenerateIndexFile(assetBundleOutputDir);
        }

        [MenuItem(itemName: "Tools/打包工具/生成打包文件Windows64", isValidateFunction: false, priority: 21)]
        private static void BuildAllAssetBundlesWindows()
        {
            UnityEngine.Debug.Log("=========Build AssetBundles Window 64 start..");
            //用lz4格式压缩
            BuildAssetBundleOptions build_options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.IgnoreTypeTreeChanges | BuildAssetBundleOptions.DeterministicAssetBundle;
            string assetBundleOutputDir = Application.dataPath + "/../AssetBundles/Windows/";
            if (!Directory.Exists(assetBundleOutputDir))
            {
                Directory.CreateDirectory(assetBundleOutputDir);
            }

            string projPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            BuildPipeline.BuildAssetBundles(assetBundleOutputDir.Substring(projPath.Length), build_options, BuildTarget.StandaloneWindows64);

            Debug.Log("=========Build AssetBundles Windows 64 finished..");

            GenerateIndexFile(assetBundleOutputDir);
        }

        [MenuItem(itemName: "Tools/打包工具/生成Windows64 Player", isValidateFunction: false, priority: 25)]
        private static void BuildWindowsPlayer()
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = new[] { "Assets/Scenes/main.unity" }; //根据情况修改场景路径名
            buildPlayerOptions.locationPathName = "Win64Player";
            buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
            buildPlayerOptions.options = BuildOptions.None;

            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }

        [MenuItem(itemName: "Tools/打包工具/生成 Android Player", isValidateFunction: false, priority: 26)]
        private static void BuildAndroidPlayer()
        {
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = new[] { "Assets/Scenes/main.unity" }; //根据情况修改场景路径名
            buildPlayerOptions.locationPathName = "AndroidPlayer";
            buildPlayerOptions.target = BuildTarget.Android;
            buildPlayerOptions.options = BuildOptions.None;

            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }

        /// <summary>
        /// 遍历目录下的资源文件，生成索引文件
        /// </summary>
        /// <param name="resDir">要遍历的目录</param>
        private static void GenerateIndexFile(string resDir)
        {
            string platName = resDir;

            if (platName[platName.Length - 1] == '/')
                platName = platName.Substring(0, platName.Length - 1);

            platName = platName.Substring(platName.LastIndexOf('/') + 1);
            DirectoryInfo dirInfo = new DirectoryInfo(resDir);
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            List<BundleItem> items = new List<BundleItem>();
            foreach (var file in files)
            {
                if (file.Extension != ResUtils.BundleExtension && file.Name != platName)
                    continue; //只处理资源关系文件和特定后缀的文件

                BundleItem item = new BundleItem();
                item.m_HashCode = ResUtils.GetFileHash(file.FullName);
                item.m_FileSize = ResUtils.GetFileSize(file.FullName);
                item.m_Name = file.FullName.Substring(resDir.Length);
                items.Add(item);
            }

            IdxFile idx = new IdxFile();
            string idxContent = IdxFile.SaveString(items, resDir);
            string filePath = resDir + ResUtils.BundleIndexFileName;
            File.WriteAllText(filePath, idxContent);

            Debug.Log("=========Generated index file to .." + filePath);
        }

        /// <summary>
        /// 设置某个目录及子目录下资源打包名称
        /// </summary>
        /// <param name="fullPath">搜索资源的目录路径</param>
        /// <param name="searchExtensions">要打包的资源扩展名</param>
        /// <param name="excluseRules">要排除掉的资源，用正则表达式</param>
        private static void SetDirAssetBundleName(string fullPath, string[] searchExtensions, Regex[] excluseRules)
        {
            if (!Directory.Exists(fullPath))
            {
                return;
            }

            string appPath = Application.dataPath + "/";
            string projPath = appPath.Substring(0, appPath.Length - 7);
            DirectoryInfo dir = new DirectoryInfo(fullPath);
            var files = dir.GetFiles("*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; ++i)
            {
                var fileInfo = files[i];

                string ext = fileInfo.Extension.ToLower();
                bool isFound = false;
                foreach (var v in searchExtensions)
                {
                    if (ext == v)
                    {
                        isFound = true;
                        break;
                    }
                }

                if (!isFound)
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar("设置打包资源名称", "正在处理" + fileInfo.Name, 1f * i / files.Length);
                string fullName = fileInfo.FullName.Replace('\\', '/');
                bool isExcluse = false;
                foreach (Regex excluseRule in excluseRules)
                {
                    if (excluseRule.Match(fullName).Success)
                    {
                        isExcluse = true;
                        break;
                    }
                }

                if (isExcluse)
                {
                    continue;
                }

                string path = fileInfo.FullName.Replace('\\', '/').Substring(projPath.Length);
                var importer = AssetImporter.GetAtPath(path);
                if (importer)
                {
                    string name = path.Substring(fullPath.Substring(projPath.Length).Length);
                    string targetName = "";
                    targetName = name.ToLower() + ResUtils.BundleExtension;
                    if (importer.assetBundleName != targetName)
                    {
                        importer.assetBundleName = targetName;
                    }
                }
            }
        }
    }
}
