
using System;

namespace YatBun.Framework
{
    public class UiParam
    {
        public virtual Type panelType { get; set; }
    }
    

    public class TipsParams : UiParam
    {
        // public override Type panelType => typeof(TipsBase);
    }
    

    public class PanelParam : UiParam
    {
        // public override Type panelType => typeof(PanelBase);
    }
    
    public class PanelParam1<T> : PanelParam
    {
        public T val1;
    }
    
    public class PanelParam2<T, U> : PanelParam
    {
        public T val1;
        public U val2;
    }
}