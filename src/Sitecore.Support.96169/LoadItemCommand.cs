using Sitecore.Data.Items;
using Sitecore.Data.Serialization;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Shell.Applications.Dialogs.ProgressBoxes;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Shell.Framework.Commands.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace Sitecore.Support.Shell.Framework.Commands.Serialization
{
    [Serializable]
    public class LoadItemCommand : BaseLoadCommand
    {
        protected virtual void AuditLoad(Item item)
        {
            Log.Audit(this, "Deserializing item {0}", new string[]
            {
                item.Paths.FullPath
            });
        }

        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            Item item = context.Items[0];
            this.AuditLoad(item);
            ProgressBox.Execute("ItemSync", Translate.Text("Load"), this.GetIcon(context, string.Empty), new ProgressBoxMethod(this.LoadItem), "item:load(id=" + item.ID + ")", new object[]
            {
                item,
                context
            });
        }

        private void LoadItem(params object[] parameters)
        {
            CommandContext commandContext = parameters[1] as CommandContext;
            if (commandContext != null)
            {
                Item item = parameters[0] as Item;
                if (item != null)
                {
                    this.LoadItem(item, this.GetOptions(commandContext));
                }
            }
        }

        protected virtual Item LoadItem(Item item, LoadOptions options)
        {
            Assert.ArgumentNotNull(item, "item");
            string filePath = PathUtils.GetFilePath(new ItemReference(item).ToString());
            Thread.Sleep(700);
            return Manager.LoadItem(filePath, options);
        }
    }
}