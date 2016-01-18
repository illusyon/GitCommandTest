using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;


public class GameUI
{
    private static GameUI _instance;
    public static GameUI instance { get { return _instance != null ? _instance : _instance = new GameUI(); } }

    public class SceneEntry
    {
        public string name;
        public Dictionary<string, GameUIWindow> windows = new Dictionary<string, GameUIWindow>();

        public bool isEmpty { get { return windows.Count == 0; } }
        public bool isLoading;
    }

    Dictionary<string, SceneEntry> _sceneEntries = new Dictionary<string, SceneEntry>();

    public class WindowEntry
    {
        public string name;
        public GameUIWindow window;
        public SceneEntry sceneEntry;
    }

    Dictionary<string, WindowEntry> _windowEntries = new Dictionary<string, WindowEntry>();

    public WindowEntry Register(string windowName, string sceneName = null)
    {
        if (sceneName == null)
            sceneName = windowName;

        SceneEntry sceneEntry;
        if (!_sceneEntries.TryGetValue(sceneName, out sceneEntry))
            sceneEntry = _sceneEntries[sceneName] = new SceneEntry() { name = sceneName };

        WindowEntry windowEntry;
        if (!_windowEntries.TryGetValue(windowName, out windowEntry))
            windowEntry = _windowEntries[windowName] = new WindowEntry() { name = windowName, sceneEntry = sceneEntry };

        return windowEntry;
    }

    private GameUI()
    {
        createLayerRoot("GUI").depth = 30;
        createLayerRoot("GUIOverlay").depth = 60;

        Comanager.Start(updatePopupQueue());
    }

    public bool isTesting = true;

    Dictionary<int, GameObject> layerRoot = new Dictionary<int, GameObject>();

    //public int manualHeight = 500;
    //public int minimumHeight = 320;
    //public int maximumHeight = 1080;

    public int manualHeight = 720;
    public int minimumHeight = 320;
    public int maximumHeight = 1080;

    Camera createLayerRoot(string layer)
    {
        var root = new GameObject();
        root.name = "GameUI_" + layer;
        GameObject.DontDestroyOnLoad(root);

        var camera = root.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Depth;
        camera.cullingMask = 1 << LayerMask.NameToLayer(layer);

        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.nearClipPlane = -2.0f;
        camera.farClipPlane = 2.0f;

        var uiCamera = root.AddComponent<UICamera>();
        uiCamera.allowMultiTouch = false;

        var uiRoot = root.AddComponent<UIRoot>();
        uiRoot.scalingStyle = UIRoot.Scaling.Flexible;
        uiRoot.manualHeight = manualHeight;
        uiRoot.minimumHeight = minimumHeight;
        uiRoot.maximumHeight = maximumHeight;

        layerRoot[LayerMask.NameToLayer(layer)] = root;

        return camera;
    }

    public GameObject getLayerRoot(int layer)
    {
        return layerRoot[layer];
    }

    public delegate void AfterShow(GameUIWindow shown);
    public delegate void AfterLoad(GameUIWindow loaded);
    public delegate void OnLoadError();

    public void show(string windowName, AfterShow afterShow = null)
    {
        Comanager.Start(doLoadWindow(windowName, (loaded) =>
        {
            loaded.show();
            if (afterShow != null)
                afterShow(loaded);
        }, null));
    }

    public void hide(string windowName)
    {
        WindowEntry windowEntry;
        if (!_windowEntries.TryGetValue(windowName, out windowEntry)) return;

        var window = windowEntry.window;
        if (window == null) return;

        window.hide();
    }

    public void loadWindow(string windowName, AfterLoad afterLoad = null, OnLoadError onLoadError = null)
    {
        Comanager.Start(doLoadWindow(windowName, afterLoad, onLoadError));
    }

