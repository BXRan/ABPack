using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace U3DUtility
{
    public class BundleItem
    {
        public int m_Version;
        public int m_FileSize;
        public string m_Name;
        public string m_HashCode;
    }

    public class IdxFile
    {
        List<string[]> m_ArrayData = new List<string[]>();

        bool m_IsFirstRowAsCols = false;

        bool IsFirstRowAsCols
        {
            set { m_IsFirstRowAsCols = value; }
            get { return m_IsFirstRowAsCols; }
        }

        int RowCount
        {
            get
            {
                if (m_IsFirstRowAsCols)
                {
                    if (m_ArrayData.Count > 0)
                    {
                        return m_ArrayData.Count - 1;
                    }
                    return 0;
                }
                else
                {
                    return m_ArrayData.Count;
                }
            }
        }

        int ColCount
        {
            get
            {
                if (m_ArrayData.Count > 0)
                    return m_ArrayData[0].Length;
                return 0;
            }
        }

        public List<BundleItem> Load(string fileContent)
        {
            m_ArrayData.Clear();

            StringReader sr = new StringReader(fileContent);

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                m_ArrayData.Add(line.Split('\t'));
            }
            sr.Close();
            sr.Dispose();

            List<BundleItem> list = new List<BundleItem>();
            for (int i = 0; i < RowCount; i++)
            {
                BundleItem item = new BundleItem();
                item.m_Name = GetString(i, 0);
                item.m_Version = GetInt(i, 1);
                item.m_HashCode = GetString(i, 2);
                item.m_FileSize = GetInt(i, 3);
                list.Add(item);
            }

            return list;
        }

        static public string SaveString(List<BundleItem> list, string path)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var v in list)
            {
                sb.Append(v.m_Name);
                sb.Append('\t');
                sb.Append(v.m_Version);
                sb.Append('\t');
                sb.Append(v.m_HashCode);
                sb.Append('\t');
                sb.Append(v.m_FileSize);
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        string GetString(int row, int col)
        {
            if (m_IsFirstRowAsCols)
                row = row + 1;
            if (row < 0 || row >= m_ArrayData.Count)
                return null;
            if (col < 0 || col >= m_ArrayData[row].Length)
                return null;
            return m_ArrayData[row][col];
        }

        int GetInt(int row, int col)
        {
            if (m_IsFirstRowAsCols)
                row = row + 1;
            if (row < 0 || row >= m_ArrayData.Count)
                return 0;
            if (col < 0 || col >= m_ArrayData[row].Length)
                return 0;
            return int.Parse(m_ArrayData[row][col]);
        }

        float GetFloat(int row, int col)
        {
            if (m_IsFirstRowAsCols)
                row = row + 1;
            if (row < 0 || row >= m_ArrayData.Count)
                return 0;
            if (col < 0 || col >= m_ArrayData[row].Length)
                return 0;
            return float.Parse(m_ArrayData[row][col]);
        }

    }
}

