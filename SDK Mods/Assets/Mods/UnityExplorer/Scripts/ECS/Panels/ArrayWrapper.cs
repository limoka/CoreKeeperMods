using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Mods.UnityExplorer.Scripts.ECS.Panels
{
    public class ArrayWrapper<T> : IIndexable<T> where T : unmanaged
    {
        public T[] array;

        public ArrayWrapper(T[] array)
        {
            this.array = array;
        }

        public int Length
        {
            get => array.Length;
            set => Array.Resize(ref array, value);
        }

        public ref T ElementAt(int index)
        {
            return ref array[index];
        }
    }
}