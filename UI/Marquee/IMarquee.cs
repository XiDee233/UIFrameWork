using YatBun.Game.Module.Chat.Data;
using FairyGUI;

namespace YatBun.Framework
{
    public interface IMarquee
    {
        void OnInit();
        GComponent GetItem();
        void Prompt(string str);
        void Prompt(string str,ChatMsg chatMsg);
        void OnPlay();
        void OnPlayComplete(GTweener tween);
        void OnClear();
        void Hide();
        void Show();
        bool IsPlaying();
        void PauseTween();
        void ResumeTween();
    }
}