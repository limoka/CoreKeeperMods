using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus
{
    public readonly struct BrushRect : IEnumerable<int3>
    {
        public readonly int2 pos;
        
        public readonly int width;
        public readonly int height;

        public BrushRect(int width, int height) : this(int2.zero, width, height)
        {
            
        }
        
        public BrushRect(int2 pos, int width, int height)
        {
            this.pos = pos;
            this.width = width;
            this.height = height;
        }

        public IEnumerator<int3> GetEnumerator()
        {
            return new Enumerator(this);
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public BrushRect WithPos(int2 pos)
        {
            return new BrushRect(pos, width, height);
        }
        
        public BrushRect WithPos(Vector3Int pos)
        {
            return new BrushRect(pos.ToInt2(), width, height);
        }
        
        public struct Enumerator : IEnumerator<int3>
        {
            private readonly BrushRect m_Rect;
            private int m_xPos;
            private int m_yPos;

            public Enumerator(BrushRect rect)
            {
                m_Rect = rect;
                m_xPos = 0;
                m_yPos = -1;
            }

            public void Dispose() { }
            
            public bool MoveNext()
            {
                m_yPos++;
                if (m_yPos <= m_Rect.height) return true;
                
                m_yPos = 0;
                m_xPos++;
                
                if (m_xPos <= m_Rect.width) return true;
                
                return false;
            }

            public void Reset()
            {
                m_xPos = 0;
                m_yPos = -1;
            }

            public int3 Current => m_Rect.pos.ToInt3() + new int3(m_xPos, 0, m_yPos);
            object IEnumerator.Current => Current;
        }
    }
}