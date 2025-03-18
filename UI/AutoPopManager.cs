using System;
using System.Collections.Generic;
using UnityEngine;

namespace YatBun.Framework
{
    struct AutoPopParam
    {
        public PanelParam panelParam;
        public Type panelType;
        public List<string> excludeScenes;
        public List<string> includeScenes;
    }

    /// <summary>
    /// 自动弹窗管理
    /// 部分Panel只能在特定的场景才能弹出
    /// </summary>
    public class AutoPopManager : Singleton<AutoPopManager>, IManager
    {
        private List<AutoPopParam> _popList;
        private bool _isInPopping;

        public void OnInit()
        {
        }

        public void OnEnterGame()
        {
            _popList = new List<AutoPopParam>();
            _isInPopping = false;
            
            EventManager.Instance.AddEvent(ClientEvent.SCENE_SWITCH_OVER,OnSceneSwitchOver);
        }

        public void OnExitGame()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="scenes"> scene的名字，“~xxxx” 表示不包括这个场景,
        /// ！！！注：整个list的每一个元素，要么都加 “~” ，要么都不加 “~”</param>
        /// <typeparam name="T"></typeparam>
        public void AddPop<T>(List<string> scenes) where T : PanelBase
        {
            if (scenes == null || scenes.Count <= 0)
            {
                Debug.LogWarning("scenes can not be null");
                return;
            }

            var popParam = new AutoPopParam {panelType = typeof(T)};
            AddPop(popParam, scenes);
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <param name="scenes"> scene的名字，“~xxxx” 表示不包括这个场景,
        /// ！！！注：整个list的每一个元素，要么都加 “~” ，要么都不加 “~”</param>
        public void AddPop(PanelParam param, List<string> scenes)
        {
            var popParam = new AutoPopParam {panelParam = param};
            AddPop(popParam, scenes);
        }

        private void AddPop(AutoPopParam param, List<string> scenes)
        {
            for (var i = scenes.Count - 1; i >= 0; i--)
            {
                if (scenes[i].IndexOf("~") >= 0)
                {
                    if (param.excludeScenes == null)
                    {
                        param.excludeScenes = new List<string>();
                    }

                    param.excludeScenes.Add(scenes[i].Replace("~", ""));
                }
                else
                {
                    if (param.includeScenes == null)
                    {
                        param.includeScenes = new List<string>();
                    }

                    param.includeScenes.Add(scenes[i]);
                }
            }

            // 暂时没有添加优先级，弹出顺序按照添加的顺序；
            // 待需要优先级的时候，再加，然后排个序即可
            _popList.Add(param);
            CheckPop();
        }


        private void CheckPop()
        {
            if (_popList.Count <= 0 || _isInPopping) return;
            var sceneName = GetCurSceneName();
            var count = _popList.Count;
            var targetIndex = -1;
            for (var i = 0; i < count; i++)
            {
                var pop = _popList[i];
                if (pop.includeScenes != null)
                {
                    if (pop.includeScenes.IndexOf(sceneName) >= 0)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (pop.excludeScenes != null)
                {
                    if (pop.excludeScenes.IndexOf(sceneName) < 0)
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            if (targetIndex < 0) return;
            var target = _popList[targetIndex];
            _popList.RemoveAt(targetIndex);
            PopOne(target);
        }


        private void PopOne(AutoPopParam param)
        {
            _isInPopping = true;
            var panel = param.panelType == null ? 
                FrameworkManager.Instance.panelMan.ShowPanel(param.panelParam,true) : 
                FrameworkManager.Instance.panelMan.ShowPanel(param.panelType, true);
            panel.PushHideComplete(() =>
            {
                _isInPopping = false;
                CheckPop();
            });
        }


        private string GetCurSceneName()
        {
            var curScene = FrameworkManager.Instance.sceneMan.curScene;
            return curScene?.curSubScene?.subSceneConfig.sceneType;
        }

        private void OnSceneSwitchOver()
        {
            CheckPop();
        }
    }
}