using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace U3DUtility
{
    public class UGUIUtils
    {
        public static bool IsPointerOverUIObject(Vector2 screenPosition, string ignoreObjName)
        {
            //实例化点击事件
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            //将点击位置的屏幕坐标赋值给点击事件
            eventDataCurrentPosition.position = new Vector2(screenPosition.x, screenPosition.y);

            List<RaycastResult> results = new List<RaycastResult>();
            //向点击处发射射线
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            int count = 0;
            foreach (var v in results)
            {
                if (v.gameObject.name != ignoreObjName)
                {
                    count++;
                }
            }

            return count > 0;
        }
    }
}
