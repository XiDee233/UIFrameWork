using System.Collections.Generic;
using YatBun.Game.Module.Chat.Data;
using YatBun.UI.Basics;
using FairyGUI;
using UnityEngine;

namespace YatBun.Framework
{
    public class MarqueeTop : IMarquee
    {
        private Queue<string> _tipQueue;

        private Queue<GTweener> _tweenQueue;
        private bool isPlaying => _tweenQueue.Count > 0;

        private Queue<GComponent> _activateQueue;
        private Queue<int> _timeEvents;

        private GObjectPool _gObjectPool;

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
            var item = _gObjectPool.GetObject(UI_marquee_top.URL) as GComponent;
            item.width = GRoot.inst.width;
            item.sortingOrder = (int) UILayerEnum.PROMPT;
            GRoot.inst.AddChild(item);
            item.y = 500;
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

            if (isPlaying)
            {
                return;
            }

            var item = GetItem() as UI_marquee_top;
            item.m_marquee.m_content_txt.text = _tipQueue.Dequeue();

            float startPos = item.m_marquee.width;
            float endPos = -item.m_marquee.m_content_txt.width;
            float time = Mathf.Abs((endPos - startPos)) / 200;

            item.m_marquee.m_content_txt.x = startPos;
            _activateQueue.Enqueue(item);
            var tween = item.m_marquee.m_content_txt.TweenMoveX(endPos, time).SetEase(EaseType.Linear)
                .OnComplete(OnPlayComplete);
            int id = TimeTool.Instance.Delay(time * 3, (() => { OnPlayComplete(tween); }));
            _timeEvents.Enqueue(id);

            _tweenQueue.Enqueue(tween);
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
        }
    }
}