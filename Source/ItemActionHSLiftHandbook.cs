using Audio;
using UnityEngine;
using UnityEngine.Scripting;

// Use the handbook: open the scrolling reader instead of the tiny shrink-to-fit prompt.
[Preserve]
public class ItemActionHSLiftHandbook : ItemAction
{
    public string TitleKey = "hsliftHandbook";
    public string BodyKey = "hsliftHandbookText";

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);
        if (_props.Values.ContainsKey("PromptTitle")) TitleKey = _props.Values["PromptTitle"];
        if (_props.Values.ContainsKey("PromptDescription")) BodyKey = _props.Values["PromptDescription"];
    }

    public override string CanInteract(ItemActionData _actionData)
    {
        return Localization.Get(TitleKey);
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased || _actionData == null || _actionData.invData == null) return;
        if (Time.time - _actionData.lastUseTime < Delay) return;
        var player = _actionData.invData.holdingEntity as EntityPlayerLocal;
        if (player == null || player.playerUI == null || player.playerUI.windowManager == null) return;
        _actionData.lastUseTime = Time.time;
        HSLiftHandbookWindow.TitleKey = TitleKey;
        HSLiftHandbookWindow.BodyKey = BodyKey;
        player.playerUI.windowManager.Open(HSLiftHandbookWindow.GroupId, true);
        var clip = GetSoundStart();
        if (!string.IsNullOrEmpty(clip)) Manager.PlayInsidePlayerHead(clip);
        try
        {
            if (QuestEventManager.Current != null && _actionData.invData.itemStack != null)
                QuestEventManager.Current.UsedItem(_actionData.invData.itemStack.itemValue);
        }
        catch (System.Exception) { }
    }
}
