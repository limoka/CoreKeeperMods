
using Unity.Entities;

namespace ECSExtension
{
    public class BufferView<T> where T : unmanaged
    {
        public readonly DynamicBuffer<T> buffer;

        public BufferView(DynamicBuffer<T> buffer)
        {
            this.buffer = buffer;
        }
    }
}