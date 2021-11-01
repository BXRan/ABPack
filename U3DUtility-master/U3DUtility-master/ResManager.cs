using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;

namespace U3DUtility
{
    /// <summary>
    /// 资源工具类
    /// </summary>
    public class ResUtils
    {
        public static string BundleExtension = "unity3d"; //打包资源扩展名

        public static string BundleIndexFileName = "list.txt"; //打包资源的索引文件

        public static string BundleRootDirName = "AssetBundles"; //打包文件所在的根目录名

        public static string BundleRootPath = Application.persistentDataPath + "/" + ResUtils.BundleRootDirName + "/";

        /// <summary>
        /// 根据平台不同获取打包依赖关系文件名
        /// </summary>
        /// <param name="plat"></param>
        /// <returns></returns>
        public static string GetBundleManifestName(RuntimePlatform plat)
        {
            if (plat == RuntimePlatform.WindowsEditor || plat == RuntimePlatform.WindowsPlayer)
            {
                return "Windows";
            }
            else if (plat == RuntimePlatform.Android)
            {
                return "Android";
            }
            else if (plat == RuntimePlatform.IPhonePlayer)
            {
                return "IOS";
            }
            else
            {
                return "Windows";
            }
        }

        /// <summary>
        /// 获取文件的大小
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static int GetFileSize(string filePath)
        {
            FileInfo file = new FileInfo(filePath);
            if (file == null)
                return 0;
            return (int)file.Length;
        }

