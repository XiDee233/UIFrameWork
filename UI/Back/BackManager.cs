using System;
using System.Collections.Generic;
using System.Linq;

namespace YatBun.Framework.Back
{
    /// <summary>
    /// 控制scene & panel的返回逻辑
    /// </summary>
    public class BackManager : Singleton<BackManager>, IManager
    {
        /// <summary>
        /// 存放所有的uiPanel，无论是scene还是panel
        /// </summary>
        private Stack<ScenePanelBase> _scenePanelBases;

        private ScenePanelBase _hidePanel;

        /// <summary>
        /// 入栈
        /// @ condition-1 没有长度则直接push
        /// @ condition-2 如果当前push进去的和peek出来的一样，那么就直接return
        /// </summary>
        /// <param name="scenePanelBase"></param>
        public void PushScenePanel(ScenePanelBase scenePanelBase)
        {
            if (_scenePanelBases.Count <= 0)
            {
                Push(scenePanelBase);
                return;
            }

            if (JudgeScenePanelEqual(_scenePanelBases.Peek(), scenePanelBase)) return;

            Push(scenePanelBase);
            LogStacks(scenePanelBase);
        }

        /// <summary>
        /// @ 空场景和特殊场景排除
        /// @ 特殊UI也排除，config配置
        /// @ 排除不是全屏的UI
        /// @ 防止套娃  需要找到peek后的第一个是否与当前的相同
        /// </summary>
        /// <param name="scenePanelBase"></param>
        private void Push(ScenePanelBase scenePanelBase)
        {
            //空场景和一些特殊场景不push进去
            if (JudgeExceptionScene(scenePanelBase)) return;

            if (JudgeExceptionUIPanel(scenePanelBase)) return;

            if (!JudgeUIPanelExist(scenePanelBase))
            {
                _scenePanelBases.Push(scenePanelBase);
                return;
            }

            Remove2UIPanel(scenePanelBase);
            _scenePanelBases.Push(scenePanelBase);
        }

        public  void Remove(ScenePanelBase scenePanelBase)
        {
            RemoveUI(scenePanelBase);
            RemoveScene(scenePanelBase);
        }

        /// <summary>
        /// 如果此时界面手动关闭 & 栈内第一层的值为此界面  那么直接remove
        /// </summary>
        private void RemoveUI(ScenePanelBase scenePanelBase)
        {
            if (JudgeMinSize()) return;
            RemoveUIPanel(scenePanelBase);
            LogStacks();
        }

        public void RemoveScene(ScenePanelBase scenePanelBase)
        {
            if (JudgeMinSize()) return;
            RemoveScenePanel(scenePanelBase);
            LogStacks();
        }

        /// <summary>
        /// 返回
        /// </summary>
        public void Back()
        {
            if (JudgeMinSize())
            {
                ReShow(Peek());
                return;
            }

            LogStacks();

            // 如果返回的两个相邻的都是scene场景的话 那么直接调用switchScene
            // 如果返回的前一个是panel后一个是scene 那么直接调用switchScene
            _hidePanel = Pop();
            ScenePanelBase showPanel = Peek();
            if (JudgeSceneExist(showPanel))
            {
                ReShowScene(showPanel);
                return;
            }

            Go2Hide();
            Go2Show();
        }

        public void Hide()
        {
            if (JudgeMinSize()) return;

            LogStacks();
            _hidePanel = Pop();
            Go2Hide();
        }

        private void Go2Hide()
        {
            // 先取出最顶层并移除 -> 顶层的直接hide
            if (JudgeEmptyScene(_hidePanel)) return;

            Go2Hide(_hidePanel);
        }

        private void Go2Show()
        {
            // 取出最顶层 -> 作为reShow面板
            ScenePanelBase showPanel = Peek();
            // 如果当前返回的界面是个空场景 那么就直接往上再找
            if (JudgeEmptyScene(showPanel))
            {
                Back();
                return;
            }

            ReShow(showPanel);
        }

        #region show

        /// <summary>
        /// 重新显示最上层ui
        /// </summary>
        /// <param name="scenePanel"></param>
        private void ReShow(ScenePanelBase scenePanel)
        {
            if (scenePanel == null)
            {
                return;
            }

            if (JudgeSceneExist(scenePanel))
            {
                ReShowScene(scenePanel);
                return;
            }

            if (JudgeUIPanelExist(scenePanel))
            {
                ReShowUIPanel(scenePanel);
            }
        }

