using OpcPlc.PluginNodes;
using System.Collections.Generic;
using System.Linq;

namespace OpcPlc.Gui.ViewModels.NodeEditor;

public class FolderItem : NodeItemBase
{
    public override bool IsFolder => true;

    public static FolderItem FromConfigFolder(ConfigFolder config)
    {
        var item = new FolderItem { Name = config.Folder ?? string.Empty };

        if (config.FolderList != null)
        {
            foreach (var childFolder in config.FolderList)
            {
                item.Children.Add(FromConfigFolder(childFolder));
            }
        }

        if (config.NodeList != null)
        {
            foreach (var childNode in config.NodeList)
            {
                item.Children.Add(NodeItem.FromConfigNode(childNode));
            }
        }

        return item;
    }

    public ConfigFolder ToConfigFolder()
    {
        var folder = new ConfigFolder
        {
            Folder = Name,
            FolderList = new List<ConfigFolder>(),
            NodeList = new List<ConfigNode>(),
        };

        foreach (var child in Children)
        {
            if (child is FolderItem folderItem)
            {
                folder.FolderList.Add(folderItem.ToConfigFolder());
            }
            else if (child is NodeItem nodeItem)
            {
                folder.NodeList.Add(nodeItem.ToConfigNode());
            }
        }

        return folder;
    }
}
