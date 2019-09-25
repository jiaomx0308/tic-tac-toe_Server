using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using SimpleJSON;


namespace MyLib
{

	public static class ActorUtil  //Actor的工具,主要提供同步放入mailbox的msg消息
	{
		public static void SendMsg (this Actor target, string msg)
		{
			var m = new ActorMsg ();
			m.msg = msg;
			target.mailbox.SendAsync (m);
		}

		public static void SendMsg (this Actor target, KBEngine.Packet packet)
		{
			var m = new ActorMsg (){ packet = packet };
			target.mailbox.SendAsync (m);
		}

		public static void SendMsg (this Actor target, ActorMsg msg)
		{
			target.mailbox.SendAsync (msg);
		}
	}

	public class ActorMsg
	{
		public string msg;
		public KBEngine.Packet packet;
		public object obj;
		public object obj1;
	}

	/// <summary>
	/// Actor 的对外不应该提供同步调用的方法 Actor对外的都是
	/// Async的方法 通过同步的SynchronizationContext来同步外部调用 
	/// </summary>
	public class Actor  //Actor的概念很像Unity的GameObject,也是基于Component,只不过其基础组件变为了ActorSynchronizationContext,一种同步消息处理机制
    {
		protected List<Component> components = new List<Component> ();  //基于Component的组件对象

        private ActorSynchronizationContext _mq = new ActorSynchronizationContext();//邮箱和消息队列,一个Actor只有唯一一个队列来处理消息
    

        public int Id = -1;  //actor的地址,唯一标识,由ActorManager统一管理

        public BufferBlock<ActorMsg> mailbox = new BufferBlock<ActorMsg>();  //BufferBlock是一段线程安全的Buffer,用来保护数据
        protected bool isStop = false;  //当前Actor是否停止运行

        public ActorSynchronizationContext _messageQueue
        {
            get { return _mq; }
        }

        /// <summary>
        /// 需要在RunTask Init 调用之前初始化MessageQueue
        /// 一般 new Actor()
        /// 立即设置 MessageQueue
        /// 
        /// 要在ActorManager.AddActor 之前
        /// </summary>
        /// <param name="context"></param>
	    public void SetMsgQueue(ActorSynchronizationContext context)
	    {
	        //this._mq = context;
	    }


	    public bool IsStop()
	    {
	        return isStop;
	    }

	    public async Task<Component[]> GetComponents()
	    {
	        await _messageQueue;
	        return components.ToArray();
	    } 

		public Actor ()
		{
		}


		public async Task<T> GetComponentAsync<T> () where T : Component  //在components中返回具有参数类型T类型的component,很像Unity的GetCompoent<>;
        {
			await this._messageQueue;

			foreach (var c in components) { 
				if (c is T) {  //is检测两者类型是否相同
					return (T)c;
				}
			}
			return null;
		}

		public T GetComponent<T> () where T : Component  //异步取得component
		{
			foreach (var c in components) {
				if (c is T) {
					return (T)c;
				}
			}
			return null;
		}

		public async Task<T> AddComponentAsync<T> () where T : Component  //同步添加componpent
		{
			await this._messageQueue;
			var c = (T)Activator.CreateInstance (typeof(T));  //有点像Unity的Instantiate,创建该component,并把该component加入List<Component>
            components.Add (c);
			c.actor = this;  //也可以通过compont来指定Actor, 
			return c;
		}

		//初始化的时候添加Component 避免多线程添加
		//内部调用 外部需要调用 Task版本
		public T AddComponent<T> () where T : Component  //异步调用时使用, 适用于Actor初始化时,或者内部调用
		{
			var c = (T)Activator.CreateInstance (typeof(T));
			components.Add (c);
			c.actor = this;
            c.AfterAdd();
			return c;
		}

		/// <summary>
		/// 执行在自己的任务调度器中 
		/// </summary>
		protected async Task Dispatch ()  //DisPatch msg,在这主要是同步的从mailbox中取得msg,并同步执行msg的处理函数
		{
			while (!isStop) {
				var msg = await mailbox.ReceiveAsync ();    //等待mailbox sendAsync
				//Console.WriteLine ("threadId receive " + this.GetType () + " id " + Thread.CurrentThread.ManagedThreadId);
				//Console.WriteLine ("receive msg " + msg);
				await ReceiveMsg (msg);
			}

		}

		protected virtual async Task ReceiveMsg (ActorMsg msg)  //在Actor这个基类中什么都没做
		{
			await Task.FromResult (default(object));
		}

