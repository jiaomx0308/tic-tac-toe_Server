using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MyLib 
{
    public sealed class SynchronizationContextAwaiter : INotifyCompletion
    {
        private readonly SynchronizationContext _context;
        public SynchronizationContextAwaiter(SynchronizationContext context)
        {
            if(context == null ) throw new ArgumentNullException("context");
            _context = context;
        }
        public bool IsCompleted {
            get
            {
                //已经在当前上下文里面了，就不需要再次切换上下文了
                //表明了并不是所有的message都使用post放到了messagequeue中执行,如果在当前拥有SynchronizationContext的上下文中,该message就直接执行了
                return SynchronizationContext.Current == _context;
            }
        }

        /// <summary>
        /// 将Action 任务调度到 _context 控制的线程里面去执行
        /// 
        /// var temp = e.GetAwaiter();
        /// </summary>
        /// <param name="action">Action.</param>
        public void OnCompleted(Action action) {
            _context.Post(x=>action(), null);
        }
        public void GetResult(){}
    }
}

