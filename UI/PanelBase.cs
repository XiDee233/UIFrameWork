using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YatBun.Excel;
using YatBun.Framework.Back;
using YatBun.Game.Core.Sdk;
using YatBun.Game.Core.UI;
using YatBun.Game.Core.Utils;
using YatBun.Game.Manager;
using YatBun.Game.Module.CommonUI;
using YatBun.Game.Module.FairyLand;
using YatBun.Game.Module.Team;
using FairyGUI;
using Game.Manager;
using Game.Module.Rule;
using UnityEngine;

namespace YatBun.Framework
{
    public enum PanelState
    {
        SHOW_START,
        SHOW_OVER,
        HIDE_START,
        HIDE_OVER,
    }

    public class PanelBg
    {
        private PLoader _loader;
        private string _bgUrl;

        public string BgUrl => _bgUrl;

        public PLoader Loader
        {
            get
            {
                if (_loader == null)
                {
                    _loader = new PLoader()
                    {
                        touchable = false,
                        align = AlignType.Center,
                        verticalAlign = VertAlignType.Middle,
                        fill = FillType.ScaleFree,
                        position = new Vector3(0, 0, 1000),
                        sortingOrder = (int) UILayerEnum.BACK_GROUND
                    };
                    _loader.ToSync();
                    var scaleX = GRoot.inst.width / GameConfig.UI_BG_WIDTH;
                    var scaleY = GRoot.inst.height / GameConfig.UI_BG_HEIGHT;
                    var scale = scaleX > scaleY ? scaleX : scaleY;
                    var w = GameConfig.UI_WIDTH;
                    var h = (int) (GameConfig.UI_BG_HEIGHT * scale);
                    _loader.SetSize(w, h);
                    _loader.x = -(w - GRoot.inst.width) * 0.5f;
                    _loader.y = -(h - GRoot.inst.height) * 0.5f;
                }

                return _loader;
            }
        }

        public string key;

        public PanelBg()
        {
        }

        public void Load(string url, string name)
        {
            Loader.visible = true;
            Loader.name = name;
            _bgUrl = url;
            LoadManager.Instance.LoadTexture(Loader, _bgUrl);
        }

        public void Unload()
        {
            Loader.visible = false;
            LoadManager.Instance.UnloadTexture(Loader);
            _bgUrl = null;
        }

        public void Dispose()
        {
            if (Loader == null) return;
            Loader.visible = false;
            Loader?.Dispose();
            _loader?.RemoveFromParent();
            _loader = null;
            _bgUrl = null;
        }

        public void Show(float time)
        {
            if (Loader == null) return;
            GTween.Kill(Loader);
            Loader.visible = true;
            if (time <= 0)
            {
                Loader.alpha = 1;
            }
            else
            {
                Loader.alpha = 0.5f;
                Loader.TweenFade(1, time);
            }
        }

        public void Hide(float time)
        {
            GTween.Kill(Loader);
            if (time <= 0)
            {
                Loader.alpha = 0;
            }
            else
            {
                Loader.TweenFade(0, time);
            }
        }

        public void SetVisible(bool isVisible)
        {
            if (isVisible)
            {
                Loader.alpha = 1;
                Loader.visible = true;
            }
            else
            {
                Loader.TweenFade(0, 0.1f).OnComplete(() => { Loader.visible = false; });
            }
        }

        public void HideImmediately()
        {
            GTween.Kill(Loader);
            Loader.alpha = 0;
        }
    }

    public class PanelBase : PanelCore
    {
        public static int ALL_PANEL_INSTANCE_COUNT = 0;
        public static List<string> _gcPanelNameList = new List<string>();

        private static List<PanelBg> _panelBgPool = new List<PanelBg>();
        private List<DelayTool> _timeDelays = new List<DelayTool>();

        public GComponent skinRoot { get; private set; }

        public PanelState panelState { get; private set; }

        public bool isWaitForData { get; private set; }

        public bool InHideMode => panelState == PanelState.HIDE_START || panelState == PanelState.HIDE_OVER;

        // 面板关闭的时间 
        public float hideTime { get; set; }
        public PanelParam panelParam;

        private bool _isAssetReady;
        private bool _isDataReady;
        private bool _isInit = false;
        private bool _isInitOnly = false;

        private bool _isReShow;
        private bool _isRecovery;

        // 打开完成 关闭完成 
        private Action _showComplete;

        private Action _hideComplete;
        private Action _delayComplete;

        // 面板结束，就会调用这个
        // 可能是正常结束，可能是直接被新的打开，都要检查是否关掉它了，也得关闭
        private Action _panelFinish;

        private List<string> _pkgNames;

