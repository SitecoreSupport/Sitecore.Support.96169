using Sitecore.Data;
using Sitecore.Data.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Data.Serialization
{
    public static class CommonUtils
    {
        internal static void ClearCaches(Database database, ID itemID)
        {
            if (EventDisabler.IsActive)
            {
                database.Caches.ItemCache.RemoveItem(itemID);
                database.Caches.DataCache.RemoveItemInformation(itemID);
            }
        }

        public static Hashtable CreateCIHashtable()
        {
            return new Hashtable(StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsDirectoryHidden(string path)
        {
            return CommonUtils.IsHidden(new DirectoryInfo(path));
        }

        public static bool IsHidden(FileSystemInfo info)
        {
            return (info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }

        internal static void RemoveItemFromCaches(Database database, ID itemId)
        {
            database.Caches.ItemCache.RemoveItem(itemId);
            database.Caches.DataCache.RemoveItemInformation(itemId);
        }

        public static List<T> Uniq<T>(List<T> workset, IComparer<T> comparer)
        {
            List<T> list = new List<T>();
            T y = default(T);
            foreach (T current in workset)
            {
                if (comparer.Compare(current, y) != 0)
                {
                    y = current;
                    list.Add(current);
                }
            }
            return list;
        }
    }
}