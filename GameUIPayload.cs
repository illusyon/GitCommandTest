using UnityEngine;

public class GameUIPayload : MonoBehaviour
{
    public void Awake()
    {
        var windows = gameObject.GetComponentsInChildren<GameUIWindow>(true);

        //var root = GetComponent<UIRoot>();
        //if (root != null)
        //{
        //    if (root.manualHeight != GoodUI.instance.manualHeight)
        //    {
        //        float scaleFactor = (float)root.manualHeight;
        //        scaleFactor /= GoodUI.instance.manualHeight;

        //        foreach (var window in windows)
        //        {
        //            window.transform.localScale /= scaleFactor;
        //        }
        //    }
        //}

        GameUI.instance.onWindowsLoaded(this, windows);

        jettison();
    }

    public void jettison()
    {
        GameObject root = this.gameObject;

        DestroyObject(root);
    }
}