    public GameUIWindow getWindow(string windowName)
    {
        WindowEntry windowEntry;
        if (!_windowEntries.TryGetValue(windowName, out windowEntry))
            return null; // TODO: exception??

        return windowEntry.window;
    }

    public bool isVisible(string windowName)
    {
        var window = getWindow(windowName);

        return window ? window.visible : false;
    }

    IEnumerator doLoadWindow(string windowName, AfterLoad afterLoad, OnLoadError onLoadError = null)
    {
        WindowEntry windowEntry;
        if (!_windowEntries.TryGetValue(windowName, out windowEntry))
        {
            Debug.LogError(string.Format("Not registered window '{0}'", windowName));
            if (onLoadError != null) onLoadError();
            yield break;
        }

        var sceneEntry = windowEntry.sceneEntry;

        if (windowEntry.window == null)
        {
            if (sceneEntry.isLoading)
            {
                while (sceneEntry.isLoading)
                    yield return null;
            }
            else if (sceneEntry.isEmpty)
            {
                sceneEntry.isLoading = true;
                // Wait for the needed scene loaded

                AsyncOperation async;
                try { async = Application.LoadLevelAdditiveAsync(sceneEntry.name); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    async = null;
                }

                if (async == null)
                {
                    if (onLoadError != null) onLoadError();
                    yield break;
                }

                yield return async;

                sceneEntry.isLoading = false;
            }
        }

        // We can assume that the window stat is now filled by onWindowsLoaded(). if not, the window does not exist.
        var window = windowEntry.window;

        if (window == null)
        {
            Debug.LogError(string.Format("Can't find window '{0}' from scene '{1}'", windowEntry.name, sceneEntry.name));
            if (onLoadError != null) onLoadError();
            yield break;
        }

        if (afterLoad != null)
        {
            afterLoad(window);
        }
    }

    /// Called from GameUIRoot.Awake()
    internal void onWindowsLoaded(GameUIPayload payload, GameUIWindow[] windows)
    {
        GameObject payloadObject = payload.gameObject;

        // The name will be used as the scene name if not registered in advance
        string payloadName = payloadObject.name;

        SceneEntry sceneEntry = null;

        // link to registered entry and try to determine the scene name which windows are loaded from
        foreach (var win in windows)
        {
            string windowName = win.gameObject.name;

            WindowEntry windowEntry;
            if (!_windowEntries.TryGetValue(windowName, out windowEntry))
            {
                // not registered during startup, but present - allow delayed registering
                // sceneEntry will be linked later according to this windows registered sibling
                windowEntry = _windowEntries[windowName] = new WindowEntry() { name = windowName, sceneEntry = null };
                Debug.Log(string.Format("delay loaded window '{0}'", windowName));
            }

            if (windowEntry.sceneEntry != null)
            {
                if (sceneEntry != null && windowEntry.sceneEntry != sceneEntry)
                {
                    Debug.LogError(string.Format("Inconsistent scene name '{0}' != '{1}' for window '{2}'",
                        sceneEntry.name, windowEntry.sceneEntry.name, windowName));
                }

                sceneEntry = windowEntry.sceneEntry;
            }
        }

        if (sceneEntry == null)
        {
            // Can't determine scene name by windows - Assume the payload name as scene name as a last resort.
            if (!_sceneEntries.TryGetValue(payloadName, out sceneEntry))
                sceneEntry = _sceneEntries[payloadName] = new SceneEntry() { name = payloadName };
        }

        foreach (var win in windows)
        {
            string windowName = win.gameObject.name;

            var windowEntry = _windowEntries[windowName];
            if (windowEntry.sceneEntry == null)
            {
                windowEntry.sceneEntry = sceneEntry;
                sceneEntry.windows.Add(windowName, win);
            }
        }

        // We now have a scene entry and a window entry for each window linked to the entry

        // Link to registered entry and reattach to window root
        foreach (var win in windows)
        {
            GameObject windowObject = win.gameObject;
            string windowName = windowObject.name;

            WindowEntry windowEntry = _windowEntries[windowName];

            if (windowEntry.window != null && windowEntry.window != win)
            {
                Debug.LogError(string.Format("Tried to load window '{0}' from scene '{1}' but window from '{2}' already loaded!",
                    windowName, payloadName, windowEntry.sceneEntry.name));
                continue;
            }

            // link the window to its entry
            windowEntry.window = win;

            Debug.Log(string.Format("window '{0}' from '{1}' loaded", windowName, sceneEntry.name));

            var root = getLayerRoot(win.gameObject.layer);

            // reattach to layer root
            var localScale = windowObject.transform.localScale;
            var localPos = windowObject.transform.localPosition;
            var localRot = windowObject.transform.localRotation;

            windowObject.transform.parent = root.transform;

            windowObject.transform.localScale = localScale;
            windowObject.transform.localRotation = localRot;
            windowObject.transform.localPosition = localPos;

            // turn off windows at first
            windowObject.SetActive(false);
        }

        // jettison payload
        payload.jettison();

        foreach (var win in windows)
        {
            win.jettison();

            // Mark the window as loaded
            win.loaded = true;
        }
    }

