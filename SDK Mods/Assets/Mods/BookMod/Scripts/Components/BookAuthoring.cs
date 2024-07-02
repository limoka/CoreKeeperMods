using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace BookMod.Components
{
    public struct BookCD : IComponentData
    {
        public int bookId;
    }

    public class BookAuthoring : MonoBehaviour
    {
        public int bookId;
    }

    public class BookConverter : SingleAuthoringComponentConverter<BookAuthoring>
    {
        protected override void Convert(BookAuthoring authoring)
        {
            AddComponentData(new BookCD()
            {
                bookId = authoring.bookId
            });
        }
    }
}