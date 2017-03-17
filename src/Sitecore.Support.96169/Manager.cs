using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.Exceptions;
using Sitecore.Diagnostics;
using Sitecore.Eventing;
using Sitecore.Jobs;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Data.Serialization
{
    public static class Manager
    {
        public static Item LoadItem(string path, LoadOptions options)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            Assert.ArgumentNotNull(options, "options");
            Item result;
            if (!options.DisableEvents)
            {
                result = Manager.DoLoadItem(path, options);
            }
            else
            {
                Item item;
                using (new EventDisabler())
                {
                    item = Manager.DoLoadItem(path, options);
                }
                Manager.DeserializationFinished(Manager.GetTargetDatabase(path, options));
                result = item;
            }
            return result;
        }

        private static Item DoLoadItem(string path, LoadOptions options)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            Assert.ArgumentNotNull(options, "options");
            Item result;
            if (File.Exists(path))
            {
                using (TextReader textReader = new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    Manager.LogLocalized("Loading item from path {0}.", new object[]
                    {
                        PathUtils.UnmapItemPath(path, options.Root)
                    });
                    bool disabledLocally = ItemHandler.DisabledLocally;
                    try
                    {
                        ItemHandler.DisabledLocally = true;
                        Item item = null;
                        try
                        {
                            item = ItemSynchronization.ReadItem(textReader, options, true);
                        }
                        catch (ParentItemNotFoundException ex)
                        {
                            item = null;
                            Manager.LogLocalizedError("Cannot load item from path '{0}'. Possible reason: parent item with ID '{1}' not found.", new object[]
                            {
                                PathUtils.UnmapItemPath(path, options.Root),
                                ex.ParentID
                            });
                        }
                        catch (ParentForMovedItemNotFoundException ex2)
                        {
                            item = ex2.Item;
                            Manager.LogLocalizedError("Item from path '{0}' cannot be moved to appropriate location. Possible reason: parent item with ID '{1}' not found.", new object[]
                            {
                                PathUtils.UnmapItemPath(path, options.Root),
                                ex2.ParentID
                            });
                        }
                        result = item;
                        return result;
                    }
                    finally
                    {
                        ItemHandler.DisabledLocally = disabledLocally;
                    }
                }
            }
            result = null;
            return result;
        }

        private static void LogLocalized(string message, params object[] parameters)
        {
            Assert.IsNotNullOrEmpty(message, "message");
            Job job = Context.Job;
            if (job != null)
            {
                job.Status.LogInfo(message, parameters);
            }
            else
            {
                Log.Info(message.FormatWith(parameters), new object());
            }
        }

        private static void LogLocalizedError(string message, params object[] parameters)
        {
            Assert.IsNotNullOrEmpty(message, "message");
            Job job = Context.Job;
            if (job != null)
            {
                job.Status.LogError(message.FormatWith(parameters));
            }
            else
            {
                Log.Error(message.FormatWith(parameters), new object());
            }
        }

        private static void DeserializationFinished(string databaseName)
        {
            EventManager.RaiseEvent<SerializationFinishedEvent>(new SerializationFinishedEvent());
            Database database = Factory.GetDatabase(databaseName, false);
            if (database != null)
            {
                database.RemoteEvents.Queue.QueueEvent<SerializationFinishedEvent>(new SerializationFinishedEvent());
            }
        }

        private static string GetTargetDatabase(string path, LoadOptions options)
        {
            string result;
            if (options.Database != null)
            {
                result = options.Database.Name;
            }
            else
            {
                result = ItemReference.Parse(PathUtils.UnmapItemPath(path, PathUtils.Root)).Database;
            }
            return result;
        }
    }
}