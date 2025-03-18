using System.Collections.Generic;
using YatBun.Game.Module.Chat.Data;
using Game.Manager;

namespace YatBun.Framework
{
    public enum MarqueeTypeEnum
    {
        TOP,
        MIDDLE,
        TIP,
    }

    public class MarqueeManager : Singleton<MarqueeManager>, IManager
    {
        private Dictionary<MarqueeTypeEnum, IMarquee> _marquees;

        public void OnInit()
        {
            _marquees = new Dictionary<MarqueeTypeEnum, IMarquee>();
        }

        public void OnEnterGame()
        {
        }

        public void OnExitGame()
        {
            foreach (var marquee in _marquees)
            {
                marquee.Value.OnClear();
            }

            _marquees.Clear();
        }

        public void Prompt(int id, params object[] args)
        {
            Prompt(LangUtils.StrFormat(id, args));
        }

        public void Prompt(string txt, MarqueeTypeEnum type = MarqueeTypeEnum.MIDDLE)
        {
            if (string.IsNullOrWhiteSpace(txt)) return;

            _marquees.TryGetValue(type, out var marquee);
            if (marquee == null)
            {
                marquee = CreateMarqueeByType(type);
                marquee.OnInit();
                _marquees.Add(type, marquee);
            }

            marquee.Prompt(txt);
        }

        public void Prompt(int langId, MarqueeTypeEnum type = MarqueeTypeEnum.MIDDLE)
        {
            string str = LangUtils.Str(langId);
            Prompt(str, type);
        }

        public void Prompt(ChatMsg chatMsg, string txt, MarqueeTypeEnum type = MarqueeTypeEnum.MIDDLE)
        {
            if (string.IsNullOrWhiteSpace(txt)) return;

            _marquees.TryGetValue(type, out var marquee);
            if (marquee == null)
            {
                marquee = CreateMarqueeByType(type);
                marquee.OnInit();
                _marquees.Add(type, marquee);
            }

            marquee.Prompt(txt, chatMsg);
        }

        private IMarquee CreateMarqueeByType(MarqueeTypeEnum type)
        {
            switch (type)
            {
                case MarqueeTypeEnum.TOP:
                    return new MarqueeTop();
                case MarqueeTypeEnum.MIDDLE:
                    return new MarqueeMiddle();
                case MarqueeTypeEnum.TIP:
                    return new MarqueeTip();
                default:
                    return null;
            }
        }

        public void HideMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            marquee?.Hide();
        }

        public void ShowMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            marquee?.Show();
        }

        public void ClearMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            marquee?.OnClear();
        }

        public bool HasMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            return marquee?.IsPlaying() ?? false;
        }

        public void PauseMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            marquee?.PauseTween();
        }
        
        public void ResumeMarquee(MarqueeTypeEnum type = MarqueeTypeEnum.TIP)
        {
            _marquees.TryGetValue(type, out var marquee);

            marquee?.ResumeTween();
        }
    }
}