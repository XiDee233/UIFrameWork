using System.Collections.Generic;
using YatBun.Game.Module.Chat.Data;
using YatBun.UI.Basics;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class MarqueeMiddle : IMarquee
    {
        private Queue<string> _tipQueue;

        private Queue<GTweener> _tweenQueue;

        private Queue<GComponent> _activateQueue;

        private Queue<int> _timeEvents;

        private GObjectPool _gObjectPool;

        private const float cd = 0.1f;

        private float lastShowTime;

        private int _timeId;

        private bool isPlaying => _tweenQueue.Count > 0;

        public void OnInit()
        {
            _gObjectPool = new GObjectPool(null);
            _tipQueue = new Queue<string>();
            _tweenQueue = new Queue<GTweener>();
            _activateQueue = new Queue<GComponent>();
            _timeEvents = new Queue<int>();
        }

        public void Hide()
        {
        }

        public void Show()
        {
        }

        public bool IsPlaying()
        {
            return isPlaying;
        }

        public void PauseTween()
        {
        }

        public void ResumeTween()
        {
        }

        public GComponent GetItem()
        {
            var item = _gObjectPool.GetObject(UI_marquee_middle.URL) as GComponent;
            item.width = GRoot.inst.width;
            item.sortingOrder = (int) UILayerEnum.PROMPT;
            GRoot.inst.AddChild(item);
            item.x = (GRoot.inst.width - item.width) * 0.5f;
            item.y = (GRoot.inst.height - item.height) * 0.5f;
            item.visible = true;
            return item;
        }

        public void Prompt(string str)
        {
            _tipQueue.Enqueue(str);
            OnPlay();
        }

        public void Prompt(string str, ChatMsg chatMsg)
        {
            
        }

        public void OnPlay()
        {
            if (_tipQueue.Count == 0)
            {
                return;
            }

            if (_tweenQueue.Count >= 3)
            {
                return;
            }

            //cd
            float time = Time.unscaledTime - lastShowTime;
            if (time <= 0)
            {
                return;
            }

            if (time < cd)
            {
                TimeTool.Instance.RemoveTimeEvent(_timeId);
                _timeId = TimeTool.Instance.Countdown((float) time, OnPlay);
                return;
            }

            lastShowTime = Time.unscaledTime;

            var item = GetItem() as UI_marquee_middle;
            item.m_content_txt.text = _tipQueue.Dequeue();
            _activateQueue.Enqueue(item);
            var tween = item.TweenMoveY(item.y - 180, 0.8f * Time.timeScale).SetEase(EaseType.CircOut)
                .OnComplete(OnPlayComplete);
            _tweenQueue.Enqueue(tween);
            int id = TimeTool.Instance.Delay(5f, (() => { OnPlayComplete(tween); }));
            _timeEvents.Enqueue(id);
        }

        public void OnPlayComplete(GTweener tween)
        {
            var id = _timeEvents.Dequeue();
            TimeTool.Instance.RemoveTimeEvent(id);
            var item = _activateQueue.Dequeue();
            GRoot.inst.RemoveChild(item);
            _gObjectPool.ReturnObject(item);
            _tweenQueue.Dequeue();
            OnPlay();
        }

        public void OnClear()
        {
            _tipQueue.Clear();
            while (_tweenQueue.Count > 0)
            {
                var tween = _tweenQueue.Dequeue();
                tween?.Kill();
            }

            while (_activateQueue.Count > 0)
            {
                var item = _activateQueue.Dequeue();
                GRoot.inst.RemoveChild(item);
                _gObjectPool.ReturnObject(item);
            }

            TimeTool.Instance.RemoveTimeEvent(_timeId);
            _timeId = -1;
        }
    }
}