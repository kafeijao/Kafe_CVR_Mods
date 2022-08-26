namespace CCK.Debugger.Components.MenuHandlers;

public interface IMenuHandler {
    public void Load(Menu menu);
    public void Unload();
    public void Update(Menu menu);
}