    public delegate void AfterPopupClose(GameUIWindow closed);

    class PopupEntry
    {
        public bool valid = true;
        public string windowName;
        public object state;
        public bool loading = false;
        public bool loaded = false;
        public bool suspended = true;
        public AfterShow afterShow;
        public AfterPopupClose afterClose;
        public GameUIWindow window;
    }

    Stack<PopupEntry> _popupStack = new Stack<PopupEntry>();
    PriorityQueue<float, PopupEntry> _popupQueue = new PriorityQueue<float, PopupEntry>();

    public void popup(string windowName, AfterShow afterShow = null, AfterPopupClose afterClose = null)
    {
        if (_popupStack.Count > 0)
        {
            var popupEntry = _popupStack.Peek();
            if (popupEntry.window != null)
            {
                if (popupEntry.window.gameObject.activeInHierarchy && !popupEntry.window.hiding)
                {
                    popupEntry.state = popupEntry.window.state;
                    popupEntry.suspended = true;
                    popupEntry.window.hide();
                }
            }
        }

        // push the requested popup into stack
        var entry = new PopupEntry()
        {
            windowName = windowName,
            afterShow = afterShow,
            afterClose = afterClose,
        };

        _popupStack.Push(entry);
        // the popup will appear on next updatePopupQueue()
    }

    public void queuePopup(string windowName, AfterShow afterShow = null, AfterPopupClose afterClose = null, float priority = 0.0f)
    {
        var entry = new PopupEntry()
        {
            windowName = windowName,
            afterShow = afterShow,
            afterClose = afterClose,
        };

        _popupQueue.enqueue(entry, priority);

        // the popup will appear on next updatePopupQueue() after all previous popups closed
    }