        private List<Transition> _inAnimateList;
        private List<Transition> _outAnimateList;
        private Transition _inAnimate;
        private Transition _outAnimate;

        protected List<string> _childHasInAnimation;

        private List<PanelPartBase> _parts;

        protected PanelBg _panelBg;

        private bool _retainPopRecordMark = false;

        public PanelBase()
        {
            ALL_PANEL_INSTANCE_COUNT += 1;
            InitConfig();

            // 设置默认状态是已关闭状态
            panelState = PanelState.HIDE_OVER;
        }

        ~PanelBase()
        {
            ALL_PANEL_INSTANCE_COUNT -= 1;
            _gcPanelNameList.Remove(GetType().Name);
        }

        /// <summary>
        /// 初始化缓存界面
        /// </summary>
        public void InitCreate()
        {
            InitLoadAsset();
        }

        /// <summary>
        /// 子界面初始化需要缓存的,可以在这里预缓存
        /// </summary>
        protected virtual void OnInitCreate()
        {
        }

        public void Show(PanelParam param)
        {
            // 先要保证上次的已经关闭
            CheckFinishActions();

            if (_inAnimateList == null)
            {
                _inAnimateList = new List<Transition>();
            }
            else
            {
                ClearAnimate();
            }

            if (null == _outAnimateList)
            {
                _outAnimateList = new List<Transition>();
            }

            _outAnimate?.Stop();
            panelState = PanelState.SHOW_START;
            panelParam = param;
            hideTime = 0;
            if (null == config)
            {
                if (LogUtil.IsEnabled) LogUtil.Log("Panel:Show PanelConfig未配置", LogUtil.LogEnum.BASE_INFO);
                return;
            }

            _isReShow = false;
            // 每次从show 进入，都会执行协议请求方法
            _isDataReady = false;
            BeforeLoadAssets();
            LoadAsset();
            Request();
        }

        public void ReShow(PanelParam param)
        {
            _outAnimate?.Stop();
            // _inAnimate?.Play();
            // 先要保证上次的已经关闭
            CheckFinishActions();
            _isReShow = !InHideMode;
            panelState = PanelState.SHOW_START;
            panelParam = param;
            Request();
            OnReshow();
            panelState = PanelState.SHOW_OVER;
        }

        protected virtual void OnReshow()
        {
        }

        private void ClearTimeEvent()
        {
            if (_timeDelays.Count <= 0) return;
            foreach (var timeDelay in _timeDelays)
            {
                timeDelay.Clear();
            }

            _timeDelays.Clear();
        }

        public void Hide()
        {
            ClearInAnimate();
            RemoveShowCompleteCall();
            _inAnimate?.Stop();
            _inAnimate = null;
            // 不可重复关闭
            if (InHideMode) return;
            panelState = PanelState.HIDE_START;
            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_CLOSE_START, this);
            DoHide();
            MaintainPopRecord();
        }

        /// <summary>
        /// 标记保留这个Pop的历史，切换场景时保留
        /// </summary>
        public void MarkRetainPopRecord() => _retainPopRecordMark = true;

        private void MaintainPopRecord()
        {
            //移除Pop历史
            if (config.style == PanelStyleEnum.FIX_SIZE)
            {
                if (!_retainPopRecordMark)
                    HistoryManager.Instance.Remove(this);
                else
                    _retainPopRecordMark = false;
            }
        }

        private void ClearInAnimate()
        {
            if (_inAnimateList == null) return;
            // Stop掉包括children、bg等的In
            var list = new List<Transition>();
            list.AddRange(_inAnimateList);
            if (list.Count <= 0) return;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                list[i]?.Stop();
            }

