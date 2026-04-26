using ReactiveUI;
using System.Collections.ObjectModel;

namespace OpcPlc.Gui.ViewModels.NodeEditor;

public abstract class NodeItemBase : ReactiveObject
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public ObservableCollection<NodeItemBase> Children { get; } = new ObservableCollection<NodeItemBase>();

    public abstract bool IsFolder { get; }
}
