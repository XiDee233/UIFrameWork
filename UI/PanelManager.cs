using System;
using System.Collections.Generic;
using System.Linq;
using YatBun.Framework.Back;
using YatBun.Framework.Extentions;
using YatBun.Game.Core.Utils;
using YatBun.Game.Module.Basic.Tools;
using YatBun.Game.Module.Battle;
using YatBun.Game.Module.CommonUI;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    /// <summary>
    /// PanelManager 只处理panel面板相关的，UI上的通用效果及其他由UIManager处理
    /// </summary>
    public partial class PanelManager : Singleton<PanelManager>, IManager
    {
        // n秒窗口不可见销毁
        private static float PANEL_DISPOSE_TIME = 0f;

        // 每个几帧检测一次
        private static int PANEL_CHECK_DISPOSE_FRAME = 15;

        private readonly Dictionary<UILayerEnum, PanelLayer> _layerDict;

        // panel 初始化的实例，仅仅只是用来做是否初始化的逻辑判断
        private readonly Dictionary<Type, PanelBase> _panelDict;

        // 所有开启panel的先后顺序
        private readonly List<PanelBase> _panelList;

        // 上一次检测Panel释放的时间，一次检测，只会销毁一个Panel
        private float _lastCheckUninstallTime;

        public PanelManager()
        {
            _layerDict = new Dictionary<UILayerEnum, PanelLayer>();
            _panelDict = new Dictionary<Type, PanelBase>();
            _panelList = new List<PanelBase>();
        }

        public void OnInit()
        {
            // 设置UI的匹配模式
            GRoot.inst.ApplyContentScaleFactor();
            GRoot.inst.SetContentScaleFactor(GameConfig.UI_WIDTH, GameConfig.UI_HEIGHT,
                Stage.inst.width / Stage.inst.height > AppConfig.MAX_ASPECT_RATIO
                    ? UIContentScaler.ScreenMatchMode.MatchWidthOrHeight
                    : UIContentScaler.ScreenMatchMode.MatchWidth);

            AddBattleLayer(UILayerEnum.BATTLE_UI);
            AddLayer(UILayerEnum.PANEL);
            AddLayer(UILayerEnum.POP);
            AddLayer(UILayerEnum.GUIDE);
            AddLayer(UILayerEnum.TIPS);
            AddLayer(UILayerEnum.LOADING);
            AddLayer(UILayerEnum.ERROR);
        }

        public void OnEnterGame()
        {
            GameManager.Instance.AddUpdate(Update);
        }

        public void OnExitGame()
        {
            HideAllPanel();
            PopQueue.OnExitGame();
            FlyComManager.Instance.Stop();
            GameManager.Instance.RemoveUpdate(Update);
        }

        public void HidePanel<T>() where T : PanelBase
        {
            HidePanel(typeof(T), false);
        }

        public void HidePanel(PanelBase panel)
        {
            HidePanel(panel, false);
        }

        public void HidePanelImmediately<T>() where T : PanelBase
        {
            HidePanel(typeof(T), true);
        }

        public void HidePanelImmediately(PanelBase panel)
        {
            HidePanel(panel, true);
        }

        public PanelBase ShowPanel<T>(bool isShowLoading = false, bool isFromScene = false) where T : PanelBase
        {
            return ShowPanel(typeof(T), null, isShowLoading, isFromScene);
        }

        public PanelBase ShowPanel<T, U>(U val1, bool isShowLoading = false, bool isFromScene = false)
            where T : PanelBase
        {
            var param = new PanelParam1<U>
            {
                val1 = val1
            };
            return ShowPanel(typeof(T), param, isShowLoading, isFromScene);
        }

        public PanelBase ShowPanel<T, U, V>(U val1, V val2, bool isShowLoading = false, bool isFromScene = false)
            where T : PanelBase
        {
            var param = new PanelParam2<U, V>
            {
                val1 = val1,
                val2 = val2,
            };
            return ShowPanel(typeof(T), param, isShowLoading, isFromScene);
        }

        public PanelBase ShowPanel(PanelParam param, bool isShowLoading = false, bool isFromScene = false)
        {
            return null == param ? null : ShowPanel(param.panelType, param, isShowLoading, isFromScene);
        }

        public void ShowPanel(PanelBase panelBase, bool isShowLoading = false, bool isFromScene = false)
        {
            LoadAndShowPanel(panelBase, null, isShowLoading, isFromScene);
        }

        public PanelBase ShowPanel(Type panelType, bool isShowLoading, bool isFromScene = false)
        {
            return ShowPanel(panelType, null, isShowLoading, isFromScene);
        }

        public void AddToStage(PanelBase panelBase)
        {
            if (null == panelBase) return;
            if (!_layerDict.TryGetValue(panelBase.config.layer, out var layer)) return;
            layer.AddToStage(panelBase);
            _panelList.Remove(panelBase);
            _panelList.Add(panelBase);
        }

        public void RemoveFromStage(PanelBase panelBase)
        {
            if (null == panelBase) return;
            if (!_layerDict.TryGetValue(panelBase.config.layer, out var layer)) return;
            layer.RemoveFromStage(panelBase);
            _panelList.Remove(panelBase);
        }

        /// <summary>
        /// 通过Type获取Panel
        /// </summary>
        /// <param name="panelType"></param>
        /// <returns></returns>
        public T GetPanel<T>() where T : PanelBase
        {
            if (_panelDict.TryGetValue(typeof(T), out var panel))
            {
                return (T) panel;
            }

            return null;
        }

        public PanelBase GetPanelByType(Type type)
        {
            return _panelDict.TryGetValue(type, out var panel) ? panel : null;
        }

        // 获取最上层Panel
        public PanelBase GetTopPanel()
        {
            for (var i = UILayerEnum.MAX - 1; i > 0; i--)
            {
                if (!_layerDict.TryGetValue(i, out var layer)) continue;
                var top = layer.GetTopPanel();
                if (null != top) return top;
            }

            return null;
        }

        /// <summary>
        /// 引导期间获取引导层之下的最上层Panel，
        /// </summary>
        /// <returns></returns>
        public PanelBase GetTopPanelUnderGuide()
        {
            for (var i = UILayerEnum.GUIDE - 1; i > 0; i--)
            {
                if (!_layerDict.TryGetValue(i, out var layer)) continue;
                var top = layer.GetTopShowPanel();
                if (null != top) return top;
            }

            return null;
        }

        /// <summary>
        /// 判断是否存在某个显示中的面板
        /// </summary>
        /// <param name="type">typeof(T)</param>
        /// <returns></returns>
        public bool HasPanel(Type type)
        {
            foreach (var layer in _layerDict.Values)
            {
                var showPanel = layer.GetTopShowPanel();
                if (showPanel == null) continue;
                if (showPanel.GetType() == type) return true;
            }

            return false;
        }

        /// <summary>
        /// 打印当前存在的所有面板
        /// </summary>
        public void DebugAllPanel()
        {
            foreach (var layer in _layerDict.Values)
            {
                foreach (var panel in layer.PanelList)
                {
                    if (LogUtil.IsEnabled)
                        LogUtil.Log($"[layer]:{layer.LayerIndex}-{layer.LayerName}---[panel]:{panel}",
                            LogUtil.LogEnum.ONLINE_DEBUG);
                    if (panel.skinRoot?.touchable == false || panel.skinRoot?.enabled == false)
                    {
                        if (LogUtil.IsEnabled)
                            LogUtil.Log(
                                $"[layer]:{layer.LayerName}-warning!!! {panel}.skinRoot.touchable={panel.skinRoot.touchable},enabled={panel.skinRoot.enabled}",
                                LogUtil.LogEnum.ONLINE_DEBUG);
                        panel.skinRoot.touchable = true;
                        panel.skinRoot.enabled = true;
                    }
                }
            }
        }

        // 切换场景是需要关闭所有UI
        public void HideAllPanel(bool isRetainPopRecord = false)
        {
            for (var i = UILayerEnum.MAX - 1; i > 0; i--)
            {
                if (!_layerDict.TryGetValue(i, out var layer)) continue;
                if (i == UILayerEnum.BATTLE_UI)
                {
                    layer.RemoveBattleComp();
                }
                else
                {
                    layer.HideAllPanel(isRetainPopRecord);
                }
            }
        }

        // 关闭某一层的所有panel
        public void HideLayerPanel(UILayerEnum layer, bool isRetainPopRecord = false)
        {
            for (var i = UILayerEnum.MAX - 1; i > 0; i--)
            {
                if (layer != i) continue;
                if (!_layerDict.TryGetValue(i, out var l)) continue;
                l.HideAllPanel(isRetainPopRecord);
            }
        }

        // 关闭某一层之上的所有panel,不包括这一层
        // LinkTo时保留Pop记录
        public void HideAboveLayerPanel(UILayerEnum layer, bool isRetainPopRecord = false)
        {
            for (var i = UILayerEnum.MAX - 1; i > layer; i--)
            {
                if (!_layerDict.TryGetValue(i, out var l)) continue;
                l.HideAllPanel(isRetainPopRecord);
            }
        }

        // 返回上一个panel 会自动关闭当前panel
        // num 为需要返回的界面数
        public void Back(int returnPanelNum = 1)
        {
            if (_panelList.Count <= 0) return;
            var panel = _panelList[_panelList.Count - returnPanelNum];
            HidePanel(panel, false);
        }

        //panel恢复
        public void Reconnect()
        {
            //只对面板和弹出框进行恢复操作
            GetLayer(UILayerEnum.PANEL)?.Reconnect();
            GetLayer(UILayerEnum.POP)?.Reconnect();
        }

        public PanelBase GetPanelByName(string panelName)
        {
            return GetPanelByName(panelName, out var parentPanel);
        }

        public PanelBase GetPanelByName(string panelName, out PanelBase parentPanel)
        {
            for (var i = _panelList.Count - 1; i >= 0; i--)
            {
                var panel = _panelList[i];
                if (panelName == panel.config.panel)
                {
                    parentPanel = panel;
                    return panel;
                }
                else if (typeof(TabPanel).IsAssignableFrom(panel.GetType()))
                {
                    var openingPanel = ((TabPanel) panel).openingPanel;
                    if (openingPanel != null && openingPanel.config.panel == panelName)
                    {
                        parentPanel = panel;
                        return openingPanel;
                    }
                }
            }

            parentPanel = null;
            return null;
        }

        public PanelLayer GetLayer(UILayerEnum layer)
        {
            if (_layerDict.TryGetValue(layer, out var panelLayer))
            {
                return panelLayer;
            }

            return null;
        }

        /// <summary>
        /// 提前创建缓存的FGui面板
        /// </summary>
        /// <param name="panelType"></param>
        public void CreatePanelObjectFirst(Type panelType)
        {
            var panelBase = CreatePanel(panelType);
            panelBase.config.manuallyCreatePak = true;
            panelBase.InitCreate();
        }

        public PanelBase CreatePanel(Type panelType)
        {
            if (_panelDict.TryGetValue(panelType, out var panel)) return _panelDict[panelType];
            panel = (PanelBase) Activator.CreateInstance(panelType);
            _panelDict[panelType] = panel;
            return panel;
        }

        /// <summary>l
        /// 检测某一层级上是否有正在显示得显示对象
        /// </summary>
        public bool CheckLayerHasShowChild(UILayerEnum layer)
        {
            var layerRoot = GetLayer(layer).layerRoot;
            if (layerRoot.children?.Count > 0)
            {
                foreach (var _children in layerRoot.children)
                {
                    if (_children.visible) return true;
                }
            }

            return false;
        }

        // 根据路径获得root和组件
        public TwoEntry<GObject, GObject> GetUIComponent(string panelName, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var names = path.Split('|');
            var panel = GetPanelByName(panelName);

            if (panel == null) return null;
            //todo 目前先测试，明天需要改引导表
            if (panel.GetType() == typeof(ChallengePanelNew))
            {
                if (names.FirstOrDefault().Equals("challenge_btn"))
                {
                    names = new[] {names.FirstOrDefault()};
                }
            }

            var root = panel?.skinRoot;
            if (root == null) return null;
            GObject node = root;
            GObject obj = null;
            for (var i = 0; i < names.Length; i++)
            {
                string componentName = names[i];

                if (componentName.IndexOf("data=") >= 0)
                {
                    var checkData = componentName.Replace("data=", "");
                    var asCom = node.asCom;
                    if (asCom == null) return null;
                    for (int j = 0; j < asCom.numChildren; j++)
                    {
                        if (asCom.GetChildAt(j).data != null && asCom.GetChildAt(j).data.ToString() == checkData)
                        {
                            obj = asCom.GetChildAt(j);
                            if (node.asList != null)
                            {
                                node.asList.ScrollToView(j);
                            }

                            break;
                        }
                    }
                }
                else if (int.TryParse(componentName, out var childrenIndex))
                {
                    //配置中的下标标识第几个元素，因为c#转换int失败时默认值也为0，为了区分只好从物理下标1开始
                    childrenIndex -= 1;
                    //从列表中查询元素
                    if (node.asList == null) return null;

                    if (childrenIndex < node.asList.numItems)
                    {
                        var index = node.asList.ItemIndexToChildIndex(childrenIndex);
                        index = index < 0 ? childrenIndex : index;
                        obj = node.asList.GetChildAt(index);
                    }
                }
                else
                {
                    var com = node.asCom;
                    obj = com?.GetChild(componentName);
                }

                if (obj == null) return null;
                node = obj;
            }

            return new TwoEntry<GObject, GObject>(root, node);
        }

        public void DestroyManuallyCreatePanel()
        {
            if (_panelDict == null) return;
            foreach (var kp in _panelDict)
            {
                if (kp.Value.config.manuallyCreatePak)
                    kp.Value.config.manuallyCreatePak = false;
            }
        }

        /// <summary>
        /// 显示面板源方法
        /// </summary>
        /// <param name="panelType"></param>
        /// <param name="param"></param>
        /// <param name="isShowLoading"></param>
        /// <param name="isFromScene"></param>
        /// <returns></returns>
        private PanelBase ShowPanel(Type panelType, PanelParam param, bool isShowLoading, bool isFromScene)
        {
            if (null == panelType)
            {
                if (LogUtil.IsEnabled)
                    LogUtil.LogWarning("PanelManager:LoadAndShowPanel,无效的窗口类型", LogUtil.LogEnum.BASE_INFO);
                return null;
            }

            var panel = CreatePanel(panelType);
            LoadAndShowPanel(panel, param, isShowLoading, isFromScene);
            return panel;
        }

        /// <summary>
        /// 只有返回时调用(不恢复面板参数)
        /// </summary>
        /// <param name="panelType"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        internal PanelBase BackPanel(Type panelType, PanelParam param)
        {
            if (null == panelType)
            {
                if (LogUtil.IsEnabled)
                    LogUtil.LogWarning("PanelManager:LoadAndShowPanel,无效的窗口类型", LogUtil.LogEnum.BASE_INFO);
                return null;
            }

            var panel = CreatePanel(panelType);
            //返回时不记录
            LoadAndShowPanel(panel, param, false, false, false);
            return panel;
        }

        /// <summary>
        /// 只有LinkTo之后返回调用(恢复面板参数)
        /// </summary>
        /// <param name="panelType"></param>
        /// <param name="param"></param>
        /// <param name="isShowLoading"></param>
        /// <returns></returns>
        internal PanelBase RecoveryPanel(Type panelType, PanelParam param)
        {
            if (null == panelType)
            {
                if (LogUtil.IsEnabled)
                    LogUtil.LogWarning("PanelManager:LoadAndShowPanel,无效的窗口类型", LogUtil.LogEnum.BASE_INFO);
                return null;
            }

            var panel = CreatePanel(panelType);
            _layerDict.TryGetValue(panel.config.layer, out var layer);
            if (null == layer)
            {
                if (LogUtil.IsEnabled)
                    LogUtil.LogWarning($"PanelManager:LoadAndShowPanel,layer = {panel.config.layer}不存在",
                        LogUtil.LogEnum.BASE_INFO);
                return null;
            }

            layer.RecoveryPanel(panel, param);
            panel.PlayUISound(panel.config.panel);
            return panel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="param"></param>
        /// <param name="isShowLoading"></param>
        /// <param name="isFromScene"></param>
        /// <param name="isRecord">是否记录历史</param>
        private void LoadAndShowPanel(PanelBase panel, PanelParam param, bool isShowLoading, bool isFromScene,
            bool isRecord = true)
        {
            _layerDict.TryGetValue(panel.config.layer, out var layer);
            if (null == layer)
            {
                if (LogUtil.IsEnabled)
                    LogUtil.LogWarning($"PanelManager:LoadAndShowPanel,layer = {panel.config.layer}不存在",
                        LogUtil.LogEnum.BASE_INFO);
                return;
            }

            //如果有面板需要打开空场景，先打开空场景再开面板，而不是先开面板，再在面板中打开空场景
            if (panel.config.isHideScene && panel.config.style == PanelStyleEnum.FULL_SCREEN)
                FrameworkManager.Instance.sceneMan.ChangeToEmpty(param, panel.GetType(), isRecord);

            EnterShowPanel(layer, panel, param, isShowLoading, isFromScene);

            //添加进历史，目前只加Panel和Pop层
            // if(isRecord && panel.config.inBackList && 
            //    (panel.config.layer == UILayerEnum.POP || panel.config.layer == UILayerEnum.PANEL))
            //     EventManager.Instance.TriggerEvent(ClientEvent.PANEL_RECORD, panel, param, isFromScene);

            PushBackList(panel, param, isFromScene);
            panel.PlayUISound(panel.config.panel);
        }

        private void EnterShowPanel(PanelLayer layer, PanelBase panel, PanelParam param, bool isShowLoading,
            bool isFromScene)
        {
            if (!isFromScene)
            {
                layer.ShowPanel(panel, param);
                return;
            }

            EnterShowSceneLoading(layer, panel, param, isShowLoading);
        }

        // 暂时先不加
        private void EnterShowPanelLoading(PanelLayer layer, PanelBase panel, PanelParam param, bool isShowLoading)
        {
        }

        private void EnterShowSceneLoading(PanelLayer layer, PanelBase panel, PanelParam param, bool isShowLoading)
        {
            // 因为当前panel都是在scene的OnEnterStart中初始化 所以当前scene都是修改成最新的sceneType的
            var sceneType = FrameworkManager.Instance.sceneMan.curScene.curSubScene.sceneType;
            if (isShowLoading && !SceneLoadingConfig.JudgeInExceptionScene(sceneType))
            {
                ShowPanel(new CommonLoadingPanelParam {MistType = MistType.OPEN, isNewLoad = true}, false);
            }

            layer.ShowPanel(panel, param);
        }

        private void PushBackList(PanelBase panel, PanelParam param, bool isFromScene)
        {
            // 添加进返回栈  如果说此场景自带默认弹出的一个panel  那么该panel不允许进入栈内
            if (!isFromScene)
            {
                BackManager.Instance.PushScenePanel(new ScenePanelBase(panel, param));
            }
        }

        public void HidePanel(Type panelType, bool isImmediately)
        {
            if (!_panelDict.TryGetValue(panelType, out var panel)) return;
            HidePanel(panel, isImmediately);
        }

        private void HidePanel(PanelBase panel, bool isImmediately)
        {
            if (!_layerDict.TryGetValue(panel.config.layer, out var layer)) return;

            layer.HidePanel(panel, isImmediately);
        }

        /// <summary>
        /// 添加战斗显示层
        /// </summary>
        /// <param name="layer"></param>
        private void AddBattleLayer(UILayerEnum layer)
        {
            if (_layerDict.ContainsKey(layer)) return;
            var battleLayer = new PanelLayer(layer);
            _layerDict[layer] = battleLayer;
            //血条层
            var hpLayer = new GComponent();
            hpLayer.name = PanelLayer.BATTLE_UI_HP;
            battleLayer.layerRoot.AddChild(hpLayer);
            //boss血条层
            var bossHpLayer = new GComponent();
            bossHpLayer.name = PanelLayer.BATTLE_UI_BOSS_BLOOD;
            battleLayer.layerRoot.AddChild(bossHpLayer);
            //伤害飘字
            var hitLayer = new GComponent();
            hitLayer.name = PanelLayer.BATTLE_UI_HIT;
            battleLayer.layerRoot.AddChild(hitLayer);
            //buff
            var buffLayer = new GComponent();
            buffLayer.name = PanelLayer.BATTLE_UI_BUFF;
            battleLayer.layerRoot.AddChild(buffLayer);
            //技能
            var skillLayer = new GComponent();
            skillLayer.name = PanelLayer.BATTLE_UI_SKILL;
            battleLayer.layerRoot.AddChild(skillLayer);
            //效果
            var effectLayer = new GComponent();
            effectLayer.name = PanelLayer.BATTLE_UI_EFFECT;
            battleLayer.layerRoot.AddChild(effectLayer);
        }

        public GComponent GetBattleUILayer(string name)
        {
            GComponent child;
            var battleLayer = GetLayer(UILayerEnum.BATTLE_UI).layerRoot;
            var numChildren = battleLayer.numChildren;
            for (var i = 0; i < numChildren; i++)
            {
                child = battleLayer.GetChildAt(i) as GComponent;
                if (child.name.IndexOf(name) == 0) return child;
            }

            return null;
        }

        private void AddLayer(UILayerEnum layer)
        {
            if (_layerDict.ContainsKey(layer)) return;
            _layerDict[layer] = new PanelLayer(layer);
        }

        private void Update()
        {
            if (Time.frameCount % PANEL_CHECK_DISPOSE_FRAME == 0)
            {
                CheckUninstall();
            }
        }

        private void CheckUninstall()
        {
            if (_panelDict.Count <= 0) return;
            var deltaTime = Time.fixedTime - _lastCheckUninstallTime;
            _lastCheckUninstallTime = Time.fixedTime;

            PanelBase destroyPanel = null;
            bool isTabPanel = false;

            var enumerator = _panelDict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var panel = enumerator.Current.Value;
                // panel 如果未隐藏完毕 
                if (panel.panelState != PanelState.HIDE_OVER) continue;
                // panel 不能销毁
                // if (panel.config.notUseUninstall == false && PerformanceManager.Instance.IsSuperLow == false) continue;

                if (panel.config.notUseUninstall == false) continue;
                panel.hideTime += deltaTime;
                if (!(panel.hideTime > PANEL_DISPOSE_TIME)) continue;
                // 手动创建资源的不在这里回收
                if (panel.config.manuallyCreatePak)
                {
                    // 未缓存的TabPanel需要检查清理
                    if (!(panel is TabPanel p) || !p.CanDestroySub()) continue;
                    isTabPanel = true;
                }

                destroyPanel = panel;
                break;
            }

            if (null == destroyPanel) return;
            if (isTabPanel)
            {
                (destroyPanel as TabPanel).DestroySubPanel();
                return;
            }

            UninstallPanel(destroyPanel);
            destroyPanel.Destroy();
        }

        private void UninstallPanel(PanelBase panel)
        {
            // ab资源减少引用
            _panelDict.Remove(panel.GetType());
        }
    }
}