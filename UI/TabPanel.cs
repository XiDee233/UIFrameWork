using System;
using System.Collections.Generic;
using System.Linq;
using YatBun.Framework.TabSub;
using YatBun.Game.Module.GiftMall.Model;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class TabPanelInfo
    {
        public int panelId;
        public string title;
        public int avatarId;
        public string key;
        public string paneBg;
        public Type pane;
        public string tabImg;
        public string tabSelectImg;
        public PanelParam param;
        public int funcId;
        public int tabSort;
        public int activityId;
        public GiftShopPanelType giftShopType;
    }

    public class TabPanel : PanelBase
    {
        private static int _recordVersion;

        protected Dictionary<string, Type> _paneConfig = new Dictionary<string, Type>();
        protected Dictionary<string, PanelParam> _paramConfig = new Dictionary<string, PanelParam>();
        protected Dictionary<string, int> _paneKeyMap = new Dictionary<string, int>();
        protected float _paneDelayTime; // 第一次子面板显示延迟的时间
        protected string _switchPaneKey;
        protected bool _ctrlInited;
        protected int _paneVersion;
        protected string _currentPaneKey;

        protected Controller Controller { get; set; }
        protected GList CtrlList { get; set; }

        protected Dictionary<string, PanelBase> _paneMap;
        private int _switchDelayPaneId;
        private bool _hasSelfCreatePanel = false;
        private List<string> _destroyKeys = new List<string>();

        public PanelBase openingPanel
        {
            get
            {
                if (_paneMap != null &&
                    (!String.IsNullOrEmpty(_currentPaneKey)) &&
                    _paneMap.ContainsKey(_currentPaneKey))
                {
                    _paneMap.TryGetValue(_currentPaneKey, out var returnValue);
                    return returnValue;
                }

                return null;
            }
        }

        protected override void OnShow()
        {
            _InitControllers();
            RefreshController();

            var defaultKey = _switchPaneKey ?? _paneConfig.Keys.FirstOrDefault();
            SwitchTo(defaultKey, _paneDelayTime);
            if (null != _paneKeyMap && null != defaultKey)
            {
                // var index = _paneKeyMap[defaultKey];
                var index = Array.IndexOf(_paneKeyMap.Keys.ToArray(), defaultKey);
                if (null != CtrlList)
                {
                    CtrlList.numItems = _paneKeyMap.Keys.Count;
                    CtrlList.selectedIndex = index;
                    CtrlList.touchable = true;

                    //[DeBug]显示完后将Tab列表中选中item移至画面中
                    CtrlList.ScrollToView(CtrlList.selectedIndex);
                }

                Controller?.SetSelectedIndex(index);
            }
        }

        protected override void OnHide()
        {
            base.OnHide();

            _ClearDelayPane();

            if (_currentPaneKey != null && _paneMap.TryGetValue(_currentPaneKey, out var currentPane))
            {
                currentPane?.Hide();
                _switchPaneKey = _currentPaneKey;
                _currentPaneKey = null;
            }
        }

        protected override void OnVisibleChanged(bool isShow)
        {
            base.OnVisibleChanged(isShow);

            if (_currentPaneKey != null && _paneMap.TryGetValue(_currentPaneKey, out var currentPane))
            {
                if (currentPane is TabSubPane pane)
                {
                    pane.OnSwitch(isShow);
                }
            }
        }

        protected virtual void RefreshTabList()
        {
        }

        /// <summary>
        /// 单独移除某一个key让其不要显示，
        /// 这个移除的逻辑需要在RefreshController中去做
        /// </summary>
        /// <param name="paneKey"></param>
        /// <returns></returns>
        public bool RemovePaneKey(string paneKey)
        {
            return _paneKeyMap.Remove(paneKey);
        }

        /// <summary>
        /// 在页面打开的时候关闭某个Tab
        /// </summary>
        /// <param name="paneKey"></param>
        public void CloseTab(string paneKey)
        {
            if (RemovePaneKey(paneKey))
            {
                // 如果都被关掉了，那么整个界面关闭，这里先不关闭整个页面
                if (_paneKeyMap.Keys.Count <= 0)
                {
                    //Hide();
                    //return;
                }

                if (null != CtrlList)
                {
                    CtrlList.numItems = _paneKeyMap.Keys.Count;
                }

                // 如果当前打开的是这个界面， 那么显示一个默认的界面
                if (paneKey == _currentPaneKey)
                {
                    // 这里先注释掉， 只需要tab按钮消失， 界面不消失
                    // var defaultKey = _paneConfig.Keys.FirstOrDefault();
                    // SwitchTo(defaultKey);
                }
                else
                {
                    // 否则就是重新选中这个界面
                    if (null != CtrlList)
                    {
                        var index = Array.IndexOf(_paneKeyMap.Keys.ToArray(), _currentPaneKey);
                        CtrlList.selectedIndex = index;
                    }
                }
            }
        }

        public void SwitchTo(string paneKey, float delay = 0)
        {
            if (null == _paneMap)
            {
                _paneMap = new Dictionary<string, PanelBase>();
            }

            if (!_paneMap.TryGetValue(paneKey, out var pane))
            {
                var paneType = _paneConfig[paneKey];
                if (paneType != null)
                {
                    pane = PanelManager.Instance.GetPanelByType(paneType);
                    // 如果存在于PanelManager 则面板被缓存，不用担心被销毁报错 否则使用原TabPanel逻辑
                    if (pane == null)
                    {
                        _paneMap[paneKey] = pane = (PanelBase) Activator.CreateInstance(paneType);
                        _hasSelfCreatePanel = true;
                    }
                    else _paneMap[paneKey] = pane;
                }
            }

            if (_currentPaneKey == paneKey) return;

            // 清掉tweener
            _ClearDelayPane();

            // 关掉之前的面板
            PanelBase currentPane = null;
            if (_currentPaneKey != null && _paneMap.TryGetValue(_currentPaneKey, out currentPane))
            {
                currentPane?.Hide();
            }

            // 然后打开新的
            if (null != pane)
            {
                var version = _paneVersion = ++_recordVersion;
                if (null != currentPane && currentPane.panelState != PanelState.HIDE_OVER)
                {
                    currentPane.PushHideComplete(() => { _ShowPane(paneKey, version); });
                }
                else
                {
                    _switchDelayPaneId = TimeTool.Instance.Delay(delay, () =>
                    {
                        _ShowPane(paneKey, version);
                        _switchDelayPaneId = 0;
                    });
                }
            }

            _currentPaneKey = paneKey;
        }

        protected virtual void RefreshController()
        {
        }

        protected virtual void OnSelect(int index)
        {
        }

        protected virtual void OnSelect(string paneKey)
        {
        }

        protected virtual void RenderTab(string paneKey, GObject itemSkin)
        {
        }

        private void _ShowPane(string paneKey, int version)
        {
            if (version != _paneVersion) return;
            if (!_paneMap.ContainsKey(paneKey)) return;
            var pane = _paneMap[paneKey];
            // 先去找tab_container做容器， 找不到的话， 就用主界面自己
            var container = (GComponent) skinRoot.GetChild("tab_container") ?? skinRoot;
            // 这里还要加个参数的支持
            _paramConfig.TryGetValue(paneKey, out var param);
            pane.Show(param);
            pane.skinRoot.SetSize(container.width, container.height);
            pane.skinRoot.AddRelation(container, RelationType.Size);
            container.AddChild(pane.skinRoot);

            // tab内的pane不应该存在layer， 如果不小心写了就报错
            if (default != pane.config.layer)
            {
                Debug.LogError(
                    $"[Tab Config Error] Error assign \"layer\" in Pane {pane.config.panel}. Leave it empty");
            }

            // 再执行两种回调的OnSelect
            OnSelect(paneKey);
            if (null != _paneKeyMap)
            {
                OnSelect(_paneKeyMap[paneKey]);
            }
        }

        private void _InitControllers()
        {
            if (!_ctrlInited)
            {
                if (CtrlList != null)
                {
                    CtrlList.itemRenderer = _InternalRenderTab;
                    CtrlList.onClickItem.Add(_OnClickItem);
                }

                Controller?.onChanged.Add(_OnCtrlChanged);
                _ctrlInited = true;
            }
        }

        private void _ClearDelayPane()
        {
            if (_switchDelayPaneId != 0)
            {
                TimeTool.Instance.RemoveTimeEvent(_switchDelayPaneId);
                _switchDelayPaneId = 0;
            }
        }

        protected virtual void _OnClickItem(EventContext context)
        {
            Timers.inst.Add(0.3f, 1, CtrlListTouchableShow);
            CtrlList.touchable = false;
            var index = CtrlList.GetChildIndex((GObject) context.data);
            if (index >= _paneKeyMap.Keys.ToList().Count) return;
            var paneKey = _paneKeyMap.Keys.ToList()[index];
            SwitchTo(paneKey);
        }

        private void CtrlListTouchableShow(object obj)
        {
            CtrlList.touchable = true;
        }

        private void _OnCtrlChanged()
        {
            var index = _paneKeyMap.Values.ToList().IndexOf(Controller.selectedIndex);
            if (index >= 0)
            {
                var paneKey = _paneKeyMap.Keys.ToArray()[index];
                SwitchTo(paneKey);
            }
        }

        private void _InternalRenderTab(int index, GObject itemSkin)
        {
            RenderTab(_paneKeyMap.Keys.ToArray()[index], itemSkin);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Timers.inst.Remove(CtrlListTouchableShow);
            foreach (var pair in _paneMap)
            {
                var panelBase = pair.Value;
                panelBase?.Destroy();
            }

            _paneMap.Clear();
            _ctrlInited = false;
        }

        public bool CanDestroySub()
        {
            return _paneMap != null && _paneMap.Count > 0 && _hasSelfCreatePanel;
        }

        public void DestroySubPanel()
        {
            if (_paneMap == null || _paneMap.Count <= 0) return;
            Timers.inst.Remove(CtrlListTouchableShow);
            _destroyKeys.Clear();
            foreach (var pair in _paneMap)
            {
                var panelBase = pair.Value;
                if (panelBase.config.manuallyCreatePak) continue;
                _destroyKeys.Add(pair.Key);
            }

            foreach (var key in _destroyKeys)
            {
                _paneMap[key].Destroy();
                _paneMap.Remove(key);
            }

            _hasSelfCreatePanel = false;
            _ctrlInited = false;
        }
    }
}