        private void ReShowUIPanel(ScenePanelBase scenePanel)
        {
            if (JudgeUIPanelParamExist(scenePanel))
            {
                PanelManager.Instance.ShowPanel(scenePanel.panelParam);
                return;
            }

            if (!JudgeUIPanelBaseExist(scenePanel)) return;

            PanelManager.Instance.ShowPanel(scenePanel.panelType, false);
        }

        private void ReShowScene(ScenePanelBase scenePanel)
        {
            var sceneMan = FrameworkManager.Instance.sceneMan;
            if (sceneMan.IsCurScene(scenePanel.SceneName))
            {
                //如果当前就是跑图  那么就显示这个  并清除所有记录
                if (!sceneMan.IsMainScene(scenePanel.SceneName))
                {
                    // 此时只需要关闭面板就好了
                    Go2Hide();
                    return;
                }

                sceneMan.SwitchScene(GameConstant.SUB_SCENE_GOFIELD, clearRecord: true);
                return;
            }

            sceneMan.SwitchScene(scenePanel.SceneName, scenePanel.SceneParam);
        }

        #endregion

        #region hide

        /// <summary>
        /// 关闭最上层ui
        /// </summary>
        /// <param name="scenePanel"></param>
        private void Go2Hide(ScenePanelBase scenePanel)
        {
            if (scenePanel == null)
            {
                return;
            }

            if (JudgeSceneExist(scenePanel))
            {
                Go2HideScene(scenePanel.SceneName);
                return;
            }

            if (JudgeUIPanelExist(scenePanel))
            {
                Go2HideUIPanel(scenePanel.panelType);
            }
        }

        private void Go2HideUIPanel(Type panelType)
        {
            PanelManager.Instance.HidePanel(panelType, true);
        }

        private void Go2HideScene(string sceneName)
        {
            Go2ShowEmptyScene();
        }

        private void Go2ShowEmptyScene()
        {
            var sceneMan = FrameworkManager.Instance.sceneMan;
            sceneMan.SwitchScene(GameConstant.SUB_SCENE_EMPTY);
        }

        #endregion

        #region pop remove

        private void RemoveUIPanel(ScenePanelBase scenePanel)
        {
            // 当前为ui 
            if (!JudgeUIPanelExist(scenePanel)) return;

            if (JudgeMinSize()) return;

            ScenePanelBase curPanel = Peek();

            // 都是不带参数
            if (!JudgeUIPanelBaseExist(curPanel)) return;

            if (scenePanel.panelType != curPanel.panelType) return;

            Pop();
        }

        // 一直删到最近的一个ui 
        private void Remove2UIPanel(ScenePanelBase scenePanel)
        {
            // 当前为ui 
            if (!JudgeUIPanelExist(scenePanel)) return;

            if (JudgeMinSize()) return;

            // 如果当前界面存在才可是往下走
            if (!JudgeIndexOfUIPanel(scenePanel)) return;

            var curPanel = Peek();

            // 都是不带参数
            if (!JudgeUIPanelBaseExist(curPanel)) return;

            if (scenePanel.panelType == curPanel.panelType)
            {
                Pop();
                return;
            }

            Pop();
            Remove2UIPanel(scenePanel);
        }

        // 直到找到return last 场景名一样的位置
        private void RemoveScenePanel(ScenePanelBase scenePanel)
        {
            if (!JudgeSceneExist(scenePanel)) return;

            if (JudgeMinSize()) return;

            if (!IsInStack(scenePanel.SceneName)) return;

            var curPanel = Peek();

            if (!JudgeSceneExist(curPanel))
            {
                Pop();
                RemoveScenePanel(scenePanel);
                return;
            }

            if (curPanel.SceneName == scenePanel.SceneName)
            {
                Pop();
                return;
            }

            Pop();
            RemoveScenePanel(scenePanel);
        }

        #endregion


        #region check

        private bool JudgeMinSize()
        {
            if (_scenePanelBases.Count <= 1)
            {
                return true;
            }

            return false;
        }

