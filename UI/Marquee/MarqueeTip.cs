using System.Collections.Generic;
using YatBun.Excel;
using YatBun.Game.Manager;
using YatBun.Game.Module.Chat;
using YatBun.Game.Module.Chat.Data;
using YatBun.UI.Basics;
using DG.Tweening;
using DG.Tweening.Core;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class MarqueeTip : IMarquee
    {
        private Queue<string> _tipQueue;

        private Queue<GTweener> _tweenQueue;
        private bool isPlaying => _doTweenQueue.Count > 0;

        private Queue<GComponent> _activateQueue;
        private Queue<int> _timeEvents;

        private GObjectPool _gObjectPool;
        private bool _visible = true;
        private Queue<ChatMsg> _chatMsgQueue;
        private ChatMsg _chatMsg;
        private Queue<Tween> _doTweenQueue;
        private int _delayTimeId = -1;
        private Tween _tempTween = null;

        public void OnInit()
        {
            _gObjectPool = new GObjectPool(null);
            _tipQueue = new Queue<string>();
            _tweenQueue = new Queue<GTweener>();
            _activateQueue = new Queue<GComponent>();
            _timeEvents = new Queue<int>();
            _chatMsgQueue = new Queue<ChatMsg>();
            _doTweenQueue = new Queue<Tween>();
        }

        public void Hide()
        {
            _visible = false;
            if (!isPlaying || _timeEvents.Count <= 0 || _activateQueue.Count <= 0) return;
            var item = _activateQueue.Peek();
            item.visible = false;
        }

        public void Show()
        {
            _visible = true;
            if (!isPlaying || _timeEvents.Count <= 0 || _activateQueue.Count <= 0) return;
            var item = _activateQueue.Peek();
            item.visible = true;
        }

        public bool IsPlaying()
        {
            return isPlaying;
        }

        public void PauseTween()
        {
            if (!IsPlaying()) return;
            if (_doTweenQueue.Count > 0)
            {
                var t = _doTweenQueue.Dequeue();
                t.Kill();
            }
            
            if (_activateQueue.Count > 0)
            {
                var item = _activateQueue.Dequeue();
                GRoot.inst.RemoveChild(item);
                _gObjectPool.ReturnObject(item);
            }

            _chatMsg = null;
        }

        public void ResumeTween()
        {
            // _tempTween = null;
            OnPlay();
        }

        public GComponent GetItem()
        {
            var item = _gObjectPool.GetObject(UI_marquee_tips.URL) as GComponent;
            item.sortingOrder = (int) UILayerEnum.MARQUEE;
            GRoot.inst.AddChild(item);
            item.x = (GRoot.inst.width - item.width) / 2;
            item.y = 250;
            item.visible = _visible;
            return item;
        }

        public void Prompt(string str)
        {
            _tipQueue.Enqueue(str);
            OnPlay();
        }

        public void Prompt(string str, ChatMsg chatMsg)
        {
            _tipQueue.Enqueue(str);
            _chatMsgQueue.Enqueue(chatMsg);
            OnPlay();
        }

        public void OnPlay()
        {
            if (_tipQueue.Count == 0)
            {
                return;
            }

            if (isPlaying)
            {
                return;
            }

            CheckScene();
            var item = GetItem() as UI_marquee_tips;
            var cell = item.m_tips_cell;
            var context = item.m_tips_cell.m_tips_text;
            var richText = context.richTextField;
            context.text = _tipQueue.Dequeue();

            // 系统聊天频道LinkTo
            if (_chatMsgQueue.Count > 0)
            {
                var chatMsg = _chatMsgQueue.Dequeue();
                _chatMsg = chatMsg;
                richText.onClickLink.Remove(OnClickLink);
                richText.onClickLink.Add(OnClickLink);
            }

            float startPos = cell.width;
            float endPos = -context.width;
            float time = Mathf.Abs((endPos - startPos)) / 200;

            context.x = startPos;
            _activateQueue.Enqueue(item);
            DOGetter<float> getX = () => context.x;
            DOSetter<float> setX = value => context.x = value;
            Tween t = DOTween.To(getX, setX, endPos, time)
                .SetEase(Ease.Linear)
                .OnComplete(OnPlayComplete).SetUpdate(true);

            int id = TimeTool.Instance.Delay(time * 3, (OnPlayComplete), true);
            _timeEvents.Enqueue(id);
            _doTweenQueue.Enqueue(t);
        }

        public void OnPlayComplete(GTweener tween)
        {
        }

        private void OnClickLink()
        {
            // 战斗中跳转不生效
            if (FrameworkManager.Instance.sceneMan.IsBattle() || BattleManager.Instance.IsBattleActive()) return;
            ChatModule.Instance.OnLinkMsgClick(_chatMsg);
        }

        private void CheckScene()
        {
            // 目前只在战斗及主界面显示
            // _visible = !FrameworkManager.Instance.sceneMan.IsBattle() && !BattleManager.Instance.IsBattleActive() && !FrameworkManager.Instance.sceneMan.IsPlot();
            _visible = FrameworkManager.Instance.sceneMan.IsBattle() || BattleManager.Instance.IsBattleActive() ||
                       FrameworkManager.Instance.sceneMan.IsMainScene();
        }

        public void OnPlayComplete()
        {
            var id = _timeEvents.Dequeue();

            TimeTool.Instance.RemoveTimeEvent(id);

            _tempTween = null;

            if (_doTweenQueue.Count > 0)
            {
                var t = _doTweenQueue.Dequeue();
                t.Kill();
            }

            if (_activateQueue.Count > 0)
            {
                var item = _activateQueue.Dequeue();
                GRoot.inst.RemoveChild(item);
                _gObjectPool.ReturnObject(item);
            }

            _chatMsg = null;

            if (StaticData.game.msgCoolDown > 0)
            {
                _delayTimeId = TimeTool.Instance.Delay(StaticData.game.msgCoolDown, OnDelayTimeOver, true);
            }
            else
            {
                OnDelayTimeOver();
            }
        }

        private void OnDelayTimeOver()
        {
            if (_delayTimeId > 0)
            {
                TimeTool.Instance.RemoveTimeEvent(_delayTimeId);
                _delayTimeId = -1;
            }

            EventManager.Instance.TriggerEvent(ClientEvent.MARQUEE_CHECK);
            OnPlay();
        }

        public void OnClear()
        {
            _tipQueue.Clear();
            _chatMsgQueue.Clear();
            _chatMsg = null;
            if (_tempTween != null) _tempTween = null;
            if (_delayTimeId > 0)
            {
                TimeTool.Instance.RemoveTimeEvent(_delayTimeId);
                _delayTimeId = -1;
            }

            while (_timeEvents.Count > 0)
            {
                var id = _timeEvents.Dequeue();
                TimeTool.Instance.RemoveTimeEvent(id);
            }

            while (_doTweenQueue.Count > 0)
            {
                var doTween = _doTweenQueue.Dequeue();
                doTween?.Kill();
            }

            while (_activateQueue.Count > 0)
            {
                var item = _activateQueue.Dequeue() as UI_marquee_tips;
                GRoot.inst.RemoveChild(item);
                _gObjectPool.ReturnObject(item);
            }
        }
    }
}