        /// <summary>
        /// 获得文件的md5 hash值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public static string GetFileHash(string filePath)
        {
            try
            {
                FileStream fs = new FileStream(filePath, FileMode.Open);
                int len = (int)fs.Length;
                byte[] data = new byte[len];
                fs.Read(data, 0, len);
                fs.Close();
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] result = md5.ComputeHash(data);
                string fileMD5 = "";
                foreach (byte b in result)
                {
                    fileMD5 += Convert.ToString(b, 16);
                }
                return fileMD5;
            }
            catch (FileNotFoundException e)
            {
                Debug.LogError("can not open file for md5 hash " + e.FileName);
                return "";
            }
        }
    }

    /// <summary>
    /// 资源加载及管理
    /// </summary>
    public class ResManager
    {
        private sealed class AssetBundleInfo
        {
            public readonly AssetBundle mAssetBundle;
            public int mReferencedCount;

            public AssetBundleInfo(AssetBundle assetBundle)
            {
                mAssetBundle = assetBundle;
                mReferencedCount = 1;
            }
        }

        private AssetBundleManifest mAssetBundleManifest;
        //保存已经加载过的包信息，键值为包文件的相对路径名例如：subdir/res1.prefab.unity3d
        private Dictionary<string, AssetBundleInfo> mLoadedAssetBundles = new Dictionary<string, AssetBundleInfo>();
        private static ResManager mSingleton;

        public static ResManager singleton
        {
            get
            {
                if (mSingleton == null)
                {
                    mSingleton = new ResManager();
                }

                return mSingleton;
            }
        }

        /// <summary>
        /// 根据lua文件路径获得lua的字节流
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public byte[] GetLuaBytes(string name)
        {
            name = name.Replace(".", "/");

            return UpdateManager.singleton.GetLuaBytes(name);
        }

        /// <summary>
        /// 动态加载资源统一接口，如果从bundle里读取不到则从本地包中读取
        /// </summary>
        /// <param name="assetPath">资源路径，相对于Resources目录，例如 subdir/res1 </param>
        /// <param name="type">资源的类型，例如typeof(GameObject)</param>
        /// <returns>加载好的资源对象</returns>
        public UnityEngine.Object LoadAsset(string assetPath, Type type)
        {
            assetPath = CheckAssetPath(assetPath, type);
            if (assetPath == null)
            {
                return null;
            }

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                UnityEngine.Object obj = LoadAssetFromBundle(assetPath, type);
                if (obj != null)
                {
                    return obj;
                }
            }

            string path = assetPath.Remove(assetPath.LastIndexOf('.'));
            return Resources.Load(path);
        }

        /// <summary>
        /// 切换场景时可以卸载所有的资源
        /// </summary>
        public void CleanAllAsset()
        {
            foreach (var v in mLoadedAssetBundles)
            {
                v.Value.mAssetBundle.Unload(false);
            }

            mLoadedAssetBundles.Clear();

            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 检查资源路径，返回合理的资源路径，主要是给尾部添加资源扩展名
        /// </summary>
        /// <param name="assetPath">需检查的资源路径</param>
        /// <param name="type">资源类型</param>
        /// <returns>新的资源路径</returns>
        private string CheckAssetPath(string assetPath, System.Type type)
        {
            if (type == typeof(Material))
            {
                if (!assetPath.Contains(".mat"))
                    assetPath += ".mat";
            }
            else if (type == typeof(TextAsset))
            {
                if (!assetPath.Contains(".txt") && !assetPath.Contains(".xml"))
                    assetPath += ".txt";
            }
            else if (type == typeof(Scene))
            {
                if (!assetPath.Contains(".unity"))
                    assetPath += ".unity";
            }
            else if (type == typeof(Shader))
            {
                if (!assetPath.Contains(".shader"))
                    assetPath += ".shader";
            }
            else if (type == typeof(GameObject))
            {
                if (!assetPath.Contains(".prefab"))
                    assetPath += ".prefab";
            }
            else
            {
                Debug.LogWarning("LoadAsset: unsupport type " + type.Name);
                return null;
            }
            return assetPath;
        }

        /// <summary>
        /// 从AB包里加载资源
        /// </summary>
        /// <param name="assetPath">资源路径，例如 subdir/res1.prefab </param>
        /// <param name="type">资源类型</param>
        /// <returns>加载好的资源对象</returns>
        private UnityEngine.Object LoadAssetFromBundle(string assetPath, Type type)
        {
            //检查依赖关系是否加载，如果没有则加载
            if (mAssetBundleManifest == null)
            {
                AssetBundle manifestBundle = AssetBundle.LoadFromFile(ResUtils.BundleRootPath + ResUtils.GetBundleManifestName(Application.platform));
                if (manifestBundle == null)
                {
                    return null;
                }

                mAssetBundleManifest = manifestBundle.LoadAsset("AssetBundleManifest", typeof(AssetBundleManifest)) as AssetBundleManifest;
                if (mAssetBundleManifest == null)
                {
                    return null;
                }
            }

            //获取资源在包内部的名字，这个名字没有路径，但有扩展名，例如 res1.prefab
            string assetName = assetPath.Substring(assetPath.LastIndexOf("/") + 1).ToLower();

            //得到bundle文件的相对路径名，例如 subdir/res1.prefab.unity3d
            string bundleFileName = assetPath + '.' + ResUtils.BundleExtension;

            AssetBundleInfo bundleInfo = null;
            if (mLoadedAssetBundles.TryGetValue(bundleFileName, out bundleInfo))
            {
                bundleInfo.mReferencedCount++;

                if (!bundleInfo.mAssetBundle.isStreamedSceneAssetBundle)
                {
                    UnityEngine.Object obj = bundleInfo.mAssetBundle.LoadAsset(assetName, type);
                    return obj;
                }
                else
                {
                    return null; //场景包不需要加载资源，返回即可
                }
            }
            else
            {
                LoadDependencies(bundleFileName);

                bundleInfo = LoadAssetBundleSingle(bundleFileName);

                if (bundleInfo != null && !bundleInfo.mAssetBundle.isStreamedSceneAssetBundle)
                {
                    UnityEngine.Object obj = bundleInfo.mAssetBundle.LoadAsset(assetName, type);
                    return obj;
                }
                else
                {
                    return null; //场景包不需要加载资源，返回即可
                }
            }
        }

        /// <summary>
        /// 加载一个包文件依赖的所有包文件
        /// </summary>
        /// <param name="assetBundleName">包文件名称</param>
        private void LoadDependencies(string assetBundleName)
        {
            string[] dependencies = mAssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
            {
                return;
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                LoadAssetBundleSingle(dependencies[i]);
            }
        }

        /// <summary>
        /// 加载单独的包文件，不加载其依赖的包文件
        /// </summary>
        /// <param name="assetBundleName">包文件名</param>
        /// <returns>加载好的包</returns>
        private AssetBundleInfo LoadAssetBundleSingle(string assetBundleName)
        {
            AssetBundleInfo bundleInfo = null;
            if (mLoadedAssetBundles.TryGetValue(assetBundleName, out bundleInfo))
            {
                return bundleInfo;
            }

            string uri = ResUtils.BundleRootPath + assetBundleName;
            AssetBundle bundle = AssetBundle.LoadFromFile(uri);
            if (bundle != null)
            {
                bundleInfo = new AssetBundleInfo(bundle);
                mLoadedAssetBundles.Add(assetBundleName, bundleInfo);
            }
            
            return bundleInfo;
        }

    }
}
