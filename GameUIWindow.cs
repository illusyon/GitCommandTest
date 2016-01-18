using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameUIWindow : MonoBehaviour
{
    public bool autoComplete = true;
    public AudioClip defaultButtonSound;
    public GameObject tweenTarget;
    public int ab = 0;
    public bool tweenOnShow = true;
    public bool tweenOnHide = false;

    public string layerPath;

    public bool showing { get { return _showing; } private set { setShowing(value); } }
    public bool hiding { get; private set; }
    public bool ready { get { return _ready; } private set { setReady(value); } }

    private bool _showing;
    private bool _ready;

    #region GoodUIFocusManager.IGuideLayer impl

    public string getGuideLayerPath()
    {
        return layerPath;
    }
    #endregion

    private void setShowing(bool value)
    {
        _showing = value;
    }

    private void setReady(bool value)
    {
        _ready = value;
    }

    // TODO: Update() 함수에서 처리하면 좋겠지만, 서브 클래스에 모두 override 달아줘야 하므로 임시로 이렇게 처리.
    public void setLayerPath(string layerPath)
    {
        this.layerPath = layerPath;
    }

    public bool loaded
    {
        get { return _loaded; }
        internal set
        {
            if (value == _loaded) return;
            _loaded = true;
            onLoad();
        }
    }
    private bool _loaded;

    // TODO: clarify this
    public bool visible
    {
        get { return gameObject.activeInHierarchy; }
        set
        {
            if (visible != value)
            {
                if (visible)
                    show();
                else
                    hide();
            }
        }
    }

    public GameUIWindow()
    {
        showing = false;
        hiding = false;
        ready = false;
        loaded = false;
        _testing = false;
    }

    protected virtual void Awake()
    {
    }

    protected virtual void Start()
    {
    }

    protected virtual void OnEnable()
    {
        if (!loaded) return;

        if (!showing && !_testing)
        {
            if (!GameUI.instance.isTesting)
                Debug.LogWarning("GoodUIWindow enabled without show() or popup()", this);

            Comanager.Start(_TesterEnable());
        }
    }

    private bool _testing;

    private IEnumerator _TesterEnable()
    {
        yield return null;
        _testing = true;
        gameObject.SetActive(false);
        _testing = false;
        show();
    }

    protected virtual void OnDisable()
    {
        if (!loaded) return;

        if (!hiding && !_testing)
        {
            if (!GameUI.instance.isTesting)
                Debug.LogWarning("GoodUIWindow disabled without hide(): " + this.gameObject.name, this);

            Comanager.Start(_TesterDisable());
        }
    }

    private IEnumerator _TesterDisable()
    {
        yield return null;
        _testing = true;
        gameObject.SetActive(true);
        _testing = false;
        hide();
    }

    public object state
    {
        get { return _state; }
        set
        {
            if (_state != value)
            {
                _state = value;
                loadState();
            }
        }
    }

    [NonSerialized]
    protected object _state;

    ////////////////////////////////////

    public void show()
    {
        if (showing || ready) return;

        if (hiding)
            StopCoroutine("doHide");

        hiding = false;
        showing = true;
        ready = false;

        beforeShow();
        if (!showing)
            return;

        gameObject.SetActive(true);

        try
        {
            onShow();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }

        if (!showing)
        {
            gameObject.SetActive(false);
            return;
        }

        if (autoComplete)
            doAutoComplete(gameObject);

        if (!tweenOnShow || tweenTarget == null)
        {
            showing = false;
            ready = true;
        }
        else if (ab == 0)
        {
            tweenTarget.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
            iTween.ScaleTo(
                            tweenTarget,
                            iTween.Hash(
                            "scale", new Vector3(1.05f, 1.05f, 1.05f),
                            "time", 0.15f,
                            "islocal", true,
                            "easeType", iTween.EaseType.linear,
                            "ignoretimescale", true,
                            "oncompletetarget", gameObject,
                            "oncomplete", "ScaleAfter"));
        }
        else if (ab == 1)
        {
            tweenTarget.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            iTween.ScaleTo(
                            tweenTarget,
                            iTween.Hash(
                            "scale", new Vector3(1.1f, 1.1f, 1.1f),
                            "time", 0.15f,
                            "islocal", true,
                            "easeType", iTween.EaseType.linear,
                            "ignoretimescale", true,
                            "oncompletetarget", gameObject,
                            "oncomplete", "ScaleAfter"));
        }
    }

    void ScaleAfter()
    {
        if (ab == 0)
        {
            iTween.ScaleTo(
                        tweenTarget,
                        iTween.Hash(
                        "scale", Vector3.one,
                        "time", 0.1f,
                        "islocal", true,
                        "easeType", iTween.EaseType.spring,
                        "ignoretimescale", true,
                        "oncompletetarget", gameObject,
                        "oncomplete", "onShowWindowTweeningEnd"));
        }
        else if (ab == 1)
        {
            iTween.ScaleTo(
                        tweenTarget,
                        iTween.Hash(
                        "scale", Vector3.one,
                        "time", 0.1f,
                        "islocal", true,
                        "easeType", iTween.EaseType.spring,
                        "ignoretimescale", true,
                        "oncompletetarget", gameObject,
                        "oncomplete", "onShowWindowTweeningEnd"));
        }
    }

    void onShowWindowTweeningEnd()
    {
        showing = false;
        ready = true;

        if (autoComplete)
            doAutoComplete(gameObject);
    }

    public void hide()
    {
        if (showing)
        {
            StopCoroutine("doShow");
            if (tweenTarget != null)
            {
                foreach (var tween in tweenTarget.GetComponents<iTween>())
                    DestroyImmediate(tween);
            }

            showing = false;
            ready = false;
            hiding = false;
            gameObject.SetActive(false);
            return;
        }

        if (!visible || hiding) return;

        showing = false;
        ready = false;
        hiding = true;

        onHide();

        if (visible)
        {
            if (!tweenOnHide || tweenTarget == null)
                StartCoroutine("doHide");
            else
            {
                tweenTarget.transform.localScale = Vector3.one;
                iTween.ScaleTo(
                    tweenTarget,
                    iTween.Hash(
                        "scale", new Vector3(0.8f, 0.8f, 0.8f),
                        "time", 0.1f,
                        "islocal", true,
                        "easeType", iTween.EaseType.linear,
                        "oncompletetarget", gameObject,
                        "oncomplete", "onHideWindowTweeningEnd")
                );
            }
        }
    }

    void onHideWindowTweeningEnd()
    {
        gameObject.SetActive(false);
        hiding = false;
    }

    IEnumerator doHide()
    {
        // HACK: wait for 2 frames to prepare next shown window
        yield return null;
        //        yield return null;

        gameObject.SetActive(false);
        hiding = false;
    }

    internal void jettison()
    {
        onJettison();
    }

    // Override this method to provide additional jettison logic (remember to call base.onJettison())
    protected virtual void onJettison()
    {
        // remove all inactive camera references to let the components acquire scene's appropriate camera

        foreach (var anchor in GetComponentsInChildren<UIAnchor>(true))
        {
            if (anchor.uiCamera != null && !anchor.uiCamera.enabled)
                anchor.uiCamera = null;
        }

        foreach (var viewport in GetComponentsInChildren<UIViewport>(true))
        {
            if (viewport.sourceCamera != null && !viewport.sourceCamera.enabled)
                viewport.sourceCamera = null;
        }

        foreach (var stretch in GetComponentsInChildren<UIStretch>(true))
        {
            if (stretch.uiCamera != null && !stretch.uiCamera.enabled)
                stretch.uiCamera = null;
        }

        foreach (var tooltip in GetComponentsInChildren<UITooltip>(true))
        {
            if (tooltip.uiCamera != null && !tooltip.uiCamera.enabled)
                tooltip.uiCamera = null;
        }
    }

    protected virtual void onLoad()
    {
    }

    protected virtual void onUnload()
    {
    }

    // Override this method to provide your own reset code
    protected virtual void onReset()
    {
        if (autoComplete)
            doAutoComplete(gameObject);
    }

    public virtual bool isStateVisible(object state)
    {
        return true;
    }

    // Override this method when you need something before became active
    protected virtual void beforeShow()
    {
    }

    // Override this method when you need something after became active, before afterShow
    protected virtual void onShow()
    {
    }

    // Override this method to provide your own clean-up code
    protected virtual void onHide()
    {
    }

    // Override this methods to provide logic when user sets state or popup stack restores state
    protected virtual void loadState()
    {
    }

    public virtual void doAutoComplete(GameObject obj)
    {
        bool active = obj.activeInHierarchy;

        // collider needs to be active - so turn on temporarily
        obj.SetActive(true);

        // Let the children first
        for (int i = 0; i < obj.transform.childCount; ++i)
        {
            var child = obj.transform.GetChild(i).gameObject;

            var childPanel = child.GetComponent<GameUIWindow>();

            if (childPanel != null)
                childPanel.doAutoComplete(child);
            else
                this.doAutoComplete(child);
        }

        if (obj.name.StartsWith("btn-") || obj.name.StartsWith("btn_"))
        {
            if (obj.GetComponent<BoxCollider>() == null)
            {
                NGUITools.AddWidgetCollider(obj);
            }

            //if (obj.GetComponent<GoodUIWindowEventEnabler>() == null)
            //{
            //    var enabler = obj.AddComponent<GoodUIWindowEventEnabler>();
            //    enabler.window = this;
            //}

            if (obj.GetComponent<UIButtonOffset>() == null &&
                obj.GetComponent<UIButtonScale>() == null)
                //&& obj.GetComponent<UIButtonTween>() == null)
            {
                var offset = obj.AddComponent<UIButtonOffset>();
                offset.duration = 0.05f;
            }

            //if (obj.GetComponent<UIButtonSound>() == null &&
            //    defaultButtonSound != null)
            //{
            //    var sound = obj.AddComponent<UIButtonSound>();
            //    sound.audioClip = defaultButtonSound;
            //    sound.trigger = UIButtonSound.Trigger.OnPress;
            //}

            var msg = obj.GetComponent<UIButtonMessage>();

            if (msg != null && msg.target == null)
                msg.target = this.gameObject;

            if (msg != null && string.IsNullOrEmpty(msg.functionName))
                msg.functionName = "onButtonClick";

            var btn = obj.GetComponent<UIButton>();
            if (btn != null)
                btn.defaultColor = Color.white;
        }

        obj.SetActive(active); // restore active state
    }
}