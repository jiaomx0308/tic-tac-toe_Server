using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MyLib
{
    public class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly SynchronizationContext _subContext;
        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();  //处于存储Await之后的函数体,一个高效的线程安全队列
        private int _pendingCount;

        public ActorSynchronizationContext(SynchronizationContext context = null)
        {
            this._subContext = context ?? new SynchronizationContext();
        }

        public override void Post(SendOrPostCallback d, object state)  //调用Await _messageQueue的函数,最终会调用的这个Post(),参数d为传入的Await后的函数体  //post()为异步线程调用
        {
            if (d == null) {
                throw new ArgumentNullException("SendOrPostCallback");
            }
            _pending.Enqueue(() => d(state));  //把该处理函数体加入等待队列
            if (Interlocked.Increment(ref _pendingCount) == 1)  //Interlocked是原子操作的接口,Increment表示递增传入的变量,当递增之后的结果为1,那么就要开始消息处理了  //用原子操作时为了防止多线程的竞争
            {
                try
                {
                    _subContext.Post(Consume, null); //调用post来异步处理消息,Consume进行消息处理,这里的null为传入的参数
                }
                catch (Exception exp)
                {
                    LogHelper.LogUnhandleException(exp.ToString());
                }
            }
        }

        //一次消息的处理是在统一的一个线程中顺序执行的,在Queue还没处理完之前,该线程不结束,顺序处理message;当消息处理完之后才等待开启下一次消息线程;
        //而消息处理中的message也归这次线程处理,使用ConcurrentQueue就是为了保证线程安全的EnQueue与DeQueue;
        /*而在Consume的SetSynchronizationContext(this),设置当前上下文为拥有该ActorSynchronizationContext的上下文时,
          是为了和SynchronizationContextAwaiter中在IsCompleted中判断如果为当前上下文就直接执行awaite后面的代码体保持一致,使所有的message都在
          拥有SynchronizationContext的线程上下文中执行,保证一个Actor执行messageQueue的地方只有这一个线程(拥有SynchronizationContext的线程)
         
             */

        private void Consume(object state)
        {
            var surroundContext = Current;  //Current为当前线程的同步上下文SynchronizationContext,不同的线程有不同的SynchronizationContext
            SetSynchronizationContext(this);  //设置当前同步上下文
            do
            {
                Action a;
                _pending.TryDequeue(out a);
                try
                {
                    a.Invoke();  //调用处理函数
                }
                catch (Exception exp)
                {
                    //Debug.LogError(exp.ToString());
                    LogHelper.LogUnhandleException(exp.ToString());
                }
            } while (Interlocked.Decrement(ref _pendingCount) > 0);  //对消息长度进行--
            SetSynchronizationContext(surroundContext);   //设置为原来的上下文,
        }

        public override void Send(SendOrPostCallback d, object state)  //send()是同步方法,我们这里没有使用
        {
            throw new NotSupportedException();
        }
        public override SynchronizationContext CreateCopy()
        {
            return this;
        }
    }
}

