using System;
using System.Collections.Generic;

namespace YatBun.Framework.Back
{
    /// <summary>
    /// 存放场景和panel的结构
    /// </summary>
    public class ScenePanelBase
    {
        public Type panelType = null; // @ panel type
        public PanelParam panelParam = null; // @ panel 参数
        public PanelConfig panelConfig;
        public string SceneName = ""; // @ 场景名字
        public object SceneParam = null; // @ 场景参数

        public ScenePanelBase(PanelBase panel, PanelParam param)
        {
            panelType = panel.GetType();
            panelConfig = panel.config;
            panelParam = param;
        }

        public ScenePanelBase(string sceneName, object sceneParam)
        {
            SceneName = sceneName;
            SceneParam = sceneParam;
        }
    }

    public static class ScenePanelConfig
    {
        private static List<string> _exceptionScene;
        private static List<string> _exceptionUIPanel;
        private static List<string> _removeUIPanel;

        static ScenePanelConfig()
        {
            _exceptionScene = new List<string>()
            {
                GameConstant.SUB_SCENE_EMPTY,
                GameConstant.SUB_SCENE_PLOT,
                GameConstant.SUB_SCENE_BATTLE,
            };

            _exceptionUIPanel = new List<string>()
            {
                "LoginPanel",
                "NoticePanel"
            };

            _removeUIPanel = new List<string>()
            {
                "CompetitionNewPanel",
            };
        }

        public static bool JudgeInExceptionScene(string sceneName)
        {
            return _exceptionScene.IndexOf(sceneName) >= 0;
        }

        public static bool JudgeInExceptionUIPanel(string panelName)
        {
            return _exceptionUIPanel.IndexOf(panelName) >= 0;
        }

        public static bool JudgeInRemoveUIPanel(string panelName)
        {
            return _removeUIPanel.IndexOf(panelName) >= 0;
        }
    }
}