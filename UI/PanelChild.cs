using System;
using System.Collections.Generic;
using System.Reflection;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class PanelChild
    {
        public GComponent skinRoot { get; protected set; }

        protected GComponent parentSkin;
        
        protected bool isFixedSize;
        // 面板所需的数据
        protected object panelData;
        
        private bool _isInit;

        private Action _hideComplete;
        
        private List<Transition> _inAnimateList;
        private List<Transition> _outAnimateList;

        public void Show(GComponent parent, GComponent selfSkin,object data)
        {
            skinRoot = selfSkin;
            panelData = data;
            Init(parent);
            RealShow();
        }

        public void Show(GComponent parent,object data)
        {
            panelData = data;
            Init(parent);
            RealShow();
        }

        public void Hide(Action callback)
        {
            _hideComplete = callback;
            ClearAnimates();
            HideAnimation();
        }

        public void HideImmediately()
        {
            ClearAnimates();
            skinRoot.RemoveFromParent();
            OnHide();
        }
        
        public void Destroy()
        {
            OnDestroy();
            skinRoot?.Dispose();
            skinRoot = null;
        }

        public void VisibleChanged(bool isShow)
        {
            OnVisibleChanged(isShow);
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void ShowAnimation()
        {
            _inAnimateList.Clear();
            var transition = skinRoot.GetTransition("In");
            if (transition == null)
            {
                OnShowComplete();
                return;
            }
            _inAnimateList.Add(transition);
            transition.Play(OnShowComplete);
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void HideAnimation()
        {
            _outAnimateList.Clear();
            var transition = skinRoot.GetTransition("Out");
            if (transition == null)
            {
                HideComplete();
                return;
            }
            _outAnimateList.Add(transition);
            transition.Play(HideComplete);
        }

        protected void HideComplete()
        {
            _hideComplete?.Invoke();
            skinRoot.RemoveFromParent();
            OnHide();
        }
        
        protected virtual void OnHide()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        protected virtual void OnVisibleChanged(bool isShow)
        {
        }

        protected virtual void OnShowComplete()
        {
            
        }
        
        private void Init(GComponent parent)
        {
            if (_isInit) return;
            parentSkin = parent;
            _inAnimateList = new List<Transition>();
            _outAnimateList = new List<Transition>();
            OnInit();
            RegisterCompEvents(skinRoot);
            _isInit = true;
        }

        private void RealShow()
        {
            parentSkin.AddChild(skinRoot);
            if (isFixedSize)
            {
                skinRoot.Center(true);
            }
            else
            {
                skinRoot.SetSize(parentSkin.width,parentSkin.height);
                skinRoot.AddRelation(parentSkin, RelationType.Size);
            }

            ClearAnimates();
            ShowAnimation();
            OnShow();
        }

        private void ClearAnimates()
        {
            var list = new List<Transition>();
            list.AddRange(_inAnimateList);
            if (null !=_outAnimateList && _outAnimateList.Count > 0)
            {
                list.AddRange(_outAnimateList);
            }
            if (list.Count <= 0) return;
            for (var i = list.Count-1;i >=0; i--)
            {
                list[i].Stop(true,true);
            }
            list.Clear();
        }
        
        private void RegisterCompEvents(GComponent comp)
        {
            if (comp == null) return;
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
                var str = infos[0];
                if (GameConstant.UI_EVENT_CLICK != str) continue;
                var method =
                    $"On{StringUtils.UpperCamelCase(item.name)}{StringUtils.UpperFirstChar(str)}";
                var binder = GetType().GetMethod(method,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (null == binder)
                {
                    Debug.LogWarning($"GComponent {item.name} not register click event = {method}");
                }
                else
                {
                    item.onClick.Set(() => { binder.Invoke(this, null); });
                }
            }
        }
        
        private Dictionary<GObject, float> _protectBtns = new Dictionary<GObject, float>();

        protected void ProtectBtn(GObject btn, float timeSecond = 5)
        {
            if (_protectBtns == null) _protectBtns = new Dictionary<GObject, float>();
            if (_protectBtns.ContainsKey(btn)) _protectBtns.Remove(btn);
            _protectBtns.Add(btn, Time.time + timeSecond);
            btn.touchable = false;
            if (btn.displayObject != null && btn.displayObject is Container)
            {
                ((Container)btn.displayObject).touchChildren = false;
            }

            Timers.inst.Remove(RevertProtectBtn);
            Timers.inst.Add(1f, 0, RevertProtectBtn);
        }

        private void RevertProtectBtn(object param)
        {
            float curTime = Time.time;
            if (_protectBtns == null)
            {
                Timers.inst.Remove(RevertProtectBtn);
                return;
            }

            List<GObject> keys = new List<GObject>(_protectBtns.Keys);
            List<GObject> keysToRemove = new List<GObject>();
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                GObject key = keys[i];
                float value = _protectBtns[key];

                if (value <= curTime)
                {
                    if (!key.isDisposed)
                    {
                        key.touchable = true;
                        if (key.displayObject != null && key.displayObject is Container)
                        {
                            ((Container)key.displayObject).touchChildren = true;
                        }
                    }

                    keysToRemove.Add(key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _protectBtns.Remove(keysToRemove[i]);
            }

            if (_protectBtns.Count == 0)
            {
                Timers.inst.Remove(RevertProtectBtn);
            }
        }
    }
}