        private bool JudgeIndexOfUIPanel(ScenePanelBase scenePanel)
        {
            return _scenePanelBases.ToList().FindIndex(val =>
                JudgeUIPanelExist(val) && JudgeUIPanelExist(scenePanel) &&
                val.panelType == scenePanel.panelType) >= 0;
        }

        private bool JudgeUIPanelParamExist(ScenePanelBase scenePanel)
        {
            return scenePanel.panelParam != null;
        }

        private bool JudgeUIPanelBaseExist(ScenePanelBase scenePanel)
        {
            return scenePanel.panelType != null;
        }

        private bool JudgeUIPanelExist(ScenePanelBase scenePanel)
        {
            return JudgeUIPanelParamExist(scenePanel) || JudgeUIPanelBaseExist(scenePanel);
        }

        private bool JudgeSceneExist(ScenePanelBase scenePanel)
        {
            return !string.IsNullOrWhiteSpace(scenePanel.SceneName);
        }

        private bool JudgeScenePanelEqual(ScenePanelBase peekScenePanel, ScenePanelBase pushScenePanel)
        {
            if (JudgeSceneExist(peekScenePanel) && JudgeSceneExist(pushScenePanel))
            {
                return peekScenePanel.SceneName.Equals(pushScenePanel.SceneName);
            }

            if (!JudgeUIPanelExist(peekScenePanel) || !JudgeUIPanelExist(pushScenePanel)) return false;
            if (peekScenePanel.panelType != pushScenePanel.panelType) return false;
            // 如果引用不相等， 就换成新的
            Pop();
            _scenePanelBases.Push(pushScenePanel);
            return true;
        }


        private bool JudgeEmptyScene(ScenePanelBase scenePanel)
        {
            return JudgeSceneExist(scenePanel) && scenePanel.SceneName.Equals(GameConstant.SUB_SCENE_EMPTY);
        }

        private bool JudgeExceptionScene(ScenePanelBase scenePanel)
        {
            if (!JudgeSceneExist(scenePanel)) return false;

            return ScenePanelConfig.JudgeInExceptionScene(scenePanel.SceneName);
        }

        private bool JudgeExceptionUIPanel(ScenePanelBase scenePanel)
        {
            if (!JudgeUIPanelExist(scenePanel)) return false;

            // 暂时不需要判断inBackList了
            var panelCfg = scenePanel.panelConfig;
            var outException = panelCfg.inBackList && panelCfg.layer == UILayerEnum.PANEL &&
                               panelCfg.style == PanelStyleEnum.FULL_SCREEN;
            if (!outException) return true;

            return ScenePanelConfig.JudgeInExceptionUIPanel(panelCfg.panel);
        }

        private ScenePanelBase Pop()
        {
            return _scenePanelBases.Pop();
        }

        private ScenePanelBase Peek()
        {
            return _scenePanelBases.Peek();
        }

        private bool IsInStack(string sceneName)
        {
            foreach (var item in _scenePanelBases)
            {
                if (item.SceneName == sceneName) return true;
            }

            return false;
        }

        #endregion

        #region Debug

        private void LogStacks(ScenePanelBase scenePanelBase = null)
        {
            if (scenePanelBase != null && scenePanelBase.panelConfig != null &&
                !scenePanelBase.panelConfig.inBackList) return;
            var scenePanelNames = new List<string>();

            foreach (var scenePanel in _scenePanelBases)
            {
                if (JudgeSceneExist(scenePanel))
                {
                    scenePanelNames.Add(scenePanel.SceneName);
                    continue;
                }

                if (JudgeUIPanelExist(scenePanel))
                {
                    scenePanelNames.Add(scenePanel.panelConfig.panel);
                }
            }

            if (scenePanelBase != null)
            {
                var str = "";
                if (JudgeSceneExist(scenePanelBase))
                {
                    str = scenePanelBase.SceneName;
                }
                else if (JudgeUIPanelExist(scenePanelBase))
                {
                    str = scenePanelBase.panelConfig.panel;
                }

                return;
            }

        }

        #endregion

        public void OnInit()
        {
            _scenePanelBases = new Stack<ScenePanelBase>();
        }

        public void OnEnterGame()
        {
        }

        public void OnExitGame()
        {
            _scenePanelBases.Clear();
        }
    }
}