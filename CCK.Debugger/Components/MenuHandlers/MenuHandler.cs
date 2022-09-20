namespace CCK.Debugger.Components.MenuHandlers;

public abstract class MenuHandler {

    // TMP Colors
    protected const string White = "<color=white>";
    protected const string Blue = "<#00AFFF>";
    protected const string Purple = "<#A000C8>";

    public abstract void Load(Menu menu);
    public abstract void Unload();
    public abstract void Update(Menu menu);
}
