namespace WoWHeightGenGUI.UI;

public interface IPanel : IDisposable
{
    string Name { get; }
    bool IsVisible { get; set; }
    void Render();
    void Update(float deltaTime);
}

public interface IConnectionAwarePanel
{
    void OnConnectionChanged();
}