		/// <summary>
		/// 在当前Actor的Context 下启动一个Task
		/// 这样所有的Task启动，都需要去调用这个Context的Post方法 这样就保证了 Actor内部的Task都是在同一个线程执行的 
		/// </summary>
		/// <param name="cb">Cb.</param>
		public void RunTask (System.Func<Task> cb) //一个运行在_messageQueue内部的Task,具有处理程序cb,具有调度器,目前尚未启用  //相当于一直运行cb(Dispatch)
        {
			var surroundContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext (_messageQueue);   //保证在SynchronizationContext的线程上下文中执行
            var t =Task.Factory.StartNew (cb,
				CancellationToken.None,
				TaskCreationOptions.DenyChildAttach,
				TaskScheduler.FromCurrentSynchronizationContext ()
			);
			SynchronizationContext.SetSynchronizationContext (surroundContext);
		}

		/// <summary>
		/// 启动Dispatch接受消息队列消息
		/// 使用Actor自己的任务调度器
		/// </summary>
		public virtual void Init ()
		{
            //RunTask(Dispatch);
        }

        /// <summary>
        /// Actor 摧毁之后Componet清理
        /// </summary>
		public virtual void Stop ()
		{
			isStop = true;
		    foreach (var component in components)
		    {
		        component.Destroy();
		    }
		}

	    public virtual string GetAttr()
	    {
	        return "id: "+Id;
	    }
	}

	public class ActorManager
	{
		public static ActorManager Instance;
		Dictionary<int, Actor> actorDict;   //存储所有的Actor
		Dictionary<Type, Actor> actorType;

		private int actId = 0;  //ActorManager负责分配与管理所有Actor的Id
        bool isStop = false;

		public ActorManager ()
		{
			actorDict = new Dictionary<int, Actor> ();
			actorType = new Dictionary<Type, Actor> ();
			Instance = this;
		}

		/// <summary>
		/// 增加Actor将会引起副作用的代码放在锁外面 
		/// </summary>
		/// <returns>The actor.</returns>
		/// <param name="act">Act.</param>
		/// <param name="addType">If set to <c>true</c> add type.</param>
		public int AddActor (Actor act, bool addType = false)  //添加新的Actor
		{
			LogHelper.Log("Actor", "AddActor " + act + " addType " + addType);
			if (isStop) {
				return -1;
			}
			var id = Interlocked.Increment (ref actId);
			lock (actorDict) {
				actorDict.Add (id, act);
				if (addType) {
					actorType.Add (act.GetType (), act);
				}
			}
			act.Id = id;
			act.Init ();
			return id;
		}

	    /// <summary>
	    /// 移除Actor不会调用其它函数避免异常 
	    /// </summary>
	    /// <param name="id">Identifier.</param>
	    public void RemoveActor(int id)   //根据ID删除某个Actor
	    {
	        Actor act = null;
	        lock (actorDict)
	        {
	            if (actorDict.ContainsKey(id))
	            {
	                act = actorDict[id];
	                if (actorType.ContainsKey(act.GetType()))
	                {
	                    var act2 = actorType[act.GetType()];
	                    if (act2 == act)
	                    {
	                        actorType.Remove(act.GetType());
	                    }
	                }
	            }
	            actorDict.Remove(id);
	        }
	        if (act != null)
	        {
	            act.Stop();
	        }
	    }

	    public Actor GetActor (int key)  //根据地址获得一个Actor
		{
			Actor ret = null;
			lock (actorDict) {
				actorDict.TryGetValue (key, out ret);
			}
			return ret;
		}

		public T GetActor<T> () where T : Actor  //根据Type获得Actor,可能会有问题,因为Dictionary是需要key唯一的,但是Type这种类型很有可能重复
		{
			T ret = null;
			lock (actorDict) {
				Actor a = null;
				actorType.TryGetValue (typeof(T), out a);
				ret = (T)a;
			}
			return ret;
		}


		public void Stop ()  //把Actor设置为Stop
		{
			isStop = true;
			lock (actorDict) {
				foreach (var act in actorDict) {
					act.Value.Stop ();
				}
			}
		}

	    public override string ToString()
	    {
	        var sj = new SimpleJSON.JSONClass();

	        var jsonObj = new JSONClass();
	        KeyValuePair<int, Actor>[] ad;
	        lock (actorDict)
	        {
	            ad = actorDict.ToArray();
	        }

	        jsonObj.Add("ActorCount", new JSONData(ad.Length));
	        var jsonArray = new JSONArray();
	        foreach (var actor in ad)
	        {
	            var actorJson = new JSONClass();
	            actorJson.Add("type", new JSONData(actor.Value.GetType().ToString()));
	            var actorComponents = new JSONClass();
                /*
	            foreach (var compoent in actor.Value.GetComponents().Result)
	            {
	                actorComponents.Add("Component", new JSONData(compoent.GetType().ToString()));
	            }
                */
	            actorJson.Add("Components", actorComponents);
	            actorJson.Add("Attribute", actor.Value.GetAttr());
	            jsonArray.Add("Actor", actorJson);
	        }
	        jsonObj.Add("Actors", jsonArray);

	        sj.Add("AtorStatus", jsonObj);
	        return sj.ToString();
	    }
	}
}
