using UnityEngine.Scripting;

// The vanilla eat-prompt box shrinks the whole handbook to fit one small panel.
// This window keeps a fixed reading size and scrolls.
[Preserve]
public class HSLiftHandbookWindow : XUiController
{
    public const string GroupId = "HSLiftHandbook";
    public static string TitleKey = "hsliftHandbook";
    public static string BodyKey = "hsliftHandbookText";

    public override void Init()
    {
        base.Init();
        var close = GetChildById("btnClose");
        if (close == null) return;
        var click = close.GetChildById("clickable");
        (click ?? close).OnPress += OnClosePressed;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        IsDirty = true;
        RefreshBindings();
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "title") { _value = Localization.Get(TitleKey); return true; }
        if (_bindingName == "body") { _value = Localization.Get(BodyKey); return true; }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }

    void OnClosePressed(XUiController sender, int mouseButton)
    {
        if (xui != null && xui.playerUI != null)
            xui.playerUI.windowManager.Close(GroupId);
    }
}