            list.Clear();
        }

        public void Reconnect()
        {
            //recovery时要确保PanelState是ShowOver状态，否则引导会中断
            panelState = PanelState.SHOW_OVER;
            OnReconnect();
        }

        public virtual void OnReconnect()
        {
        }

        public void Recovery(PanelParam param)
        {
            _isRecovery = true;
            Show(param);
        }

        /// <summary>
        /// 默认执行OnShow
        /// </summary>
        public virtual void OnRecovery()
        {
            OnShow();
        }

        public virtual void HideImmediately()
        {
            DisposeProtectBtn();
            ClearInAnimate();
            ClearTimeEvent();
            ClearInAnimate();
            _inAnimate?.Stop();
            _inAnimate = null;
            _panelBg?.HideImmediately();
            // 不可重复关闭
            if (InHideMode) return;
            _isReShow = false;
            panelState = PanelState.HIDE_START;
            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_CLOSE_START, this);
            EventManager.Instance.RemoveEvent(ClientEvent.SCENE_LOADING_BE_CLOSED, TryPlayInAnimation);
            try
            {
                OnHide();
                HidePart();
            }
            catch
            {
                //FIXME 堆栈收集 
            }

            HideComplete();
            MaintainPopRecord();
        }

        private void HidePart()
        {
            if (_parts != null)
            {
                foreach (var partView in _parts)
                {
                    partView.CallPartOnHide();
                }
            }
        }

        public void HideOffline()
        {
            if (null == skinRoot) return;
            FrameworkManager.Instance.panelMan.RemoveFromStage(this);
            _isDataReady = false;
            panelState = PanelState.HIDE_OVER;
            MaintainPopRecord();
        }

        public override void Destroy()
        {
            base.Destroy();
            OnDestroy();
            DestroyPart();
            Timers.inst.Remove(NextFrame);
            if (null != _panelBg)
            {
                _panelBg.Dispose();
                _panelBgPool.Add(_panelBg);
            }

            skinRoot?.Dispose();
            skinRoot = null;
            _isAssetReady = false;
            _childHasInAnimation = null;
            _gcPanelNameList?.Add(GetType().Name);
            FrameworkManager.Instance.uiMan.UnloadUIList(_pkgNames);
            LoadManager.Instance.ClearRes(config.panel);
            SystemUtils.LightGC();
        }

        private void DestroyPart()
        {
            if (_parts != null)
            {
                foreach (var partView in _parts)
                {
                    partView.Destroy();
                }

                _parts.Clear();
                _parts = null;
            }
        }

        public void PushShowComplete(Action onComplete)
        {
            _showComplete = onComplete;
        }

        public void PushHideComplete(Action onComplete)
        {
            _hideComplete -= onComplete;
            _hideComplete += onComplete;
        }

        public override void Wait(Action onFinish)
        {
            _panelFinish += onFinish;
        }

        public void SetVisible(bool isShow)
        {
            if (skinRoot == null) return;
            var needTrigger = !skinRoot.visible && isShow;
            if (null != skinRoot)
            {
                skinRoot.visible = isShow;
            }

            if (needTrigger)
            {
                EventManager.Instance.TriggerEvent(ClientEvent.PANEL_OPEN_COMPLETE, config.panel);
            }

            _panelBg?.SetVisible(isShow);
            OnVisibleChanged(isShow);
            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_VISIBLE_CHANGED, config.panel);
        }

        /// <summary>
        /// 判断去更新面板的背景
        /// </summary>
        public void AddPanelBg()
        {
            UpdatePanelBg(config.bgUrl);
        }

        protected virtual void InitConfig()
        {
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnReset()
        {
        }

        // 在载入资源前的操作
        protected virtual void BeforeLoadAssets()
        {
        }

        protected virtual void OnRequestData()
        {
            OnRequestDataEnd(false);
        }

        protected virtual void OnRequestDataEnd(bool needClose)
        {
            if (needClose)
            {
                isWaitForData = true;
                return;
            }

            isWaitForData = false;
            _isDataReady = true;
            CheckAndShow();
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void ShowAnimation()
        {
            if (null == _inAnimateList) return;
            _inAnimateList.Clear();
            // 播放board动画
            var bg = (GComponent) skinRoot.GetChild("bg");
            if (null != bg)
            {
                var bgTransition = bg.GetTransition("In");
                bgTransition?.Play();
                if (bgTransition != null)
                {
                    _inAnimateList.Add(bgTransition);
                }
            }

            var bottom = (GComponent) skinRoot.GetChild("bottom_btns");
            if (null != bottom)
            {
                var t = bottom.GetTransition("In");
                t?.Play();
                _inAnimateList.Add(t);
            }

            if (_childHasInAnimation != null)
            {
                _childHasInAnimation.ForEach(childName =>
                {
                    var childComponent = (GComponent) skinRoot.GetChild(childName);
                    if (childComponent != null)
                    {
                        var childT = childComponent.GetTransition("In");
                        if (childT != null)
                        {
                            childT.Play();
                            _inAnimateList.Add(childT);
                        }
                    }
                });
            }

            var transition = skinRoot.GetTransition("In");
            if (config.isBgTween)
            {
                var time = transition?.totalDuration * 0.6f ?? 0.2f;
                _panelBg?.Show(time);
            }
            else
            {
                _panelBg?.Show(-1);
            }


            if (transition == null)
            {
                ShowComplete();
                return;
            }

            if (AudioManager.Instance.HasSound($"{config.panel}_In"))
            {
                transition.SetHook("PlayMusicIn", OnPlayMusicIn);
            }

            _inAnimate = transition;
            _inAnimateList.Add(_inAnimate);


            if (SceneLoadPanel.Instance.isShowing)
            {
                EventManager.Instance.AddEvent(ClientEvent.SCENE_LOADING_BE_CLOSED, TryPlayInAnimation);
            }
            else
            {
                EnsureShowCompleteCall();
                _inAnimate.Play(ShowComplete);
            }
        }

        private void TryPlayInAnimation()
        {
            EventManager.Instance.RemoveEvent(ClientEvent.SCENE_LOADING_BE_CLOSED, TryPlayInAnimation);
            if (_inAnimate != null)
            {
                EnsureShowCompleteCall();
                _inAnimate.Play(ShowComplete);
            }
        }

        protected void EnsureShowCompleteCall(Transition animation = null)
        {
            float costTime;
            animation = animation != null ? animation : _inAnimate;
            if (animation != null)
            {
                costTime = (animation.totalDuration + 0.1f) * 1.5f;
                Timers.inst.Add(costTime, 1, CallShowComplete);
            }
        }

        private void CallShowComplete(object obj)
        {
            ShowComplete();
        }

        private void RemoveShowCompleteCall()
        {
            Timers.inst.Remove(CallShowComplete);
        }

        protected void ShowComplete()
        {
            RemoveShowCompleteCall();
            // 如果是有合批的话， complete之后需要再合批处理一次
            if (skinRoot.fairyBatching)
            {
                skinRoot.InvalidateBatchingState();
            }

            _panelBg?.Show(-1);
            _showComplete?.Invoke();
            _showComplete = null;
            panelState = PanelState.SHOW_OVER;
            skinRoot.InvalidateBatchingState();
            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_OPEN_COMPLETE, config.panel);
            if (config.style == PanelStyleEnum.FULL_SCREEN)
            {
                Timers.inst.CallLater(NextFrame);
            }
            else
            {
                skinRoot.touchable = true;
                OnShowComplete();
                OnPartShowComplete();
            }
        }

        private void OnPartShowComplete()
        {
            if (_parts == null) return;
            foreach (var part in _parts)
            {
                part.OnShowComplete();
            }
        }

        protected virtual void OnShowComplete()
        {
        }

        protected virtual void OnHideComplete()
        {
        }

        protected override void OnHide()
        {
            base.OnHide();
            StopUISound();
        }

        protected virtual void HideAnimation()
        {
            if (_outAnimateList == null) return;
            _outAnimateList.Clear();
            _inAnimate?.Stop();
            _inAnimate = null;
            // 播放board动画
            var bg = (GComponent) skinRoot.GetChild("bg");
            if (null != bg)
            {
                var bgTransition = bg.GetTransition("Out");
                bgTransition?.Play();
                _outAnimateList.Add(bgTransition);
            }

            var bottom = (GComponent) skinRoot.GetChild("bottom_btns");
            if (null != bottom)
            {
                var t = bottom.GetTransition("Out");
                t?.Play();
                _outAnimateList.Add(t);
            }

            var transition = skinRoot.GetTransition("Out");
            if (config.isBgTween)
            {
                var time = transition?.totalDuration * 0.6f ?? 0.2f;
                _panelBg?.Hide(time);
            }

            if (transition == null)
            {
                HideComplete();
                return;
            }

            _outAnimate = transition;
            _outAnimateList.Add(_outAnimate);
            _outAnimate.Play(HideComplete);
            //加一层保护避免out异常
            var costTime = (_outAnimate.totalDuration + 0.1f) * 1.5f;
            Timers.inst.Remove(HideComplete);
            Timers.inst.Add(costTime, 1, HideComplete);
        }

        private void HideComplete(object o)
        {
            var costTime = (_outAnimate.totalDuration + 0.1f) * 1.5f;
            // LogUtil.LogError($"{config.panel}   Out播放异常", LogUtil.LogEnum.ALL);
            HideComplete();
            _outAnimate.Stop();
        }

        protected void HideComplete()
        {
            Timers.inst.Remove(HideComplete);
            DisposeProtectBtn();
            ClearTimeEvent();
            if (default == config.layer)
            {
                skinRoot?.RemoveFromParent();
            }
            else
            {
                FrameworkManager.Instance.panelMan.RemoveFromStage(this);
            }

            _panelBg?.Hide(-1);
            _isDataReady = false;
            panelState = PanelState.HIDE_OVER;
            _hideComplete?.Invoke();
            _hideComplete = null;
            if (skinRoot != null)
            {
                skinRoot.visible = false;
                skinRoot.touchable = false;
            }

            CheckFinishActions();
            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_CLOSE_COMPLETE, config);
            OnHideComplete();
        }

        protected void UpdatePanelBg(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (null == _panelBg)
            {
                if (_panelBgPool.Count <= 0)
                {
                    _panelBg = new PanelBg();
                }
                else
                {
                    _panelBg = _panelBgPool[_panelBgPool.Count - 1];
                    _panelBg.Unload();
                    _panelBgPool.Remove(_panelBg);
                }

                _panelBg.key = config.panel;
                GRoot.inst.AddChild(_panelBg.Loader);
            }

            _panelBg.SetVisible(true);
            _panelBg.Load(url, config.panel);
        }

        protected virtual void OnDestroy()
        {
            StopUISound();
        }

        protected virtual void OnVisibleChanged(bool isShow)
        {
        }

        private void LoadAsset()
        {
            _pkgNames = new List<string> {config.pkgName};
            if (config.depends?.Length > 0)
            {
                _pkgNames.AddRange(config.depends);
            }

            FrameworkManager.Instance.uiMan.LoadUIList(_pkgNames, OnLoadComplete);
        }

        private void InitLoadAsset()
        {
            _pkgNames = new List<string> {config.pkgName};
            if (config.depends?.Length > 0) _pkgNames.AddRange(config.depends);
            FrameworkManager.Instance.uiMan.LoadUIList(_pkgNames, OnInitLoadComplete);
        }

        private void OnInitLoadComplete()
        {
            _isAssetReady = true;
            if (!_isInit)
            {
                _isInit = true;
                _isInitOnly = true;
                InitOnly();
            }
            else
            {
                Reset();
            }

            OnInitCreate();
        }

        private void OnLoadComplete()
        {
            _isAssetReady = true;
            if (!_isInit)
            {
                _isInit = true;
                Init();
            }
            else
            {
                Reset();
            }

            if (_isInitOnly)
            {
                OnInit();
                InitPartView();
                _isInitOnly = false;
            }

            CheckAndShow();
        }

        private void Init()
        {
            if (skinRoot != null) return;
            skinRoot = UIPackage.CreateObject(config.pkgName, config.resName).asCom;
            if (null == skinRoot)
            {
                Debug.Log($"Panel 创建面板组件失败：{config.pkgName} {config.panel}");
                return;
            }

            skinRoot.fairyBatching = true;
            config.panel = GetType().Name;
            skinRoot.name = config.panel;
            skinRoot.displayObject.name = config.panel;
            if (skinRoot is GComponent component)
            {
                RegisterCompEvents(component);
            }

            OnInit();
            InitPart();
        }

        private void InitOnly()
        {
            if (skinRoot != null) return;
            skinRoot = UIPackage.CreateObject(config.pkgName, config.resName).asCom;
            if (null == skinRoot)
            {
                Debug.Log($"Panel 创建面板组件失败：{config.pkgName} {config.panel}");
                return;
            }

            skinRoot.fairyBatching = true;
            config.panel = GetType().Name;
            skinRoot.name = config.panel;
            skinRoot.displayObject.name = config.panel;
            if (skinRoot is GComponent component)
            {
                RegisterCompEvents(component);
            }

            InitPartOnly();
        }

        /// <summary>
        /// 初始化组件实例
        /// </summary>
        private void InitPart()
        {
            if (_parts != null) return;
            if (config.panelPart != null)
            {
                _parts = new List<PanelPartBase>();
                foreach (var partType in config.panelPart)
                {
                    var partView = (PanelPartBase) Activator.CreateInstance(partType);
                    partView.Init(this);
                    partView.OnInit();
                    _parts.Add(partView);
                }
            }
        }

        private void InitPartView()
        {
            if (_parts == null) return;
            foreach (var part in _parts)
            {
                part.OnInit();
            }
        }

        private void InitPartOnly()
        {
            if (_parts != null) return;
            if (config.panelPart != null)
            {
                _parts = new List<PanelPartBase>();
                foreach (var partType in config.panelPart)
                {
                    var partView = (PanelPartBase) Activator.CreateInstance(partType);
                    partView.Init(this);
                    partView.InitCreate();
                    _parts.Add(partView);
                }
            }
        }

        private void Reset()
        {
            OnReset();
        }

        private void ResetPart()
        {
            if (_parts != null)
            {
                foreach (var partView in _parts)
                {
                    partView.OnReset();
                }
            }
        }

        private void CheckAndShow()
        {
            if (_isAssetReady && _isDataReady)
            {
                DoShow();
            }
        }

        private void Request()
        {
            isWaitForData = true;
            OnRequestData();
        }

        private void DoShow()
        {
            skinRoot.visible = true;
            skinRoot.touchable = false;
            if (config.layer != default)
            {
                FrameworkManager.Instance.panelMan.AddToStage(this);
            }

            EventManager.Instance.TriggerEvent(ClientEvent.PANEL_OPEN_START, this);
            // if (config.style == PanelStyleEnum.FULL_SCREEN && config.isHideScene)
            // {
            //     FrameworkManager.Instance.sceneMan.ChangeToEmpty(panelParam, GetType());
            // }

            if (!_isReShow && config.style == PanelStyleEnum.FULL_SCREEN && config.layer == UILayerEnum.PANEL)
            {
                skinRoot.touchable = false;
            }
            else
            {
                skinRoot.touchable = true;
            }

            EventManager.Instance.AddEvent(ClientEvent.WITHNETDATA_PANEL_RESHOW, NetShow);

#if UNITY_EDITOR
            if (!_isRecovery)
            {
                OnShow();
                OnPartShow();
            }
            else
            {
                OnRecovery();
            }
#else
            try
            {
                if (!_isRecovery)
                {
                    OnShow();
                    OnPartShow();
                }
                else
                {
                    OnRecovery();
                }
            }
            catch
            {
            }
#endif
            _isRecovery = false;

            if (!_isReShow)
            {
                ShowAnimation();
            }

            Sdk.LogInfo($"Show Panel <{GetType().Name}>");
        }

        private void NetShow()
        {
#if UNITY_EDITOR
            OnNetUpdateShow();
#else
            try
            {
               OnNetUpdateShow();
            }
            catch
            {
            }
#endif
        }

        protected virtual void OnNetUpdateShow()
        {
        }


        private void OnPartShow()
        {
            if (_parts != null)
            {
                foreach (var partView in _parts)
                {
                    partView.OnShow();
                }
            }
        }

        private void DoHide()
        {
            _isReShow = false;
            EventManager.Instance.RemoveEvent(ClientEvent.SCENE_LOADING_BE_CLOSED, TryPlayInAnimation);
            EventManager.Instance.RemoveEvent(ClientEvent.WITHNETDATA_PANEL_RESHOW, NetShow);
            try
            {
                OnHide();
                HidePart();
            }
            catch
            {
                //FIXME 堆栈收集
            }

            HideAnimation();
            Timers.inst.Remove(NextFrame);

            Sdk.LogInfo($"Hide Panel <{GetType().Name}>");
        }

        protected virtual void GoHome()
        {
            if (TeamModule.Instance.HasTeam)
            {
                var param = new CommonPaperCostBuyParam()
                {
                    msg = LangUtils.Str(10003963), confirmStr = LangUtils.Str(10001003),
                    hideCallback = OnLeaveConfirmCb
                };
                PanelManager.Instance.ShowPanel(param);
            }
            else
            {
                SureGoHome();
            }
        }

        private void OnLeaveConfirmCb(CommonPanelHideEnum commonPanelHideEnum)
        {
            if (commonPanelHideEnum != CommonPanelHideEnum.YES) return;
            TeamModule.Instance.OnTeamLeaveReq(SureGoHome);
        }

        private void SureGoHome()
        {
            //重置 商店页签跳转、bannerPop重新打开flag
            EventManager.Instance.TriggerEvent(ClientEvent.RESET_FLAG);
            //如果处于幻境中，返回幻境主页
            if (FairyLandModule.Instance.IsFairyLandScene)
            {
                FairyLandModule.Instance.EnterFairyLandScene();
            }
            else
            {
                //返回宝箱主页
                FrameworkManager.Instance.sceneMan.SwitchScene(StaticData.game.mainConsole == 1 ? GameConstant.SUB_SCENE_GOFIELD : GameConstant.SUB_SCENE_TREASURE_BOX, clearRecord: true);
            }
        }

        /// <summary>
        /// 如果panelBase的inBackList = true -> back, else -> hide;
        /// </summary>
        protected virtual void GoReturn()
        {
            if (!config.inBackList)
            {
                PanelManager.Instance.Back();
                return;
            }

            BackManager.Instance.Back();
        }

        protected virtual void OnRulePanelOpen()
        {
            RuleManager.Instance.ShowRulePanel(config.panel);
        }

        protected virtual void OnShareClick()
        {
            // var cfg = ShareHelper.GetShareCfgByPanelName(config.panel);
            // if (cfg == null) return;
            // ShareHelper.Share(cfg.id);
        }


        private void ClearAnimate()
        {
            var list = new List<Transition>();
            list.AddRange(_inAnimateList);
            if (null != _outAnimateList && _outAnimateList.Count > 0)
            {
                list.AddRange(_outAnimateList);
            }

            if (list.Count <= 0) return;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                list[i]?.Stop();
            }

            list.Clear();
        }

        private void RegisterCompEvents(GComponent comp)
        {
            if (comp == null)
            {
                return;
            }

            var children = comp.GetChildren();
            for (var i = children.Length - 1; i >= 0; i--)
            {
                var item = children[i];
                if (item.data == null)
                {
                    if (item is GComponent component)
                    {
                        RegisterCompEvents(component);
                    }

                    continue;
                }

                var infos = item.data.ToString().Split('|');
                var action = infos[0];
                switch (action)
                {
                    case GameConstant.UI_EVENT_CLOSE:
                        item.onClick.Add(Hide);
                        break;
                    case GameConstant.UI_EVENT_HOME:
                        item.onClick.Add(GoHome);
                        break;
                    case GameConstant.UI_EVENT_RETURN:
                    case GameConstant.UI_EVENT_RETURN_SCENE:
                    case GameConstant.UI_EVENT_RETURN_PANEL:
                        item.onClick.Add(GoReturn);
                        break;
                    case GameConstant.UI_EVENT_RULE:
                        item.onClick.Add(OnRulePanelOpen);
                        break;
                    case GameConstant.UI_EVENT_SHARE:
                        item.onClick.Add(OnShareClick);
                        break;
                    case GameConstant.UI_EVENT_CLICK:
                        // click 事件尽量自己手动添加，尽量少用匿名方法
                        var method =
                            $"On{StringUtils.UpperCamelCase(item.name)}{StringUtils.UpperFirstChar(action)}";
                        var binder = GetType().GetMethod(method,
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        item.onClick.Add(() =>
                        {
                            if (binder != null) binder.Invoke(this, null);
                        });
                        break;
                    default:
                        break;
                }

                // 这里再加个声音点击的支持
                string clickSound = null;
                switch (action)
                {
                    case GameConstant.UI_EVENT_CLOSE:
                    case GameConstant.UI_EVENT_HOME:
                    case GameConstant.UI_EVENT_RETURN:
                    case GameConstant.UI_EVENT_RETURN_SCENE:
                    case GameConstant.UI_EVENT_RETURN_PANEL:
                    case GameConstant.UI_EVENT_RULE:
                    case GameConstant.UI_EVENT_CLICK:
                        clickSound = "t1";
                        break;
                }

                if (infos.Length > 1)
                {
                    clickSound = infos[1];
                }

                // if (null != clickSound)
                // {
                //     item.onClick.Add(() =>
                //     {
                //         AudioManager.Instance.Play(clickSound);
                //     });
                // }
            }
        }

        /// <summary>
        /// 这个是给关闭队列使用的
        /// </summary>
        private void CheckFinishActions()
        {
            var finishHandler = _panelFinish;
            _panelFinish = null;
            finishHandler?.Invoke();
        }


        private void NextFrame(object param)
        {
            skinRoot.touchable = true;
            OnShowComplete();
            OnPartShowComplete();
        }


        private void OnPlayMusicIn()
        {
            PlayUISound($"{config.panel}_In");
        }

        public void PlayUISound(string configId)
        {
            AudioManager.Instance.PlayUISound(configId, this.GetHashCode());
        }

        public void PlayUISoundByEvent(string eventId)
        {
            AudioManager.Instance.PlayUISoundByEvent(eventId, this.GetHashCode());
        }

        private void StopUISound()
        {
            AudioManager.Instance.StopByHashCode(this.GetHashCode());
        }

        /// <summary>
        ///  TODO 扩展：链式调用和组件抽离
        /// </summary>
        /// <param name="time"></param>
        /// <param name="call"></param> 
        /// <param name="repeat"></param>
        protected DelayTool SetDelayTime(float time, int repeat = 1)
        {
            var tool = new DelayTool();
            _timeDelays.Add(tool);
            return tool.SetDelayTime(time, repeat);
        }

        protected void Remove(DelayTool tool)
        {
            if (!_timeDelays.Contains(tool)) return;
            tool.Clear();
            _timeDelays.Remove(tool);
        }
    }


    //单线程即执行完一个才去执行另外一个
    public class DelayTool
    {
        private Dictionary<int, int> _repeatDic = new Dictionary<int, int>();
        private Dictionary<int, float> _timeDic = new Dictionary<int, float>();
        private Dictionary<int, TimerCallback> _callDic = new Dictionary<int, TimerCallback>();
        private Dictionary<int, object> _callbackParamDic = new Dictionary<int, object>();
        private List<TimerCallback> _delegates = new List<TimerCallback>();
        TimersEngine _engine = GameObject.Find("[FairyGUI.Timers]").AddComponent<TimersEngine>();
        private TimerCallback _callback;
        private Coroutine _curCoroutine = null;
        private bool _isStart = false;
        private int _index = 1;
        private float _time = 1f;
        private int _repeat = 1;

        public DelayTool SetDelayTime(float time, int repeat)
        {
            _time = time;
            _repeat = repeat;
            return this;
        }


        //并发执行
        public DelayTool MultithreadDelay(Action call)
        {
            return MultithreadDelay(call, _time, _repeat);
        }

        //并发执行
        public DelayTool MultithreadDelay(Action call, float time, int repeat, object callbackParam = null)
        {
            void TimerCallback(object o)
            {
                try
                {
                    call.Invoke();
                    _delegates.Remove(TimerCallback);
                }
                catch
                {
                    //FIXME 堆栈收集
                }
            }

            _delegates.Add(TimerCallback);
            Timers.inst.Add(time, repeat, TimerCallback, callbackParam);
            return this;
        }


        private void ClearMultithread()
        {
            if (_delegates.Count <= 0) return;
            foreach (var fun in _delegates)
            {
                if (fun is TimerCallback call)
                {
                    Timers.inst.Remove(call);
                }
                else
                {
                    throw new Exception("_timeList is add other thing");
                }
            }

            _delegates.Clear();
        }


        //链式执行即执行完一个才去执行另外一个
        public DelayTool SingleDelay(Action call)
        {
            return SingleDelay(call, _time, _repeat);
        }


        //链式执行
        public DelayTool SingleDelay(Action call, float time = 1, int repeat = 1,
            object callbackParam = null)
        {
            if (call == null) return this;
            _repeatDic.Add(_index, repeat);
            _timeDic.Add(_index, time);
            _callbackParamDic.Add(_index, callbackParam);

            void TimerCallback(object o)
            {
                try
                {
                    call.Invoke();
                }
                catch
                {
                    Debug.LogError("==================================================");
                    //FIXME 堆栈收集
                }
            }

            _callDic.Add(_index, TimerCallback);
            ++_index;
            if (!_isStart) _curCoroutine = _engine.StartCoroutine(Start());

            return this;
        }

        private void ClearSingleThread()
        {
            if (_callback == null) return;
            if (_curCoroutine == null) return;
            _engine.StopCoroutine(_curCoroutine);
            Timers.inst.Remove(_callback);
            _callDic.Clear();
            _repeatDic.Clear();
            _timeDic.Clear();
            _callbackParamDic.Clear();
            _callDic = null;
            _repeatDic = null;
            _timeDic = null;
            _callbackParamDic = null;
        }

        private IEnumerator Start()
        {
            _isStart = true;
            while (_callDic.Count > 0)
            {
                Debug.Log($"========{_callDic.First().Key}========");
                _callback = _callDic.First().Value;
                Timers.inst.Add(_timeDic.First().Key, _repeatDic.First().Value, _callback,
                    _callbackParamDic.First().Value);
                yield return new WaitForSeconds(_repeatDic.First().Value * _timeDic.First().Key);
                _timeDic?.Remove(_timeDic.First().Key);
                _repeatDic?.Remove(_repeatDic.First().Key);
                _callDic?.Remove(_callDic.First().Key);
                _callbackParamDic?.Remove(_callbackParamDic.First().Key);
            }

            _isStart = false;

            yield return null;
        }

        public void Clear()
        {
            ClearMultithread();
            ClearSingleThread();
        }

        public void Remove(Action call)
        {
            ///  TODO 待补充 @jiangkun
        }
    }
    // if (CloseManager.Instance.NeedClose(FuncBtnEnum.Bag)) return;
    // PanelManager.Instance.ShowPanel<BagPanel>();

    // //延迟5秒调用方法
    // SetTimeout(5f).Call(AAA);
    // //每5秒调用一次方法，调用5次
    // SetInverval(5f, 5).Call(AAA);
    // //发协议，返回后，播放特效，播放完成后，刷新UI
    // SendReq(xxx).PlayEffect(EFF, 1).Call(AAA);

    //VO => view object
    //DTO => date T object

    //StepVO
    //status : pending（进行中） | resolve（已完成） | reject（失败） | empty(未开始)
    //interrupt / Destroy
    //callback

    //TimeoutStepVO:StepVO
    //time 5f

    //CallbackVO:StepVO
    //callback
    //param

    //IntervalStepVO:StepVO
    //interval
    //count

    //1 组件化
    //2 可自销毁
    //3 可扩展
    //4 可时序化

    //A.B.C.D
    //A=>B=>C=>D

    //On(UIEvent.ASDB).Timeout().Call()
    //On(UIEvent.ASDB).OverrideTimeout().Call()
}