    IEnumerator updatePopupQueue()
    {
        while (true)
        {
            // suspend to the next frame
            yield return null;

            PopupEntry popupEntry;

            // feed the stack while something valid in the queue
            while (_popupStack.Count == 0 && _popupQueue.count > 0)
            {
                popupEntry = _popupQueue.dequeue();
                if (!popupEntry.valid) continue;
                _popupStack.Push(popupEntry);
            }

            // wait for a valid popup in the stack
            if (_popupStack.Count == 0)
                continue;

            popupEntry = _popupStack.Peek();

            if (popupEntry.loading)
            {
                continue;
            }

            if (!popupEntry.loaded)
            {
                popupEntry.loading = true;

                // not loaded yet - load
                Comanager.Start(doLoadWindow(popupEntry.windowName,
                    (loaded) =>
                    {
                        // loaded - link the window
                        popupEntry.window = loaded;
                        popupEntry.loading = false;
                        popupEntry.loaded = true;

                        // it's default to suspended, so will wake up at next update loop
                    },
                    () =>
                    {
                        // error
                        popupEntry.window = null;
                        popupEntry.loading = false;
                        popupEntry.loaded = true;
                    }));

                continue;
            }

            if (popupEntry.window == null)
            {
                // tried but can't load - just pop from the stack
                _popupStack.Pop();
                continue;
            }

            // just continue if window is running
            if (popupEntry.window.visible)
                continue;

            if (popupEntry.suspended)
            {
                if (!popupEntry.window.hiding)
                {
                    // resume
                    popupEntry.suspended = false;

                    var win = popupEntry.window;

                    var stateVisible = popupEntry.state != null ? win.isStateVisible(popupEntry.state) : true;
                    if (stateVisible)
                    {
                        win.show();
                        win.state = popupEntry.state;
                        popupEntry.state = null; // release state object

                        if (popupEntry.afterShow != null)
                        {
                            try { popupEntry.afterShow(win); }
                            catch (Exception ex) { Debug.LogException(ex); }
                            popupEntry.afterShow = null; // only once
                        }

                        if (win.showing || win.ready)
                            continue;

                        // Maybe win.show() or win.loadState() or afterShow() hides it again - fall through
                    }
                }

                // the callback of the hidden window tried to hide the window again.
                // should be popuped from the stack and will not show
            }

            // the top window is neither visible nor suspended
            // that means the window is now closed - Pop from the stack
            _popupStack.Pop();

            if (popupEntry.afterClose != null)
            {
                try { popupEntry.afterClose(popupEntry.window); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
    }
}

public class WeakRef<TObject> where TObject : class
{
    WeakReference peer = null;

    public bool isAlive { get { return peer != null ? peer.IsAlive : false; } }
    public TObject target
    {
        get { return peer.Target as TObject; }
        set
        {
            if (value == null)
                peer = null;
            else if (peer == null)
                peer = new WeakReference(value);
            else if (peer.Target != value)
                peer = new WeakReference(value);
        }
    }

    public static implicit operator TObject(WeakRef<TObject> w)
    {
        return w.target;
    }

    public static implicit operator WeakRef<TObject>(TObject obj)
    {
        return new WeakRef<TObject>(obj);
    }

    public WeakRef()
    {
    }

    public WeakRef(TObject obj)
    {
        if (obj != null) target = obj;
    }
}

public class PriorityQueue<TPriority, TItem> : IEnumerable
{
    int _totalCount = 0;
    SortedDictionary<TPriority, Queue<TItem>> _storage = new SortedDictionary<TPriority, Queue<TItem>>();

    public PriorityQueue()
    {
    }

    public int count { get { return _totalCount; } }
    public bool isEmpty() { return _totalCount == 0; }

    public TItem dequeue()
    {
        if (isEmpty())
            throw new Exception("empty queue");
        foreach (var e in _storage)
        {
            var q = e.Value;
            if (q.Count > 0)
            {
                --_totalCount;
                return q.Dequeue();
            }
        }

        throw new Exception("should not reach here");
    }

    public TItem peek()
    {
        if (isEmpty())
            throw new Exception("empty queue");

        foreach (var e in _storage)
        {
            var q = e.Value;
            if (q.Count > 0)
                return q.Peek();
        }

        throw new Exception("should not reach here");
    }

    public void enqueue(TItem item, TPriority prio)
    {
        Queue<TItem> q;
        if (!_storage.TryGetValue(prio, out q))
            q = _storage[prio] = new Queue<TItem>();

        q.Enqueue(item);
        ++_totalCount;
    }

    public void clear()
    {
        _storage.Clear();
    }

    public bool contains(TItem item)
    {
        foreach (var e in _storage)
        {
            var q = e.Value;
            if (q.Contains(item)) return true;
        }

        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var e in _storage)
        {
            var q = e.Value;
            foreach (var item in q)
                yield return item;
        }
    }
};
