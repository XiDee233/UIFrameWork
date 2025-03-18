using System;
using System.Collections.Generic;
using FairyGUI;
using Game.Manager;

namespace YatBun.Framework
{
    public enum PanelStyleEnum
    {
        FIX_SIZE,
        FULL_SCREEN,
    }

    public class PanelConfig
    {
        // Package name
        public string pkgName;

        // Resource name
        public string resName;

        // Panel name
        public string panel;

        // 加载依赖的UI资源
        public string[] depends;

        // 窗口是否全屏显示
        public PanelStyleEnum style = PanelStyleEnum.FIX_SIZE;

        // 背景图片资源路径，动态加载的，只有在style为FULL_SCREEN的情况下才会有作用
        public string bgUrl;

        // 背景是否有缓动打开和缓动关闭的效果 只有在bgUrl有设置时，才有效
        public bool isBgTween = true;

        public UILayerEnum layer;

        public bool isModal;

        // 全屏窗口下是否隐藏3D场景 只有在style为FULL_SCREEN才有效
        public bool isHideScene = false;

        // 是否计入返回堆栈
        public bool inBackList = true;

        // 在非全屏界面，才会有这个判断
        public bool isShowCloseTip = true;

        // 关闭界面的提示语
        public string closeTip = LangUtils.Str(10002974);

        public float modalAlpha = 0.95f;

        // 模态逻辑处理 当弹窗需要显示黑底，但是点击黑底不会被关闭时，把isModalLogic设置为false即可，默认情况下逻辑与显示是统一的
        public bool isModalLogic = true;

        // 在未使用的情况下，销毁 true 表示要销毁，false 表示不会销毁
        public bool notUseUninstall = true;

        // 恢复时是否需要重新加载界面
        public bool recoveryReload = true;

        // 是否为手动实例化包，且Manager中不回收有此标记的资源，与notUseUninstall同等作用（只做标记）
        public bool manuallyCreatePak = false;

        /// <summary>
        /// 界面组件，必须有OnInit,OnShow,OnHide,Destroy方法
        /// </summary>
        public List<Type> panelPart;
    }
}