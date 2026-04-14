using OpcPlc.Gui.Services;
using OpcPlc.Gui.ViewModels;
using OpcPlc.Gui.ViewModels.NodeEditor;
using System.IO;
using System.Windows.Input;
using Xunit;

namespace OpcPlc.Gui.Tests.Services;

public class MainWindowViewModelSaveTests
{
    [Fact]
    public void SaveNodesCommand_WritesJsonToDisk()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nodes-save-{System.Guid.NewGuid()}.json");

        try
        {
            var vm = new MainWindowViewModel();
            vm.NodeEditor.Root = new FolderItem
            {
                Name = "TestRoot",
            };
            vm.NodeEditor.Root.Children.Add(new NodeItem
            {
                NodeId = "42",
                Name = "Answer",
                DataType = "Int32",
            });

            var service = new NodesFileService(tempPath);
            service.Save(vm.NodeEditor.Root);

            var json = File.ReadAllText(tempPath);
            Assert.Contains("TestRoot", json);
            Assert.Contains("Answer", json);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}
