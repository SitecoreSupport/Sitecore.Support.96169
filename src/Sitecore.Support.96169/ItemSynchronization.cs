using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.Exceptions;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Data.Serialization
{
    public static class ItemSynchronization
    {
        private static Template AssertTemplate(Database database, ID template)
        {
            Template template2 = database.Engines.TemplateEngine.GetTemplate(template);
            if (template2 == null)
            {
                database.Engines.TemplateEngine.Reset();
                template2 = database.Engines.TemplateEngine.GetTemplate(template);
            }
            Assert.IsNotNull(template2, "Template: " + template + " not found");
            return template2;
        }

        private static void AssertTemplates(Database database)
        {
        }

        public static SyncItem BuildSyncItem(Item item)
        {
            SyncItem syncItem = new SyncItem
            {
                ID = item.ID.ToString(),
                DatabaseName = item.Database.Name,
                ParentID = item.ParentID.ToString(),
                Name = item.Name,
                BranchId = item.BranchId.ToString(),
                TemplateID = item.TemplateID.ToString(),
                TemplateName = item.TemplateName,
                ItemPath = item.Paths.Path
            };
            item.Fields.ReadAll();
            item.Fields.Sort();
            foreach (Field field in item.Fields)
            {
                if (TemplateManager.GetTemplate(item).GetField(field.ID) != null && field.Shared)
                {
                    string fieldValue = ItemSynchronization.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        syncItem.AddSharedField(field.ID.ToString(), field.Name, field.Key, fieldValue, true);
                    }
                }
            }
            Item[] versions = item.Versions.GetVersions(true);
            Array.Sort<Item>(versions, new Comparison<Item>(ItemSynchronization.CompareVersions));
            Item[] array = versions;
            for (int i = 0; i < array.Length; i++)
            {
                Item version = array[i];
                ItemSynchronization.BuildVersion(syncItem, version);
            }
            return syncItem;
        }

        private static void BuildVersion(SyncItem item, Item version)
        {
            SyncVersion syncVersion = item.AddVersion(version.Language.ToString(), version.Version.ToString(), version.Statistics.Revision);
            if (syncVersion != null)
            {
                version.Fields.ReadAll();
                version.Fields.Sort();
                foreach (Field field in version.Fields)
                {
                    if (TemplateManager.GetTemplate(version).GetField(field.ID) != null && !field.Shared)
                    {
                        string fieldValue = ItemSynchronization.GetFieldValue(field);
                        if (fieldValue != null)
                        {
                            syncVersion.AddField(field.ID.ToString(), field.Name, field.Key, fieldValue, true);
                        }
                    }
                }
            }
        }

        private static int CompareVersions(Item left, Item right)
        {
            int num = left.Language.Name.CompareTo(right.Language.Name);
            if (num == 0)
            {
                num = left.Version.Number.CompareTo(right.Version.Number);
            }
            return num;
        }

        private static string GetFieldValue(Field field)
        {
            string value = field.GetValue(false, false);
            string result;
            if (value == null)
            {
                result = null;
            }
            else if (!field.IsBlobField)
            {
                result = value;
            }
            else
            {
                Stream blobStream = field.GetBlobStream();
                if (blobStream == null)
                {
                    result = null;
                }
                else
                {
                    using (blobStream)
                    {
                        byte[] array = new byte[blobStream.Length];
                        blobStream.Read(array, 0, array.Length);
                        result = System.Convert.ToBase64String(array, Base64FormattingOptions.InsertLineBreaks);
                    }
                }
            }
            return result;
        }

        private static bool NeedUpdate(Item item, SyncItem syncItem)
        {
            bool result;
            foreach (SyncVersion current in syncItem.Versions)
            {
                if (ItemSynchronization.NeedUpdate(item.Database.GetItem(item.ID, Language.Parse(current.Language), Sitecore.Data.Version.Parse(current.Version)), current))
                {
                    result = true;
                    return result;
                }
            }
            result = false;
            return result;
        }

        private static bool NeedUpdate(Item localVersion, SyncVersion version)
        {
            return localVersion == null || string.Compare(version.Values[FieldIDs.Updated.ToString()], localVersion[FieldIDs.Updated.ToString()], StringComparison.InvariantCulture) > 0;
        }

        private static void PasteField(Item item, SyncField field)
        {
            Template template = ItemSynchronization.AssertTemplate(item.Database, item.TemplateID);
            if (template.GetField(field.FieldID) == null)
            {
                item.Database.Engines.TemplateEngine.Reset();
                template = ItemSynchronization.AssertTemplate(item.Database, item.TemplateID);
            }
            if (template.GetField(field.FieldID) == null)
            {
                throw new FieldIsMissingFromTemplateException(string.Concat(new string[]
                {
                    "Field '",
                    field.FieldName,
                    "' does not exist in template '",
                    template.Name,
                    "'"
                }), FileUtil.MakePath(item.Template.InnerItem.Database.Name, item.Template.InnerItem.Paths.FullPath), FileUtil.MakePath(item.Database.Name, item.Paths.FullPath), item.ID);
            }
            Field field2 = item.Fields[ID.Parse(field.FieldID)];
            if (field2.IsBlobField && !MainUtil.IsID(field.FieldValue))
            {
                byte[] buffer = System.Convert.FromBase64String(field.FieldValue);
                field2.SetBlobStream(new MemoryStream(buffer, false));
            }
            else
            {
                field2.SetValue(field.FieldValue, true);
            }
        }

        public static Item PasteSyncItem(SyncItem syncItem, LoadOptions options)
        {
            return ItemSynchronization.PasteSyncItem(syncItem, options, false);
        }

        public static Item PasteSyncItem(SyncItem syncItem, LoadOptions options, bool failOnDataInconsistency)
        {
            Item result;
            if (syncItem == null)
            {
                result = null;
            }
            else
            {
                Exception ex = null;
                Database database = options.Database;
                if (database == null)
                {
                    database = Factory.GetDatabase(syncItem.DatabaseName);
                }
                ItemSynchronization.AssertTemplates(database);
                Item item = database.GetItem(syncItem.ParentID);
                ID iD = options.UseNewID ? ID.NewID : ID.Parse(syncItem.ID);
                Item item2 = database.GetItem(iD);
                LoadOptions loadOptions = new LoadOptions(options);
                bool flag = false;
                if (item2 == null)
                {
                    if (item == null)
                    {
                        if (failOnDataInconsistency)
                        {
                            ParentItemNotFoundException ex2 = new ParentItemNotFoundException
                            {
                                ParentID = syncItem.ParentID,
                                ItemID = syncItem.ID
                            };
                            throw ex2;
                        }
                        result = null;
                        return result;
                    }
                    else
                    {
                        ID iD2 = ID.Parse(syncItem.TemplateID);
                        ItemSynchronization.AssertTemplate(database, iD2);
                        item2 = ItemManager.AddFromTemplate(syncItem.Name, iD2, item, iD);
                        item2.Versions.RemoveAll(true);
                        loadOptions.ForceUpdate = true;
                        flag = true;
                    }
                }
                else
                {
                    if (!loadOptions.ForceUpdate)
                    {
                        loadOptions.ForceUpdate = ItemSynchronization.NeedUpdate(item2, syncItem);
                    }
                    if (loadOptions.ForceUpdate)
                    {
                        if (item == null && failOnDataInconsistency)
                        {
                            ParentForMovedItemNotFoundException ex3 = new ParentForMovedItemNotFoundException
                            {
                                ParentID = syncItem.ParentID,
                                Item = item2
                            };
                            ex = ex3;
                        }
                        if (item != null && item.ID != item2.ParentID)
                        {
                            item2.MoveTo(item);
                        }
                    }
                }
                Item item4;
                try
                {
                    ItemSynchronization.AssertTemplates(item2.Database);
                    if (loadOptions.ForceUpdate)
                    {
                        if (item2.TemplateID.ToString() != syncItem.TemplateID)
                        {
                            using (new EditContext(item2))
                            {
                                item2.RuntimeSettings.ReadOnlyStatistics = true;
                                item2.ChangeTemplate(item2.Database.Templates[ID.Parse(syncItem.TemplateID)]);
                            }
                            if (EventDisabler.IsActive)
                            {
                                CommonUtils.RemoveItemFromCaches(database, item2.ID);
                            }
                            item2.Reload();
                        }
                        if (item2.Name != syncItem.Name || item2.BranchId.ToString() != syncItem.BranchId)
                        {
                            using (new EditContext(item2))
                            {
                                item2.RuntimeSettings.ReadOnlyStatistics = true;
                                item2.Name = syncItem.Name;
                                item2.BranchId = ID.Parse(syncItem.BranchId);
                            }
                            CommonUtils.ClearCaches(item2.Database, item2.ID);
                            item2.Reload();
                        }
                        ItemSynchronization.ResetTemplateEngine(item2);
                        ItemSynchronization.AssertTemplates(item2.Database);
                        item2.Editing.BeginEdit();
                        item2.RuntimeSettings.ReadOnlyStatistics = true;
                        item2.RuntimeSettings.SaveAll = true;
                        if (options.ForceUpdate)
                        {
                            foreach (Field field in item2.Fields)
                            {
                                if (field.Shared)
                                {
                                    field.Reset();
                                }
                            }
                        }
                        foreach (SyncField current in syncItem.SharedFields)
                        {
                            ItemSynchronization.PasteField(item2, current);
                        }
                        item2.Editing.EndEdit();
                        if (EventDisabler.IsActive)
                        {
                        }
                        CommonUtils.ClearCaches(database, iD);
                        item2.Reload();
                        ItemSynchronization.ResetTemplateEngine(item2);
                    }
                    Hashtable hashtable = CommonUtils.CreateCIHashtable();
                    if (loadOptions.ForceUpdate)
                    {
                        Item[] versions = item2.Versions.GetVersions(true);
                        for (int i = 0; i < versions.Length; i++)
                        {
                            Item item3 = versions[i];
                            hashtable[item3.Uri] = null;
                        }
                    }
                    foreach (SyncVersion current2 in syncItem.Versions)
                    {
                        ItemSynchronization.PasteVersion(item2, current2, hashtable, loadOptions);
                    }
                    if (loadOptions.ForceUpdate)
                    {
                        foreach (ItemUri itemUri in hashtable.Keys)
                        {
                            if (options.Database != null)
                            {
                                options.Database.GetItem(itemUri.ToDataUri()).Versions.RemoveVersion();
                            }
                            else
                            {
                                Database.GetItem(itemUri).Versions.RemoveVersion();
                            }
                        }
                    }
                    CommonUtils.ClearCaches(item2.Database, item2.ID);
                    if (failOnDataInconsistency && ex != null)
                    {
                        throw ex;
                    }
                    item4 = item2;
                }
                catch (ParentForMovedItemNotFoundException ex4)
                {
                    throw ex4;
                }
                catch (ParentItemNotFoundException ex5)
                {
                    throw ex5;
                }
                catch (FieldIsMissingFromTemplateException ex6)
                {
                    throw ex6;
                }
                catch (Exception innerException)
                {
                    if (flag)
                    {
                        item2.Delete();
                        CommonUtils.ClearCaches(database, iD);
                    }
                    throw new Exception("Failed to paste item: " + syncItem.ItemPath, innerException);
                }
                result = item4;
            }
            return result;
        }

        private static void PasteVersion(Item item, SyncVersion syncVersion, Hashtable versions, LoadOptions options)
        {
            Language language = Language.Parse(syncVersion.Language);
            Sitecore.Data.Version version = Sitecore.Data.Version.Parse(syncVersion.Version);
            Item item2 = item.Database.GetItem(item.ID, language);
            Item item3 = item2.Versions[version];
            if (options.ForceUpdate || ItemSynchronization.NeedUpdate(item3, syncVersion))
            {
                if (item3 == null)
                {
                    item3 = item2.Versions.AddVersion();
                }
                else
                {
                    versions.Remove(item3.Uri);
                }
                if (options.ForceUpdate || item3.Statistics.Revision != syncVersion.Revision)
                {
                    ItemSynchronization.AssertTemplates(item3.Database);
                    item3.Editing.BeginEdit();
                    item3.RuntimeSettings.ReadOnlyStatistics = true;
                    if (options.ForceUpdate)
                    {
                        if (item3.Versions.Count == 0)
                        {
                            item3.Fields.ReadAll();
                        }
                        foreach (Field field in item3.Fields)
                        {
                            if (!field.Shared)
                            {
                                field.Reset();
                            }
                        }
                    }
                    bool flag = false;
                    foreach (SyncField current in syncVersion.Fields)
                    {
                        ID id;
                        if (ID.TryParse(current.FieldID, out id) && id == FieldIDs.Owner)
                        {
                            flag = true;
                        }
                        ItemSynchronization.PasteField(item3, current);
                    }
                    if (!flag)
                    {
                        item3.Fields[FieldIDs.Owner].Reset();
                    }
                    item3.Editing.EndEdit();
                    CommonUtils.ClearCaches(item3.Database, item3.ID);
                    ItemSynchronization.ResetTemplateEngine(item3);
                }
            }
        }

        public static Item ReadItem(TextReader reader)
        {
            return ItemSynchronization.ReadItem(reader, new LoadOptions());
        }

        public static Item ReadItem(TextReader reader, LoadOptions options)
        {
            return ItemSynchronization.PasteSyncItem(SyncItem.ReadItem(new Tokenizer(reader)), options);
        }

        public static Item ReadItem(TextReader reader, LoadOptions options, bool failOnDataInconsistency)
        {
            return ItemSynchronization.PasteSyncItem(SyncItem.ReadItem(new Tokenizer(reader)), options, failOnDataInconsistency);
        }

        private static void ResetTemplateEngine(Item target)
        {
            if (target.Database.Engines.TemplateEngine.IsTemplatePart(target))
            {
                target.Database.Engines.TemplateEngine.Reset();
            }
        }

        public static void WriteItem(Item item, TextWriter writer)
        {
            ItemSynchronization.BuildSyncItem(item).Serialize(writer);
        }
    }
}