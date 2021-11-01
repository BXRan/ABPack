using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace U3DEditorUtility
{
    [CanEditMultipleObjects, CustomEditor(typeof(DefaultAsset))]
    public class LuaInspector : Editor
    {
        private GUIStyle m_TextStyle;

        public override void OnInspectorGUI()
        {
            if (m_TextStyle == null )
            {
                m_TextStyle = "ScriptText";
            }

            bool enabled = GUI.enabled;
            GUI.enabled = true;
            string assetPath = AssetDatabase.GetAssetPath(target);
            
            if (assetPath.EndsWith(".lua"))
            {
                string luaFile = File.ReadAllText(assetPath);
                string text;
                
                if (targets.Length > 1)
                {
                    text = Path.GetFileName(assetPath);
                }
                else
                {
                    text = luaFile;
                    if (text.Length > 7000)
                    {
                        text = text.Substring(0, 7000) + "...\n\n<...etc...>";
                    }
                }
                
                Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), this.m_TextStyle);
                rect.x = 0f;
                rect.y -= 3f;
                rect.width = EditorGUIUtility.currentViewWidth + 1f;
                GUI.Box(rect, text, this.m_TextStyle);
            }
            GUI.enabled = enabled;
        }
    }
}
