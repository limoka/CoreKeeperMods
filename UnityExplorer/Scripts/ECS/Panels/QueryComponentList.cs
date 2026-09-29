using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Widgets.ScrollView;

#if CPP
using Il2CppInterop.Runtime;
#endif

namespace ECSExtension.Panels
{
    public class QueryComponentList : ICellPoolDataSource<QueryComponentCell>
    {
        private List<SearchData> componentNames = new List<SearchData>();
        private ScrollPool<QueryComponentCell> scrollPool;
        public int ItemCount => componentNames.Count + 1;
        private LayoutElement viewportLayout;
        
        public QueryComponentList(ScrollPool<QueryComponentCell> scrollPool, LayoutElement viewportLayout)
        {
            this.scrollPool = scrollPool;
            this.viewportLayout = viewportLayout;
            scrollPool.Initialize(this);
            var sliderContainer = this.scrollPool.UIRoot.transform.Find("SliderContainer").gameObject;
            sliderContainer.SetActive(false);
            scrollPool.Refresh(true, true);
        }

        public ComponentType[] GetComponents(SearchType searchType)
        {
            return componentNames
                .Where(data => data.searchType == searchType)
                .Select(data => ReflectionUtility.GetTypeByName(data.componentName))
                .Where(type => type != null)
                .Select(type =>
                {
#if CPP
                    var il2cppType = Il2CppType.From(type);
                    return TypeManager.GetTypeIndex(il2cppType);
#else
                    return TypeManager.GetTypeIndex(type);
#endif
                })
                .Where(index => index >= 0)
                .Select(ComponentType.FromTypeIndex).ToArray();
        }
        
        public void OnCellBorrowed(QueryComponentCell cell)
        {
            cell.OnTextChanged += OnTextChanged;
            cell.OnSearchTypeChanged += OnSearchTypeChanged;
        }

        private void OnSearchTypeChanged(int index, int type)
        {
            if (index >= 0 && index < componentNames.Count)
            {
                componentNames[index].searchType = (SearchType)type;
            }

            viewportLayout.preferredHeight = ItemCount * 25;
            scrollPool.Refresh(true, true);
        }

        private void OnTextChanged(int index, string text)
        {
            if (index >= 0 && index < componentNames.Count)
            {
                if (string.IsNullOrEmpty(text))
                    componentNames.RemoveAt(index);
                else
                    componentNames[index].componentName = text;
            }
            else
            {
                componentNames.Add(new SearchData(text));
            }

            viewportLayout.preferredHeight = ItemCount * 25;
            scrollPool.Refresh(true, true);
        }

        public void SetCell(QueryComponentCell cell, int index)
        {
            if (index >= 0 && index < componentNames.Count)
                cell.ConfigureCell(index, componentNames[index]);
            else if (index < componentNames.Count + 1)
                cell.SetCellToDefault(index);
            else
                cell.Disable();
        }
        
        public class SearchData
        {
            public string componentName;
            public SearchType searchType;

            public SearchData(string componentName)
            {
                this.componentName = componentName;
            }
        }

        public enum SearchType
        {
            Include,
            Exclude
        }
    }
}