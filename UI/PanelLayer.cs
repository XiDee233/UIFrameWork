using System;
using System.Collections.Generic;
using YatBun.Game.Manager;
using YatBun.Game.Module.CommonUI;
using YatBun.Game.Module.Team.UI;
using YatBun.UI.Basics;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class PanelLayer
    {
        private const int MAX_NOTCH_HEIGHT = 200;
        private const string MASK_NAME = "MASK";
        private readonly int _layerIndex;
        private readonly string _layerName;
        private readonly List<PanelBase> _panelList;
        public GComponent layerRoot { get; private set; }
        public List<PanelBase> PanelList => _panelList;
        public string LayerName => _layerName;
        public int LayerIndex => _layerIndex;

        private GGraph _mask;
        private UI_close_tip _closeTip;
        private PanelBase _topPanel;
        private static bool _isCanClick = true;

        public const string BATTLE_UI_HP = "HP";
        public const string BATTLE_UI_HIT = "HIT";
        public const string BATTLE_UI_BUFF = "BUFF";
        public const string BATTLE_UI_SKILL = "SKILL";
        public const string BATTLE_UI_EFFECT = "EFFECT";
        public const string BATTLE_UI_BOSS_BLOOD = "BOSS_BLOOD";

        public GGraph Mask
        {
            get
            {
                if (null != _mask) return _mask;
                _mask = new GGraph
                {
                    name = MASK_NAME
                };
                _mask.DrawRect(GRoot.inst.width, GRoot.inst.height + MAX_NOTCH_HEIGHT * 2, 0, new Color(0, 0, 0, 1f),
                    new Color(0, 0, 0, 1f));
                _mask.y = -MAX_NOTCH_HEIGHT;
                return _mask;
            }
        }

        public PanelLayer(UILayerEnum index)
        {
            _layerIndex = (int) index;
            _layerName = string.Format(GameConstant.UI_LAYER_NAME, ((int) index).GetEnumName<UILayerEnum>());
            _panelList = new List<PanelBase>();
            Init();
        }

        public void ShowPanel(PanelBase panelBase, PanelParam param)
        {
            var index = _panelList.IndexOf(panelBase);
            // 窗口已经是显示状态，将其放到最上层
            if (index >= 0)
            {
                // 已经是最上层，不作处理，直接OnReShow 即可
                if (index == _panelList.Count - 1)
                {
                    panelBase.ReShow(param);
                    return;
                }

                // 先移除，再走统一流程
                _panelList.Remove(panelBase);
            }

            _panelList.Add(panelBase);
            InternalShow(panelBase, param);
            UpdateVisible();
        }

        public void HidePanel(PanelBase panelBase, bool isImmediately)
        {
            if (!_panelList.Contains(panelBase)) return;
            _panelList.Remove(panelBase);
            InternalHide(panelBase, isImmediately);
            UpdateVisible();
        }

        public void RecoveryPanel(PanelBase panelBase, PanelParam param)
        {
            if (!_panelList.Contains(panelBase))
                _panelList.Remove(panelBase);

            _panelList.Add(panelBase);
            InternalRecovery(panelBase, param);
            UpdateVisible();
        }

        public void Reconnect()
        {
            var list = new List<PanelBase>();
            list.AddRange(_panelList);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var panelBase = list[i];
                if (!panelBase.config.recoveryReload) continue;
                InternalHide(panelBase, true);
            }

            _panelList.Clear();
            foreach (var panelBase in list)
            {
                panelBase.Reconnect();
                if (!panelBase.config.recoveryReload)
                {
                    _panelList.Add(panelBase);
                    continue;
                }

                ShowPanel(panelBase, panelBase.panelParam);
            }

            list.Clear();
        }

        /// <summary>
        /// 移除战斗组件
        /// </summary>
        public void RemoveBattleComp()
        {
            var _children = layerRoot._children;
            foreach (var layer in _children)
            {
                (layer as GComponent).RemoveChildren();
            }
        }

        /// <summary>
        /// 隐藏这层所有面板
        /// </summary>
        /// <param name="isRetainPopRecord">是否保留Pop记录，在切换场景时保留</param>
        public void HideAllPanel(bool isRetainPopRecord = false)
        {
            if (_panelList.Count <= 0) return;
            for (var i = _panelList.Count - 1; i >= 0; i--)
            {
                if (i >= _panelList.Count) continue;
                if (_panelList[i] is TeamReadyPop || _panelList[i] is CommonLoadingPanel) continue;
                if (isRetainPopRecord) _panelList[i].MarkRetainPopRecord();
                InternalHide(_panelList[i], true);
            }

            _panelList.Clear();
        }

        public void AddToStage(PanelBase panelBase)
        {
            if (panelBase.config.style == PanelStyleEnum.FULL_SCREEN)
            {
                AddPanelBg(panelBase);

                if (panelBase.config.layer == UILayerEnum.POP)
                {
                    FitPop(panelBase);
                }
                else
                {
                    FitPanel(panelBase);
                }
            }
            else
            {
                if (Stage.inst.width/Stage.inst.height > AppConfig.MAX_ASPECT_RATIO)
                {
                    panelBase.skinRoot.SetXY((int)((AppConfig.DESIGN_WIDTH - panelBase.skinRoot.width) / 2), (int)((AppConfig.DESIGN_HEIGHT - panelBase.skinRoot.height) / 2), true);
                    panelBase.skinRoot.AddRelation(panelBase.skinRoot.root, RelationType.Center_Center);
                    panelBase.skinRoot.AddRelation(panelBase.skinRoot.root, RelationType.Middle_Middle);
                }
                else
                {
                    panelBase.skinRoot.Center(true);
                }
            }

            ChangeMask((() => { ChangeCloseTip(panelBase); }), true);
        }

        public void RemoveFromStage(PanelBase panelBase)
        {
            panelBase.skinRoot?.RemoveFromParent();
            _mask?.RemoveFromParent();
            _panelList.Remove(panelBase);
            if (_panelList.Count > 0)
            {
                // 还有弹窗在显示，更改mask层级和closeTip
                var panel = _panelList[_panelList.Count - 1];
                ChangeMask();
                ChangeCloseTip(panel);
            }
            else
            {
                ChangeMask();
                ChangeCloseTip(null);
            }
        }

        /// <summary>
        /// 获取该layer的顶层panel
        /// topPanel 处于显示中，如有panel处于关闭的过程中，返回null,关闭完成，返回当前正在显示的顶层
        /// </summary>
        /// <returns></returns>
        public PanelBase GetTopPanel()
        {
            return _topPanel;
        }

        public PanelBase GetTopShowPanel()
        {
            if (_panelList.Count < 1) return null;
            for (var i = _panelList.Count - 1; i >= 0; i--)
            {
                if ((_panelList[i].panelState == PanelState.SHOW_OVER ||
                     _panelList[i].panelState == PanelState.SHOW_START) &&
                    _panelList[i].skinRoot.visible
                )
                {
                    return _panelList[i];
                }
            }

            return null;
        }

        public void AddChild(GComponent component, int index)
        {
            layerRoot.AddChildAt(component, index);
        }

        private void InternalShow(PanelBase panelBase, PanelParam param)
        {
            panelBase.Show(param);
        }

        private void InternalRecovery(PanelBase panelBase, PanelParam param)
        {
            panelBase.Recovery(param);
        }

        private void InternalHide(PanelBase panelBase, bool isImmediately)
        {
            if (panelBase.isWaitForData)
            {
                panelBase.HideOffline();
            }
            else
            {
                if (isImmediately)
                {
                    panelBase.HideImmediately();
                }
                else
                {
                    panelBase.Hide();
                }
            }
        }

        private void Init()
        {
            layerRoot = new GComponent {gameObjectName = _layerName, name = _layerName};
            layerRoot.name = _layerName;

            var zv = _layerIndex == (int) UILayerEnum.PANEL ? 1000 : -1500 * (_layerIndex - 2);
            if (_layerIndex > (int) UILayerEnum.POP)
            {
                zv = -1500 * ((int) UILayerEnum.TIPS - 2);
            }

            if ((_layerIndex == (int)UILayerEnum.PANEL || _layerIndex == (int)UILayerEnum.POP) && Stage.inst.width/Stage.inst.height > AppConfig.MAX_ASPECT_RATIO)
            {
                layerRoot.SetSize(AppConfig.DESIGN_WIDTH, GRoot.inst.height);
                layerRoot.SetPosition((Stage.inst.width * AppConfig.DESIGN_HEIGHT/Stage.inst.height - AppConfig.DESIGN_WIDTH)/2, 0, zv);
                layerRoot.AddRelation(GRoot.inst, RelationType.Center_Center);
            }
            else
            {
                layerRoot.SetSize(GRoot.inst.width, GRoot.inst.height);
                layerRoot.SetPosition(0, 0, zv);
                layerRoot.AddRelation(GRoot.inst, RelationType.Size);
            }
            layerRoot.sortingOrder = _layerIndex;
            GRoot.inst.AddChild(layerRoot);
        }

        private void OnClickMask(EventContext context)
        {
            if (null == _topPanel || _panelList.Count <= 0 || _topPanel != _panelList[_panelList.Count - 1]) return;
            EventManager.Instance.TriggerEvent(ClientEvent.POP_LAYER_CLICK, _topPanel);
            if (!_topPanel.config.isModalLogic || _topPanel.InHideMode || !_isCanClick) return;
            _isCanClick = false;
            _mask.onClick.Remove(OnClickMask);
            HidePanel(_topPanel, false);
        }

        private void UpdateVisible()
        {
            // 防止pop层也有全屏fullScreen  导致前置pop界面会被隐藏
            if (_layerIndex != (int) UILayerEnum.PANEL) return;
            if (!Net.IsConnect()) return;
            var isFullWindow = false;
            for (var i = _panelList.Count - 1; i >= 0; i--)
            {
                var panel = _panelList[i];
                // 把全屏下的窗口隐藏掉
                if (isFullWindow)
                {
                    panel.SetVisible(false);
                    continue;
                }

                if (panel.config.style == PanelStyleEnum.FULL_SCREEN)
                {
                    isFullWindow = true;
                }

                panel.SetVisible(true);
            }
        }


        /**
         * 全屏窗口添加bg
         */
        private void AddPanelBg(PanelBase panelBase)
        {
            panelBase.AddPanelBg();
        }

        /// <summary>
        /// 刘海适配
        /// </summary>
        /// <param name="panelBase"></param>
        private void FitPanel(PanelBase panelBase)
        {
            UIManager.FitPanel(panelBase, layerRoot);
        }

        private void FitPop(PanelBase panelBase)
        {
            UIManager.FitPop(panelBase, layerRoot);
        }

        /// <summary>
        /// 更改mask层级 永远只关注最顶层
        /// </summary>
        /// <param name="top"></param>
        private void ChangeMask(Action call = null, bool needNext = false)
        {
            _topPanel = null;

            Timers.inst.Remove(AddClickNextFrame);

            if (_panelList.Count == 0)
            {
                _mask?.RemoveFromParent();
                return;
            }

            int modalIdx = -1;
            bool isModalTop = false;
            PanelBase top = _panelList[_panelList.Count - 1];

            for (int i = _panelList.Count - 1; i >= 0; i--)
            {
                PanelBase panel = _panelList[i];
                if (panel.config.isModal)
                {
                    modalIdx = i;
                    isModalTop = i == _panelList.Count - 1;
                    break;
                }
            }

            if (modalIdx == -1)
            {
                _mask?.RemoveFromParent();
                layerRoot.AddChild(top.skinRoot);
                _topPanel = top;
                return;
            }

            Mask.alpha = top.config.modalAlpha;
            _mask.onClick.Remove(OnClickMask);
            _isCanClick = false;
            if (isModalTop) layerRoot.AddChild(_mask);

            layerRoot.AddChild(top.skinRoot);
            _topPanel = top;
            call?.Invoke();

            Timers.inst.Add(0.14f, 1, AddClickNextFrame); //避免鼠标点击记录的是上一帧的东西
        }


        /// <summary>
        /// 点击关闭窗口组件是否需要显示 只有在pop层并且顶层窗口要显示的时候，才会显示这个组件
        /// </summary>
        /// <param name="top"> 顶层的panel </param>
        private void ChangeCloseTip(PanelBase top)
        {
            if (top == null || _layerIndex != (int) UILayerEnum.POP || !top.config.isShowCloseTip)
            {
                RemoveCloseTip();
            }
            else
            {
                AddCloseTip();
            }
        }

        private void AddCloseTip()
        {
            if (null == _closeTip)
            {
                _closeTip = UI_close_tip.CreateInstance();
                layerRoot.AddChild(_closeTip);
                _closeTip.touchable = false;
                _closeTip.opaque = true;
                _closeTip.y = layerRoot.height - _closeTip.height - 20;
                _closeTip.x = (layerRoot.width - _closeTip.width) * 0.5f;
                _closeTip.AddRelation(layerRoot, RelationType.Bottom_Bottom);
                _closeTip.AddRelation(layerRoot, RelationType.Bottom_Middle);
            }

            _closeTip.visible = true;
            _closeTip.m_txt.text = _topPanel.config.closeTip;

            // 修改第二次打开时文本tip跟mask错层问题
            layerRoot.AddChild(_closeTip);
        }

        private void RemoveCloseTip()
        {
            if (null == _closeTip) return;
            _closeTip.visible = false;
        }

        private void AddClickNextFrame(object obj)
        {
            _mask.onClick.Set(OnClickMask);
            _isCanClick = true;
        }
    }
}