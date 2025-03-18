using System;
using System.Collections.Generic;
using YatBun.Framework.Extentions;
using YatBun.Game.Helper;
using YatBun.Game.Module.Bag;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    /// <summary>
    /// 面板内的事件，点击，特效统一添加移除
    /// </summary>
    public class PanelCore : IWaitable
    {
        public PanelConfig config { get; protected set; }

        /// <summary>
        /// 红点事件数据
        /// </summary>
        private List<string> _reminders;

        /// <summary>
        /// 特效组件
        /// </summary>
        private EffectHelper _effectHelper;

        private Dictionary<int, Action<long>> _itemIdActions;

        /// <summary>
        /// 点击事件数据
        /// </summary>
        private Dictionary<GObject, EventCallback0> _onClicks;

        /// <summary>
        /// 侦听事件数据
        /// </summary>
        private Dictionary<ClientEvent, Delegate> _eventDict;

        /// <summary>
        /// 按钮连续点击保护
        /// </summary>
        private Dictionary<GObject, float> _protectBtns = new Dictionary<GObject, float>();

        /// <summary>
        /// 绑定红点，一个key只能绑定一个控件,界面关闭自动释放
        /// </summary>
        /// <param name="key"></param>
        public void ReminderBind(string key, GComponent comp)
        {
            if (_reminders == null) _reminders = new List<string>();
            if (_reminders.IndexOf(key) >= 0) return;
            _reminders.Add(key);
            Reminder.Bind(key, comp);
        }

        /// <summary>
        /// 绑定红点，一个key只能绑定一个控件
        /// </summary>
        /// <param name="key"></param>
        public void ReminderBind(string key, GameObject comp3d)
        {
            if (_reminders == null) _reminders = new List<string>();
            if (_reminders.IndexOf(key) >= 0) return;
            _reminders.Add(key);
            Reminder.Bind(key, comp3d);
        }

        /// <summary>
        /// 取消一个组件的红点关联
        /// </summary>
        /// <param name="key"></param>
        public void ReminderCancelComp(GComponent comp)
        {
            var node = Reminder.GetCompNode(comp);
            if (node != null && _reminders != null)
            {
                var key = node.Key;
                _reminders.Remove(key);
                Reminder.CancelComp(comp);
            }
        }

        /// <summary>
        /// 控件添加点击事件,界面关闭自动释放
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="callback"></param>
        public void OnClick(GObject obj, EventCallback0 callback)
        {
            if (_onClicks == null) _onClicks = new Dictionary<GObject, EventCallback0>();
            if (_onClicks.ContainsKey(obj)) return;
            _onClicks.Add(obj, callback);
            obj.onClick.Set(callback);
        }

        /// <summary>
        /// 添加侦听事件,界面关闭自动释放
        /// </summary>
        /// <param name="evtId"></param>
        /// <param name="call"></param>
        public void AddEvent(ClientEvent evtId, Action call)
        {
            if (_AddEvent(evtId, call)) EventManager.Instance.AddEvent(evtId, call);
        }

        /// 添加侦听事件,界面关闭自动释放
        public void AddEvent<T>(ClientEvent evtId, Action<T> call)
        {
            if (_AddEvent(evtId, call)) EventManager.Instance.AddEvent<T>(evtId, call);
        }

        /// 添加侦听事件,界面关闭自动释放
        public void AddEvent<T, U>(ClientEvent evtId, Action<T, U> call)
        {
            if (_AddEvent(evtId, call)) EventManager.Instance.AddEvent<T, U>(evtId, call);
        }

        /// 添加侦听事件,界面关闭自动释放
        public void AddEvent<T, U, V>(ClientEvent evtId, Action<T, U, V> call)
        {
            if (_AddEvent(evtId, call)) EventManager.Instance.AddEvent<T, U, V>(evtId, call);
        }

        /// 添加侦听事件,界面关闭自动释放
        public void AddEvent<T, U, V, W>(ClientEvent evtId, Action<T, U, V, W> call)
        {
            if (_AddEvent(evtId, call)) EventManager.Instance.AddEvent<T, U, V, W>(evtId, call);
        }

        /// <summary>
        /// 添加只执行一次的事件，侦听执行一次之后移除
        /// </summary>
        /// <param name="evtId"></param>
        /// <param name="call"></param>
        public void OnceEvent(ClientEvent evtId, Action call)
        {
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="evtId"></param>
        public void RemoveEvent(ClientEvent evtId)
        {
            if (_eventDict != null)
            {
                _eventDict.TryGetValue(evtId, out var call);
                _eventDict.Remove(evtId);
                EventManager.Instance.RemoveEvent(evtId, call);
            }
        }

        private bool _AddEvent(ClientEvent evtId, Delegate act)
        {
            if (_eventDict == null) _eventDict = new Dictionary<ClientEvent, Delegate>();
            if (_eventDict.ContainsKey(evtId)) return false;
            _eventDict.Add(evtId, act);
            return true;
        }

        /// <summary>
        /// 特效组件,界面关闭自动释放
        /// </summary>
        public EffectHelper effectHelper
        {
            get
            {
                if (_effectHelper == null)
                {
                    var hashCode = GetHashCode();
                    var master = config?.panel != null ? config.panel + hashCode : hashCode + "";
                    _effectHelper = EffectHelper.Init(master);
                }

                return _effectHelper;
            }
        }

        /// <summary>
        /// 根据道具Id，监听道具变化，界面关闭自动释放
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        public void AddIdCall(int id, Action<long> callback)
        {
            if (_itemIdActions == null) _itemIdActions = new Dictionary<int, Action<long>>();
            if (_itemIdActions.ContainsKey(id)) return;
            var item = ItemModule.Instance.GetItem(id);
            item.AddCall(callback);
            _itemIdActions.Add(id, callback);
        }

        /// <summary>
        /// 根据道具Id，移除监听道具变化，界面关闭自动释放
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        public void RemoveIdCall(int id, Action<long> callback)
        {
            var item = ItemModule.Instance.GetItem(id);
            item.RemoveCall(callback);
            _itemIdActions?.Remove(id);
        }

        /// <summary>
        /// 移除所有监听道具变化，界面关闭自动释放
        /// </summary>
        private void RemoveAllIdCall()
        {
            if (_itemIdActions != null)
            {
                foreach (var callInfo in _itemIdActions)
                {
                    var item = ItemModule.Instance.GetItem(callInfo.Key);
                    item.RemoveCall(callInfo.Value);
                }
            }
        }

        /// <summary>
        /// 按钮连续点击保护,界面关闭自动释放
        /// </summary>
        /// <param name="btn"></param>
        /// <param name="timeSecond"></param>
        public void ProtectBtn(GObject btn, float timeSecond = 5)
        {
            if (_protectBtns == null) _protectBtns = new Dictionary<GObject, float>();
            if (_protectBtns.ContainsKey(btn)) _protectBtns.Remove(btn);
            _protectBtns.Add(btn, Time.time + timeSecond);
            btn.touchable = false;
            if (btn.displayObject != null && btn.displayObject is Container)
            {
                ((Container) btn.displayObject).touchChildren = false;
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
                            ((Container) key.displayObject).touchChildren = true;
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

        /// <summary>
        /// 点击事件释放
        /// </summary>
        private void DisposeOnClick()
        {
            if (_onClicks == null) return;
            GObject obj;
            EventCallback0 callback;
            foreach (var clickInfo in _onClicks)
            {
                obj = clickInfo.Key;
                callback = clickInfo.Value;
                obj.onClick.Clear();
            }
        }

        /// <summary>
        /// 侦听事件释放
        /// </summary>
        private void DisposeEvent()
        {
            if (_eventDict == null) return;
            ClientEvent evtId;
            foreach (var eventInfo in _eventDict)
            {
                evtId = eventInfo.Key;
                _eventDict.TryGetValue(evtId, out var call);
                EventManager.Instance.RemoveEvent(evtId, call);
            }
        }

        /// <summary>
        /// 释放特效组件
        /// </summary>
        private void DisposeEffectHelper()
        {
            _effectHelper?.Dispose();
            _effectHelper = null;
        }

        /// <summary>
        /// 释放红点绑定
        /// </summary>
        private void DisposeReminders()
        {
            if (_reminders != null)
            {
                foreach (var reminderKey in _reminders)
                {
                    Reminder.ClearBind(reminderKey);
                }
            }
        }

        /// <summary>
        /// 按钮点击保护释放
        /// </summary>
        protected void DisposeProtectBtn()
        {
            Timers.inst.Remove(RevertProtectBtn);
            if (_protectBtns == null) return; //这里允许为空，表示没有被保护的btn
            foreach (KeyValuePair<GObject, float> data in _protectBtns)
            {
                GObject btn = data.Key;
                btn.touchable = true;
                if (btn.displayObject != null && btn.displayObject is Container)
                {
                    ((Container) btn.displayObject).touchChildren = true;
                }
            }

            _protectBtns.Clear();
        }

        /// <summary>
        /// 基类点击事件，侦听事件，特效组件移除
        /// </summary>
        protected virtual void OnHide()
        {
            Dispose();
        }

        /// <summary>
        /// 注销注册事件
        /// </summary>
        public void Dispose()
        {
            DisposeEvent();
            DisposeOnClick();
            DisposeReminders();
            DisposeEffectHelper();
            DisposeProtectBtn();
            RemoveAllIdCall();
            //清除红点数据
            _reminders?.Clear();
            _reminders = null;
            //清除事件数据
            _eventDict?.Clear();
            _eventDict = null;
            //清楚点击数据
            _onClicks?.Clear();
            _onClicks = null;
            //Id道具变化监听数据
            _itemIdActions?.Clear();
            _itemIdActions = null;
        }

        /// <summary>
        /// 销毁注册事件数据
        /// </summary>
        public virtual void Destroy()
        {
            Dispose();
            //清除红点数据
            _reminders?.Clear();
            _reminders = null;
            //清除事件数据
            _eventDict?.Clear();
            _eventDict = null;
            //清楚点击数据
            _onClicks?.Clear();
            _onClicks = null;
        }

        /// <summary>
        /// IWaitable接口方法
        /// </summary>
        /// <param name="onFinish"></param>
        public virtual void Wait(Action onFinish)
        {
        }
    }
}