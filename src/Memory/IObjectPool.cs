using System;

namespace FastNet.Memory
{
    public interface IObjectPool<T> : IDisposable
    {
        T Acquire();
        void Release(T obj);
    }
}