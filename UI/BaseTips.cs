using FairyGUI;

namespace YatBun.Framework
{
    /// <summary>
    /// 要实现的效果是，点到其他地方就关闭，点到tips里面不需要关闭
    /// </summary>
    public class BaseTips : PanelBase
    {
        protected override void OnShow()
        {
            base.OnShow();
            GRoot.inst.onTouchBegin.Add(_OnTouched);
        }

        protected override void OnHide()
        {
            base.OnHide();
            GRoot.inst.onTouchBegin.Remove(_OnTouched);
        }

        private void _OnTouched(EventContext context)
        {
            var target = ((DisplayObject) context.initiator).gOwner;
            // 如果是点到了tips自身，那么就不做处理
            if (skinRoot!=null&&(skinRoot == target || skinRoot.IsAncestorOf(target))) return;
            
            // 否则就关闭这个tips
            Hide();
        }
    }
}