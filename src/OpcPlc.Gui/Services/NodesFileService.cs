using Newtonsoft.Json;
using OpcPlc.Gui.ViewModels.NodeEditor;
using OpcPlc.PluginNodes;
using System;
using System.IO;
using System.Reflection;

namespace OpcPlc.Gui.Services;

public class NodesFileService
{
    private readonly string _appDataPath;

    public string ResolvedPath => _appDataPath;

    public NodesFileService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "OpcPlc.Gui");
        Directory.CreateDirectory(dir);
        _appDataPath = Path.Combine(dir, "nodesfile.json");

        EnsureExtracted();
    }

    /// <summary>
    /// For testing only: use a custom file path directly.
    /// </summary>
    internal NodesFileService(string customPath)
    {
        _appDataPath = customPath;
    }

    public FolderItem? Load()
    {
        if (!File.Exists(_appDataPath))
        {
            return null;
        }

        var json = File.ReadAllText(_appDataPath);
        var config = JsonConvert.DeserializeObject<ConfigFolder>(json);
        if (config == null)
        {
            return null;
        }

        return FolderItem.FromConfigFolder(config);
    }

    public void Save(FolderItem root)
    {
        var config = root.ToConfigFolder();
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(_appDataPath, json);
    }

    private void EnsureExtracted()
    {
        if (File.Exists(_appDataPath))
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "OpcPlc.Gui.nodesfile.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            File.WriteAllText(_appDataPath, reader.ReadToEnd());
        }
        else
        {
            // Fallback: create an empty root folder if embedded resource is missing
            File.WriteAllText(_appDataPath, "{\n  \"Folder\": \"MyTelemetry\"\n}");
        }
    }
}
