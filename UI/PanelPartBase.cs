using FairyGUI;
using Sirenix.OdinInspector;

namespace YatBun.Framework
{
    public class PanelPartBase : PanelCore
    {
        protected PanelBase _parent;
        protected GComponent _view;

        public PanelPartBase()
        {
        }

        public void Init(PanelBase parent)
        {
            _parent = parent;
        }

        public void InitCreate()
        {
            OnInitCreate();
        }

        /// <summary>
        /// 子界面初始化需要缓存的,可以在这里预缓存
        /// </summary>
        protected virtual void OnInitCreate()
        {
        }

        public virtual void OnInit()
        {
        }

        public virtual void OnShow()
        {
        }

        public virtual void OnShowComplete()
        {
        }

        public void CallPartOnHide()
        {
            OnHide();
        }

        protected override void OnHide()
        {
            _view = null;
            base.OnHide();
        }

        public virtual void OnReset()
        {
        }

        /// <summary>
        /// 销毁，需要后调用父节点销毁，可能用到父节点数据，从子节点依次向上销毁
        /// </summary>
        public override void Destroy()
        {
            _view = null;
            _parent = null;
            base.Destroy();
        }
    }
}