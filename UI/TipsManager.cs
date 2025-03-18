using System;
using System.Collections.Generic;
using YatBun.Framework.Extentions;
using YatBun.Game.Core.Utils;
using FairyGUI;

namespace YatBun.Framework
{
    public class TipsManager : Singleton<TipsManager>, IManager
    {
        // panel 初始化的实例，仅仅只是用来做是否初始化的逻辑判断
        public Dictionary<Type, TipsBase> tipDict;

        public void OnInit()
        {
        }

        public void OnEnterGame()
        {
        }

        public void OnExitGame()
        {
        }

        public TipsManager()
        {
            tipDict = new Dictionary<Type, TipsBase>();
        }

        public TipsBase ShowTips(TipsParams param)
        {
            if (!tipDict.TryGetValue(param.panelType, out var panel))
            {
                panel = (TipsBase) Activator.CreateInstance(param.panelType);
                tipDict[param.panelType] = panel;
            }

            panel.Show(param);
            return panel;
        }

        public void HideTips<T>()
        {
            if (tipDict.TryGetValue(typeof(T), out var panel))
                panel.Hide();
        }
        
        public bool HasTips<T>()
        {
            return tipDict.TryGetValue(typeof(T), out var panel);
        }
    }

    public class TipsBase : IWaitable
    {
        protected GComponent SkinRoot { get; private set; }
        protected TipsConfig Config { get; set; }
        protected TipsParams PanelParam;
        protected Action HideCompleteCB;

        private Action _tipFinish;

        protected TipsBase()
        {
            InitConfig();
        }

        protected virtual void InitConfig()
        {
        }

        public void Show(TipsParams param)
        {
            PanelParam = param;
            var list = new List<string> {Config.PkgName};
            if (Config.Depends?.Length > 0)
            {
                list.AddRange(Config.Depends);
            }

            FrameworkManager.Instance.uiMan.LoadUIList(list, OnLoadComplete);
            CheckFinishActions();
        }

        public void Wait(Action onFinish)
        {
            _tipFinish += onFinish;
        }

        private void OnLoadComplete()
        {
            Init();
            DoShow();
        }

        private void Init()
        {
            if (SkinRoot != null)
            {
                if(LogUtil.IsEnabled)LogUtil.Log($"当前的skinRoot为：{SkinRoot}", LogUtil.LogEnum.FUNCTION, "TipsManager");
                return;
            }

            SkinRoot = UIPackage.CreateObject(Config.PkgName, Config.ResName).asCom;
            if (null == SkinRoot)
            {
                if(LogUtil.IsEnabled)LogUtil.Log($"Panel 创建面板组件失败：{Config.PkgName} {Config.Panel}", LogUtil.LogEnum.FUNCTION, "TipsManager");
                return;
            }

            SkinRoot.name = Config.Panel;
            OnInit();
        }

        // 这里调整布局
        private void DoShow()
        {
            OnShow();
            ShowAnimation();

            var centerX = (GRoot.inst.width - SkinRoot.width) / 2;
            var centerY = (GRoot.inst.height - SkinRoot.height) / 2;

            SkinRoot.SetXY(centerX, centerY + Config.OffsetY);
            SkinRoot.sortingOrder = (int) UILayerEnum.TIPS;
            AddToStage();
        }

        private void AddToStage()
        {
            GRoot.inst.AddChild(SkinRoot);
            if (Config.Manually) return;
            TimeTool.Instance.Delay(Config.Delay, Hide);
        }

        public void Hide()
        {
            HideAnimation();
        }

        private void HideComplete()
        {
            HideCompleteCB?.Invoke();
            TipsManager.Instance.tipDict.Remove(PanelParam.panelType);
            CheckFinishActions();
            GRoot.inst.RemoveChild(SkinRoot);
            SkinRoot.Dispose();
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void ShowAnimation()
        {
            var transition = SkinRoot.GetTransition("In");
            if (transition == null || transition.playing) return;
            transition.Play();
        }

        protected virtual void HideAnimation()
        {
            var transition = SkinRoot.GetTransition("Out");
            if (null == transition)
            {
                HideComplete();
                return;
            }

            transition.Play(HideComplete);
        }

        private void CheckFinishActions()
        {
            var finishHandler = _tipFinish;
            _tipFinish = null;
            finishHandler?.Invoke();
        }
    }

    public class TipsConfig
    {
        // Package name
        public string PkgName;

        // Resource name
        public string ResName;

        // Panel name
        public string Panel;

        // 加载依赖的UI资源
        public string[] Depends;

        // 延迟关闭时间
        public float Delay = 0.8f;

        // Y轴坐标(计算居中值的上下偏移值)
        public float OffsetY = 0;

        // 是否手动关闭
        public bool Manually = false;
    }
}