using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FastNet.Memory
{
    public class ObjectPool<T> : IObjectPool<T>
    {
        public const int DefaultInitialSize = 1000;

        private readonly Func<T> factory;
        private readonly int initialPoolSize;
        private readonly Stack<T> pool;

        public ObjectPool(Func<T> factory)
            : this(factory, DefaultInitialSize)
        {
        }

        public ObjectPool(Func<T> factory, int initialPoolSize)
        {
            this.factory = factory;
            this.initialPoolSize = initialPoolSize;
            this.pool = new Stack<T>();
            Initialize();
        }

        protected virtual void Initialize()
        {
            for (var i = 0; i < this.initialPoolSize; i++)
            {
                this.pool.Push(this.factory());
            }
        }

        public T Acquire()
        {
            T obj = default(T);
            if (this.pool.Count > 0)
            {
                obj = this.pool.Pop();
            }
            else
            {
                obj = this.factory();
            }
            return obj;
        }

        public void Release(T obj)
        {
            this.pool.Push(obj);
        }

        public void Dispose()
        {
            while (this.pool.Count > 0)
            {
                if (this.pool.Pop() is IDisposable disposableObj)
                {
                    disposableObj.Dispose();
                }
            }
        }
    }
}